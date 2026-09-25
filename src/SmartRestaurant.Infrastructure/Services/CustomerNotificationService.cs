using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Dtos;
using SmartRestaurant.Application.Interfaces;
using SmartRestaurant.Domain.Entities.Customers;
using SmartRestaurant.Domain.Entities.Events;
using SmartRestaurant.Domain.Entities.Identity;
using SmartRestaurant.Domain.Entities.Notifications;
using SmartRestaurant.Domain.Entities.Orders;
using SmartRestaurant.Domain.Entities.Reservations;
using SmartRestaurant.Domain.Entities.Settings;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardKpiDto> GetKpisAsync()
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var paidOrders = await _db.Orders.Include(o => o.Items).AsNoTracking()
            .Where(o => o.Status != OrderStatus.Cancelled).ToListAsync();
        var todaysOrders = paidOrders.Where(o => o.CreatedAt.Date == today).ToList();

        var revenueToday = todaysOrders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.Total);
        var revenueMonth = paidOrders.Where(o => o.CreatedAt >= monthStart && o.Status == OrderStatus.Completed).Sum(o => o.Total);
        var active = todaysOrders.Count(o => o.Status is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready);
        var completedToday = todaysOrders.Where(o => o.Status == OrderStatus.Completed).ToList();
        var avg = completedToday.Count > 0 ? Math.Round(revenueToday / completedToday.Count, 2) : 0;

        var foodCost = paidOrders.Where(o => o.CreatedAt >= monthStart).SelectMany(o => o.Items).Sum(i => i.CostSnapshot);
        var monthRevenue = paidOrders.Where(o => o.CreatedAt >= monthStart && o.Status == OrderStatus.Completed).Sum(o => o.Total);
        var fcPct = monthRevenue > 0 ? Math.Round(foodCost / monthRevenue * 100, 1) : 0;

        var lowStock = await _db.Ingredients.CountAsync(i => i.StockQty <= i.MinStock);
        var eventInquiries = await _db.EventBookings.CountAsync(b => b.Status == EventStatus.Inquiry || b.Status == EventStatus.Quoted);
        var reservationsToday = await _db.Reservations.CountAsync(r => r.DateTime.Date == today && r.Status != ReservationStatus.Cancelled);
        var customers = await _db.Customers.CountAsync();
        var avgRating = await _db.Reviews.Where(r => r.Status == ReviewStatus.Approved).AnyAsync()
            ? Math.Round(await _db.Reviews.Where(r => r.Status == ReviewStatus.Approved).AverageAsync(r => (double)r.Rating), 1)
            : 0;
        var pendingReviews = await _db.Reviews.CountAsync(r => r.Status == ReviewStatus.Pending);

        return new DashboardKpiDto(revenueToday, todaysOrders.Count, avg, active, reservationsToday, lowStock,
            eventInquiries, revenueMonth, customers, fcPct, avgRating, pendingReviews);
    }

    public async Task<SalesChartDto> GetSalesChartsAsync(int days = 7)
    {
        var from = DateTime.UtcNow.Date.AddDays(-days);
        var paid = await _db.Orders.Include(o => o.Items).ThenInclude(i => i.MenuItem)
            .Where(o => o.CreatedAt >= from && o.Status == OrderStatus.Completed).AsNoTracking().ToListAsync();

        var daily = paid.GroupBy(o => o.CreatedAt.Date)
            .Select(g => new DailyPointDto(g.Key.ToString("MMM dd"), g.Sum(o => o.Total)))
            .OrderBy(x => x.Label).ToList();

        var hourly = paid.GroupBy(o => o.CreatedAt.Hour)
            .Select(g => new SalesByHourDto($"{g.Key:00}:00", g.Sum(o => o.Total), g.Count()))
            .OrderBy(x => x.Hour).ToList();

        var topItems = paid.SelectMany(o => o.Items).GroupBy(i => i.NameSnapshot)
            .Select(g => new TopItemDto(g.Key, g.Sum(i => i.Quantity), g.Sum(i => i.LineTotal)))
            .OrderByDescending(t => t.Revenue).Take(8).ToList();

        var orderTypes = paid.GroupBy(o => o.Type.ToString())
            .Select(g => new CategoryBreakdownDto(g.Key, g.Count(), Math.Round((double)g.Count() / Math.Max(1, paid.Count) * 100, 1)))
            .OrderByDescending(x => x.Amount).ToList();

        var paymentMethods = (await _db.Payments.Where(p => p.PaidAt >= from).Select(p => new { p.Method, p.Amount }).AsNoTracking().ToListAsync())
            .GroupBy(p => p.Method.ToString())
            .Select(g => new CategoryBreakdownDto(g.Key, g.Sum(p => p.Amount), 0)).ToList();
        var pmTotal = paymentMethods.Sum(p => p.Amount);
        if (pmTotal > 0)
            paymentMethods = paymentMethods.Select(p => p with { Percent = Math.Round((double)(p.Amount / pmTotal) * 100, 1) }).ToList();

        return new SalesChartDto(daily, hourly, topItems, orderTypes, paymentMethods);
    }
}

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _db;
    public CustomerService(AppDbContext db) => _db = db;

    public async Task<List<CustomerDto>> GetAllAsync(string? search = null)
    {
        var q = _db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.FullName.Contains(search) || c.Phone.Contains(search));
        var list = await q.ToListAsync();
        return list.OrderByDescending(c => c.TotalSpent) // in-memory: SQLite cannot ORDER BY decimal
            .Select(ToDto).ToList();
    }

    public async Task<CustomerDto> SaveAsync(SaveCustomerDto dto)
    {
        Customer c;
        if (dto.Id is null or 0) { c = new Customer(); _db.Customers.Add(c); }
        else c = await _db.Customers.FirstAsync(x => x.Id == dto.Id);
        c.FullName = dto.FullName; c.Phone = dto.Phone; c.Email = dto.Email; c.Address = dto.Address; c.BirthDate = dto.BirthDate;
        await _db.SaveChangesAsync();
        return ToDto(c);
    }

    public async Task<bool> AddPointsAsync(int id, int points)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return false;
        c.LoyaltyPoints = Math.Max(0, c.LoyaltyPoints + points);
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<CustomerDto?> GetByPhoneAsync(string phone) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Phone == phone).ContinueWith(t => t.Result == null ? null : ToDto(t.Result));

    internal static CustomerDto ToDto(Customer c) => new(c.Id, c.FullName, c.Phone, c.Email, c.Address,
        c.LoyaltyPoints, c.Tier.ToString(), c.TotalSpent, c.OrderCount, c.BirthDate, c.CreatedAt);
}

public class ReviewService : IReviewService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notify;
    public ReviewService(AppDbContext db, INotificationService notify) { _db = db; _notify = notify; }

    public async Task<List<ReviewDto>> GetAllAsync(int? status = null)
    {
        var q = _db.Reviews.AsNoTracking();
        if (status.HasValue) q = q.Where(r => (int)r.Status == status);
        var list = await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ReviewDto> SubmitAsync(SaveReviewDto dto)
    {
        var r = new Review
        {
            CustomerName = dto.CustomerName, Rating = Math.Clamp(dto.Rating, 1, 5), Comment = dto.Comment,
            Type = (ReviewType)dto.Type, OrderId = dto.OrderId, Status = ReviewStatus.Pending
        };
        _db.Reviews.Add(r);
        await _db.SaveChangesAsync();
        await _notify.PushAsync("New review submitted", $"{dto.CustomerName} rated {dto.Rating}★", 5, AppRole.Manager, "/admin#reviews");
        return ToDto(r);
    }

    public async Task<bool> ModerateAsync(int id, int status)
    {
        var r = await _db.Reviews.FindAsync(id);
        if (r == null) return false;
        r.Status = (ReviewStatus)status;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReplyAsync(int id, string reply)
    {
        var r = await _db.Reviews.FindAsync(id);
        if (r == null) return false;
        r.Reply = reply;
        await _db.SaveChangesAsync();
        return true;
    }

    internal static ReviewDto ToDto(Review r) => new(r.Id, r.CustomerId, r.CustomerName, r.Rating, r.Comment,
        (int)r.Type, r.Type.ToString(), (int)r.Status, r.Status.ToString(), r.Reply, r.CreatedAt);
}

public class CouponService : ICouponService
{
    private readonly AppDbContext _db;
    public CouponService(AppDbContext db) => _db = db;

    public async Task<List<CouponDto>> GetAllAsync()
    {
        var list = await _db.Coupons.OrderByDescending(c => c.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<CouponDto> SaveAsync(SaveCouponDto dto)
    {
        Coupon c;
        if (dto.Id is null or 0) { c = new Coupon(); _db.Coupons.Add(c); }
        else c = await _db.Coupons.FirstAsync(x => x.Id == dto.Id);
        c.Code = dto.Code.ToUpperInvariant().Trim(); c.Description = dto.Description; c.Type = (CouponType)dto.Type;
        c.Value = dto.Value; c.MinOrderAmount = dto.MinOrderAmount; c.MaxUses = dto.MaxUses;
        c.ValidFrom = dto.ValidFrom; c.ValidTo = dto.ValidTo; c.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return ToDto(c);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var c = await _db.Coupons.FindAsync(id);
        if (c == null) return false;
        _db.Coupons.Remove(c);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<CouponValidationResult> ValidateAsync(ValidateCouponDto dto)
    {
        var c = await _db.Coupons.FirstOrDefaultAsync(x => x.Code == dto.Code.Trim().ToUpper());
        if (c == null) return new CouponValidationResult(false, "Coupon not found.", 0);
        if (!c.IsValid) return new CouponValidationResult(false, "Coupon expired or usage limit reached.", 0);
        if (dto.OrderAmount < c.MinOrderAmount) return new CouponValidationResult(false, $"Minimum order for this coupon is ${c.MinOrderAmount:F2}.", 0);
        var discount = c.Type == CouponType.Percent ? Math.Round(dto.OrderAmount * c.Value / 100m, 2) : c.Value;
        return new CouponValidationResult(true, $"Coupon applied — you save ${discount:F2}!", discount);
    }

    internal static CouponDto ToDto(Coupon c) => new(c.Id, c.Code, c.Description, (int)c.Type, c.Value, c.MinOrderAmount,
        c.MaxUses, c.UsedCount, c.ValidFrom, c.ValidTo, c.IsActive, c.IsValid);
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    public NotificationService(AppDbContext db) => _db = db;

    public async Task<List<NotificationDto>> GetAllAsync(int? role = null, bool unreadOnly = false)
    {
        var q = _db.Notifications.AsNoTracking();
        if (role.HasValue)
            q = q.Where(n => n.TargetRole == null || (int)(n.TargetRole ?? AppRole.Manager) == role);
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        var list = await q.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task PushAsync(string title, string message, int type, AppRole? targetRole = null, string? link = null)
    {
        _db.Notifications.Add(new Notification
        {
            Title = title, Message = message, Type = (NotificationType)type, TargetRole = targetRole, Link = link
        });
        await _db.SaveChangesAsync();
    }

    public async Task<bool> MarkReadAsync(int id)
    {
        var n = await _db.Notifications.FindAsync(id);
        if (n == null) return false;
        n.IsRead = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkAllReadAsync(int? role = null)
    {
        var q = _db.Notifications.Where(n => !n.IsRead);
        if (role.HasValue) q = q.Where(n => n.TargetRole == null || (int)(n.TargetRole ?? AppRole.Manager) == role);
        await q.ForEachAsync(n => n.IsRead = true);
        await _db.SaveChangesAsync();
        return true;
    }

    internal static NotificationDto ToDto(Notification n) => new(n.Id, n.Title, n.Message, (int)n.Type, n.Type.ToString(), n.IsRead, n.Link, n.CreatedAt);
}

public class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;
    public SettingsService(AppDbContext db) => _db = db;

    public async Task<SettingsDto> GetAsync()
    {
        var s = await _db.Settings.AsNoTracking().FirstAsync();
        return ToDto(s);
    }

    public async Task<SettingsDto> SaveAsync(SettingsDto dto)
    {
        var s = await _db.Settings.FirstAsync();
        s.Name = dto.Name; s.Tagline = dto.Tagline; s.Address = dto.Address; s.Phone = dto.Phone; s.Email = dto.Email;
        s.Currency = dto.Currency; s.TaxRate = dto.TaxRate; s.ServiceChargeRate = dto.ServiceChargeRate;
        s.DeliveryFee = dto.DeliveryFee; s.OpeningHours = dto.OpeningHours;
        s.LoyaltyPointsPerDollar = dto.LoyaltyPointsPerDollar; s.FacebookUrl = dto.FacebookUrl; s.InstagramUrl = dto.InstagramUrl;
        s.OnlineOrderingEnabled = dto.OnlineOrderingEnabled; s.ReservationsEnabled = dto.ReservationsEnabled;
        s.MaxGuestsPerReservation = dto.MaxGuestsPerReservation; s.EstimatedDeliveryMinutes = dto.EstimatedDeliveryMinutes;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(s);
    }

    internal static SettingsDto ToDto(RestaurantSetting s) => new(s.Id, s.Name, s.Tagline, s.Address, s.Phone, s.Email,
        s.Currency, s.TaxRate, s.ServiceChargeRate, s.DeliveryFee, s.OpeningHours, s.LoyaltyPointsPerDollar,
        s.FacebookUrl, s.InstagramUrl, s.OnlineOrderingEnabled, s.ReservationsEnabled, s.MaxGuestsPerReservation, s.EstimatedDeliveryMinutes);
}
