using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Dtos;
using SmartRestaurant.Application.Interfaces;
using SmartRestaurant.Domain.Entities.Accounting;
using SmartRestaurant.Domain.Entities.Identity;
using SmartRestaurant.Domain.Entities.Orders;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

public class AccountingService : IAccountingService
{
    private readonly AppDbContext _db;
    public AccountingService(AppDbContext db) => _db = db;

    public async Task<List<ExpenseDto>> GetExpensesAsync(DateTime? from = null, DateTime? to = null)
    {
        var q = _db.Expenses.Include(e => e.CreatedBy).AsNoTracking();
        if (from.HasValue) q = q.Where(e => e.Date >= from);
        if (to.HasValue) q = q.Where(e => e.Date <= to);
        var list = await q.OrderByDescending(e => e.Date).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ExpenseDto> SaveExpenseAsync(SaveExpenseDto dto, int userId)
    {
        Expense e;
        if (dto.Id is null or 0) { e = new Expense { CreatedById = userId }; _db.Expenses.Add(e); }
        else e = await _db.Expenses.FirstAsync(x => x.Id == dto.Id);
        e.Category = (ExpenseCategory)dto.Category; e.Amount = dto.Amount; e.Description = dto.Description;
        e.Date = dto.Date; e.PaidTo = dto.PaidTo; e.ReceiptRef = dto.ReceiptRef; e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        e = await _db.Expenses.Include(x => x.CreatedBy).FirstAsync(x => x.Id == e.Id);
        return ToDto(e);
    }

    public async Task<bool> DeleteExpenseAsync(int id)
    {
        var e = await _db.Expenses.FindAsync(id);
        if (e == null) return false;
        _db.Expenses.Remove(e);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<PayrollDto>> GetPayrollsAsync(string? period = null)
    {
        var q = _db.Payrolls.Include(p => p.Employee).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(period)) q = q.Where(p => p.Period == period);
        var list = await q.OrderByDescending(p => p.Period).ThenBy(p => p.EmployeeId).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<PayrollDto> SavePayrollAsync(SavePayrollDto dto)
    {
        Payroll p;
        if (dto.Id is null or 0)
        {
            var emp = await _db.Users.FindAsync(dto.EmployeeId);
            var hourly = (emp?.BaseSalary ?? 2000m) / 176m; // ~22 working days × 8h
            var otPay = Math.Round(hourly * 1.5m * (decimal)dto.OvertimeHours, 2);
            p = new Payroll
            {
                EmployeeId = dto.EmployeeId, Period = dto.Period,
                BaseSalary = emp?.BaseSalary ?? 0, OvertimeHours = dto.OvertimeHours,
                OvertimePay = otPay, Bonus = dto.Bonus, Deductions = dto.Deductions
            };
            _db.Payrolls.Add(p);
        }
        else
        {
            p = await _db.Payrolls.FirstAsync(x => x.Id == dto.Id);
            p.OvertimeHours = dto.OvertimeHours; p.Bonus = dto.Bonus; p.Deductions = dto.Deductions;
        }
        p.NetPay = p.BaseSalary + p.OvertimePay + p.Bonus - p.Deductions;
        await _db.SaveChangesAsync();
        p = await _db.Payrolls.Include(x => x.Employee).FirstAsync(x => x.Id == p.Id);
        return ToDto(p);
    }

    public async Task<bool> MarkPayrollPaidAsync(int id)
    {
        var p = await _db.Payrolls.FindAsync(id);
        if (p == null) return false;
        p.Status = PayrollStatus.Paid; p.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ProfitLossDto> GetProfitLossAsync(DateTime from, DateTime to)
    {
        var orders = await _db.Orders.Include(o => o.Items)
            .Where(o => o.CreatedAt >= from && o.CreatedAt <= to && o.Status != OrderStatus.Cancelled).AsNoTracking().ToListAsync();
        var expenses = await _db.Expenses.Where(e => e.Date >= from && e.Date <= to).AsNoTracking().ToListAsync();
        var payrolls = await _db.Payrolls.Where(p => p.Status == PayrollStatus.Paid).AsNoTracking().ToListAsync();

        var revenue = orders.Sum(o => o.Total - o.TaxAmount); // net revenue before tax
        var foodCost = orders.SelectMany(o => o.Items).Sum(i => i.CostSnapshot);
        var expenseTotal = expenses.Sum(e => e.Amount);
        var payrollTotal = payrolls.Sum(p => p.NetPay);
        var net = revenue - foodCost - expenseTotal - payrollTotal;

        var breakdown = expenses.GroupBy(e => e.Category.ToString())
            .Select(g => new CategoryBreakdownDto(g.Key, g.Sum(x => x.Amount), revenue > 0 ? Math.Round((double)(g.Sum(x => x.Amount) / revenue) * 100, 1) : 0))
            .OrderByDescending(x => x.Amount).ToList();

        var trend = orders.GroupBy(o => o.CreatedAt.Date)
            .Select(g => new DailyPointDto(g.Key.ToString("MMM dd"), g.Sum(x => x.Total)))
            .OrderBy(x => x.Label).ToList();

        return new ProfitLossDto(revenue, foodCost, expenseTotal, payrollTotal, net,
            revenue > 0 ? Math.Round(net / revenue * 100, 1) : 0, breakdown, trend);
    }

    internal static ExpenseDto ToDto(Expense e) => new(e.Id, (int)e.Category, e.Category.ToString(), e.Amount,
        e.Description, e.Date, e.PaidTo, e.ReceiptRef, e.CreatedBy?.FullName);

    internal static PayrollDto ToDto(Payroll p) => new(p.Id, p.EmployeeId, p.Employee?.FullName ?? "-", p.Period,
        p.BaseSalary, p.OvertimeHours, p.OvertimePay, p.Bonus, p.Deductions, p.NetPay, (int)p.Status, p.Status.ToString(), p.PaidAt);
}

public class StaffService : IStaffService
{
    private readonly AppDbContext _db;
    public StaffService(AppDbContext db) => _db = db;

    public async Task<List<ShiftDto>> GetShiftsAsync(DateTime? from = null, DateTime? to = null)
    {
        var q = _db.Shifts.Include(s => s.Employee).AsNoTracking();
        if (from.HasValue) q = q.Where(s => s.StartTime >= from);
        if (to.HasValue) q = q.Where(s => s.StartTime <= to);
        var list = await q.OrderBy(s => s.StartTime).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ShiftDto> SaveShiftAsync(SaveShiftDto dto)
    {
        Shift s;
        if (dto.Id is null or 0) { s = new Shift(); _db.Shifts.Add(s); }
        else s = await _db.Shifts.FirstAsync(x => x.Id == dto.Id);
        s.EmployeeId = dto.EmployeeId; s.StartTime = dto.StartTime; s.EndTime = dto.EndTime; s.Notes = dto.Notes;
        await _db.SaveChangesAsync();
        s = await _db.Shifts.Include(x => x.Employee).FirstAsync(x => x.Id == s.Id);
        return ToDto(s);
    }

    public async Task<bool> DeleteShiftAsync(int id)
    {
        var s = await _db.Shifts.FindAsync(id);
        if (s == null) return false;
        _db.Shifts.Remove(s);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<AttendanceDto>> GetAttendanceAsync(DateTime? date = null)
    {
        var q = _db.Attendances.Include(a => a.Employee).AsNoTracking();
        if (date.HasValue) q = q.Where(a => a.CheckIn!.Value.Date == date.Value.Date);
        var list = await q.OrderByDescending(a => a.CheckIn).Take(100).ToListAsync();
        return list.Select(a => new AttendanceDto(a.Id, a.EmployeeId, a.Employee?.FullName ?? "-", a.CheckIn, a.CheckOut, a.HoursWorked, a.Notes)).ToList();
    }

    public async Task<AttendanceDto> CheckInAsync(int employeeId)
    {
        var today = DateTime.UtcNow.Date;
        var existing = await _db.Attendances.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.CheckIn!.Value.Date == today && a.CheckOut == null);
        if (existing != null) return new AttendanceDto(existing.Id, existing.EmployeeId, (await _db.Users.FindAsync(employeeId))?.FullName ?? "-", existing.CheckIn, existing.CheckOut, existing.HoursWorked, existing.Notes);
        var a = new Attendance { EmployeeId = employeeId, CheckIn = DateTime.UtcNow };
        _db.Attendances.Add(a);
        await _db.SaveChangesAsync();
        return new AttendanceDto(a.Id, a.EmployeeId, (await _db.Users.FindAsync(employeeId))?.FullName ?? "-", a.CheckIn, a.CheckOut, a.HoursWorked, a.Notes);
    }

    public async Task<AttendanceDto> CheckOutAsync(int employeeId)
    {
        var today = DateTime.UtcNow.Date;
        var a = await _db.Attendances.Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.CheckIn!.Value.Date == today && x.CheckOut == null)
            ?? throw new InvalidOperationException("No open attendance record found for today.");
        a.CheckOut = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new AttendanceDto(a.Id, a.EmployeeId, a.Employee?.FullName ?? "-", a.CheckIn, a.CheckOut, a.HoursWorked, a.Notes);
    }

    internal static ShiftDto ToDto(Shift s) => new(s.Id, s.EmployeeId, s.Employee?.FullName ?? "-", s.StartTime, s.EndTime, (int)s.Status, s.Status.ToString(), s.Notes);
}
