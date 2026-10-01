using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

// ═══════════════════════════ Accounting (double-entry) ═══════════════════════════
public class AccountingService : IAccountingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AccountingService> _log;
    public AccountingService(AppDbContext db, ILogger<AccountingService> log) => (_db, _log) = (db, log);

    /*
     * Auto journal on order completion:
     *   Dr 1100 Cash  (or 1200 AR for online gateways)   TOTAL
     *       Cr 4000 Sales Revenue                        SUBTOTAL − DISCOUNT
     *       Cr 2100 VAT Payable                          TAX
     * Cost side (COGS) posts from inventory valuation when the accountant runs
     * period close — kept explicit to avoid double-counting perishables waste.
     */
    public async Task PostSaleAsync(Guid orderId)
    {
        var order = await _db.Orders.Include(o => o.Payments).Include(o => o.Table).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return;
        if (await _db.JournalEntries.AnyAsync(j => j.SourceId == orderId && j.SourceType == "Sale")) return; // idempotent

        var accounts = await _db.Accounts.ToDictionaryAsync(a => a.Code);
        var online = order.Payments.Any(p => p.Status == PaymentStatus.Paid && (int)p.Gateway >= 10);
        var debitAcc = accounts.GetValueOrDefault(online ? "1200" : "1100") ?? accounts.Values.First();

        var entry = new JournalEntry
        {
            EntryNumber = $"JE-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100, 999)}",
            PostedAt = DateTime.UtcNow, Description = $"Sale {order.OrderNumber}" + (order.Table != null ? $" — Table {order.Table.Number}" : ""),
            SourceType = "Sale", SourceId = order.Id, BranchId = order.BranchId
        };
        entry.Lines.Add(new JournalLine { AccountId = debitAcc.Id, Debit = order.Total, Memo = $"Order {order.OrderNumber}" });
        if (order.SubTotal - order.Discount > 0)
            entry.Lines.Add(new JournalLine { AccountId = accounts["4000"].Id, Credit = order.SubTotal - order.Discount, Memo = "Revenue" });
        if (order.Tax > 0)
            entry.Lines.Add(new JournalLine { AccountId = accounts["2100"].Id, Credit = order.Tax, Memo = "VAT 9%" });
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
        _log.LogInformation("Sale journal {Entry} posted for order {Order}", entry.EntryNumber, order.OrderNumber);
    }

    public async Task<ExpenseDto> AddExpenseAsync(ExpenseUpsertDto dto)
    {
        var expense = new Expense { Title = dto.Title, Category = dto.Category, Amount = dto.Amount, SpentAt = dto.SpentAt, PaidTo = dto.PaidTo, Note = dto.Note, BranchId = dto.BranchId };
        _db.Expenses.Add(expense);

        var accounts = await _db.Accounts.ToDictionaryAsync(a => a.Code);
        var entry = new JournalEntry
        {
            EntryNumber = $"JE-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100, 999)}",
            PostedAt = DateTime.UtcNow, Description = $"Expense — {dto.Title}", SourceType = "Expense", SourceId = expense.Id, BranchId = dto.BranchId
        };
        entry.Lines.Add(new JournalLine { AccountId = accounts["6000"].Id, Debit = dto.Amount, Memo = dto.Category });
        entry.Lines.Add(new JournalLine { AccountId = accounts["1100"].Id, Credit = dto.Amount, Memo = "Cash out" });
        _db.JournalEntries.Add(entry);
        expense.JournalEntryId = entry.Id;
        await _db.SaveChangesAsync();
        return ToDto(expense);
    }

    public async Task<List<ExpenseDto>> GetExpensesAsync(Guid? branchId, DateTime? from, DateTime? to)
    {
        var q = _db.Expenses.AsNoTracking().AsQueryable();
        if (branchId != null) q = q.Where(e => e.BranchId == branchId);
        if (from != null) q = q.Where(e => e.SpentAt >= from);
        if (to != null) q = q.Where(e => e.SpentAt <= to);
        var list = await q.OrderByDescending(e => e.SpentAt).Take(300).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<List<JournalEntryDto>> GetJournalsAsync(Guid? branchId, int take = 100)
    {
        var q = _db.JournalEntries.AsNoTracking().Include(j => j.Lines).ThenInclude(l => l.Account).AsQueryable();
        if (branchId != null) q = q.Where(j => j.BranchId == branchId);
        var list = await q.OrderByDescending(j => j.PostedAt).Take(take).ToListAsync();
        return list.Select(j => new JournalEntryDto
        {
            Id = j.Id, EntryNumber = j.EntryNumber, PostedAt = j.PostedAt, Description = j.Description, SourceType = j.SourceType,
            Lines = j.Lines.Select(l => new JournalLineDto { AccountCode = l.Account?.Code ?? "?", AccountName = l.Account?.Name ?? "?", Debit = l.Debit, Credit = l.Credit, Memo = l.Memo }).ToList()
        }).ToList();
    }

    public async Task<List<TrialBalanceRow>> GetTrialBalanceAsync(Guid? branchId, DateTime? from, DateTime? to)
    {
        var q = _db.JournalEntries.AsNoTracking().Include(j => j.Lines).ThenInclude(l => l.Account).AsQueryable();
        if (branchId != null) q = q.Where(j => j.BranchId == branchId);
        if (from != null) q = q.Where(j => j.PostedAt >= from);
        if (to != null) q = q.Where(j => j.PostedAt <= to);
        var rows = await q.SelectMany(j => j.Lines).ToListAsync();
        return rows.GroupBy(l => new { l.Account!.Code, l.Account.Name, l.Account.Group })
            .OrderBy(g => g.Key.Code)
            .Select(g => new TrialBalanceRow { Code = g.Key.Code, Name = g.Key.Name, Group = g.Key.Group, Debit = g.Sum(l => l.Debit), Credit = g.Sum(l => l.Credit) })
            .ToList();
    }

    public async Task<ProfitLossDto> GetProfitLossAsync(Guid? branchId, DateTime? from, DateTime? to)
    {
        var expenses = await GetExpensesAsync(branchId, from, to);
        var salesQ = _db.Orders.AsNoTracking().Where(o => o.Status == OrderStatus.Completed);
        if (branchId != null) salesQ = salesQ.Where(o => o.BranchId == branchId);
        if (from != null) salesQ = salesQ.Where(o => o.CompletedAt >= from);
        if (to != null) salesQ = salesQ.Where(o => o.CompletedAt <= to);
        var orders = await salesQ.ToListAsync();
        return new ProfitLossDto
        {
            Revenue = orders.Sum(o => o.SubTotal - o.Discount),
            VatPayable = orders.Sum(o => o.Tax),
            Expenses = expenses.Sum(e => e.Amount),
            OrdersCount = orders.Count,
            ExpenseBreakdown = expenses.Take(20).ToList()
        };
    }

    internal static ExpenseDto ToDto(Expense e) => new()
    { Id = e.Id, Title = e.Title, Category = e.Category, Amount = e.Amount, SpentAt = e.SpentAt, PaidTo = e.PaidTo, Note = e.Note, BranchId = e.BranchId };
}

// ═══════════════════════════ Reports / Dashboard ═══════════════════════════
public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly IEventService _events;
    private readonly IInventoryService _inventory;
    public ReportService(AppDbContext db, IEventService events, IInventoryService inventory) => (_db, _events, _inventory) = (db, events, inventory);

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid? branchId)
    {
        var today = DateTime.UtcNow.Date;
        var ordersQ = _db.Orders.AsNoTracking().Where(o => o.Status != OrderStatus.Cancelled);
        if (branchId != null) ordersQ = ordersQ.Where(o => o.BranchId == branchId);

        var todayCompleted = await ordersQ.Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= today).ToListAsync();
        var activeStatuses = new[] { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served };
        var openOrders = await ordersQ.CountAsync(o => activeStatuses.Contains(o.Status));
        var lowStock = await _inventory.LowStockCountAsync(null);
        var tablesQ = _db.Tables.AsNoTracking().Where(t => !t.IsDeleted);
        if (branchId != null) tablesQ = tablesQ.Where(t => t.BranchId == branchId);

        var last7 = await GetSalesSeriesAsync(branchId, today.AddDays(-6), today);
        var topItems = await GetTopItemsAsync(branchId, today.AddDays(-7), DateTime.UtcNow);
        // SQLite can't SUM decimal in SQL — materialize then aggregate in memory
        var branchOrders = branchId == null ? await _db.Orders.AsNoTracking().Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= today.AddDays(-30)).Select(o => new { o.BranchId, o.Total }).ToListAsync() : new();
        var branchSales = branchOrders.GroupBy(o => o.BranchId).Select(g => new { BranchId = g.Key, Revenue = g.Sum(x => x.Total), Orders = g.Count() }).ToList();
        var branchNames = await _db.Branches.ToDictionaryAsync(b => b.Id, b => b.Name);

        var revenue = todayCompleted.Sum(o => o.Total);
        return new DashboardStatsDto
        {
            TodayRevenue = revenue, TodayOrders = todayCompleted.Count,
            AvgTicket = todayCompleted.Count == 0 ? 0 : Math.Round(revenue / todayCompleted.Count, 0),
            OpenOrders = openOrders, LowStockCount = lowStock,
            TablesTotal = await tablesQ.CountAsync(), TablesOccupied = await tablesQ.CountAsync(t => t.Status == TableStatus.Occupied),
            Last7Days = last7, TopItems = topItems,
            BranchSales = branchSales.Where(b => b.BranchId != null).Select(b => new BranchSalesDto { BranchId = b.BranchId!.Value, Name = branchNames.GetValueOrDefault(b.BranchId.Value, "?"), Revenue = b.Revenue, Orders = b.Orders }).ToList(),
            RunningEvent = await _events.GetRunningAsync(branchId)
        };
    }

    public async Task<List<SalesPointDto>> GetSalesSeriesAsync(Guid? branchId, DateTime from, DateTime to)
    {
        var q = _db.Orders.AsNoTracking().Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= from && o.CompletedAt < to.AddDays(1));
        if (branchId != null) q = q.Where(o => o.BranchId == branchId);
        var rows = await q.Select(o => new { o.CompletedAt, o.Total }).ToListAsync();
        var days = (to.Date - from.Date).Days + 1;
        var result = new List<SalesPointDto>();
        for (int i = 0; i < days; i++)
        {
            var d = from.Date.AddDays(i);
            var dayRows = rows.Where(r => r.CompletedAt!.Value.Date == d).ToList();
            result.Add(new SalesPointDto { Date = d, Revenue = dayRows.Sum(r => r.Total), Orders = dayRows.Count });
        }
        return result;
    }

    public async Task<List<TopItemDto>> GetTopItemsAsync(Guid? branchId, DateTime from, DateTime to, int take = 8)
    {
        var q = _db.OrderItems.AsNoTracking().Where(i => i.Order!.Status == OrderStatus.Completed && i.Order.CompletedAt >= from && i.Order.CompletedAt <= to);
        if (branchId != null) q = q.Where(i => i.BranchId == branchId);
        // in-memory grouping (SQLite decimal limitation)
        var rows = await q.Select(i => new { i.ItemNameSnapshot, i.Quantity, i.UnitPrice }).ToListAsync();
        return rows.GroupBy(i => i.ItemNameSnapshot)
            .Select(g => new TopItemDto { Name = g.Key, Qty = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.Quantity * x.UnitPrice) })
            .OrderByDescending(g => g.Qty).Take(take).ToList();
    }

    public async Task<XReportDto> GetXReportAsync(Guid? branchId)
    {
        var today = DateTime.UtcNow.Date;
        var q = _db.Orders.AsNoTracking().Include(o => o.Payments).Where(o => o.CompletedAt >= today);
        if (branchId != null) q = q.Where(o => o.BranchId == branchId);
        var orders = await q.ToListAsync();
        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        var branchName = branchId != null ? (await _db.Branches.FindAsync(branchId))?.Name ?? "?" : "All branches";
        return new XReportDto
        {
            BranchName = branchName, GeneratedAt = DateTime.UtcNow,
            OrdersCount = completed.Count, Guests = completed.Sum(o => o.ItemsCount),
            GrossSales = completed.Sum(o => o.SubTotal), Discounts = completed.Sum(o => o.Discount), Vat = completed.Sum(o => o.Tax),
            NetSales = completed.Sum(o => o.SubTotal - o.Discount),
            CashCollected = completed.Where(o => o.Payments.Any(p => p.Gateway == PaymentGatewayType.Cash)).Sum(o => o.Total),
            OnlineCollected = completed.Where(o => o.Payments.Any(p => (int)p.Gateway >= 10)).Sum(o => o.Total),
            CardCollected = completed.Where(o => o.Payments.Any(p => p.Gateway == PaymentGatewayType.CardPresent)).Sum(o => o.Total),
            Voids = orders.Count(o => o.Status == OrderStatus.Cancelled)
        };
    }
}
