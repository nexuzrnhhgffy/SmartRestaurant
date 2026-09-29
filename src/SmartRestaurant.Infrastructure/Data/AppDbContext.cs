using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Domain.Entities.Accounting;
using SmartRestaurant.Domain.Entities.Common;
using SmartRestaurant.Domain.Entities.Customers;
using SmartRestaurant.Domain.Entities.Events;
using SmartRestaurant.Domain.Entities.Identity;
using SmartRestaurant.Domain.Entities.Inventory;
using SmartRestaurant.Domain.Entities.Menu;
using SmartRestaurant.Domain.Entities.Notifications;
using SmartRestaurant.Domain.Entities.Orders;
using SmartRestaurant.Domain.Entities.Reservations;
using SmartRestaurant.Domain.Entities.Settings;
using SmartRestaurant.Domain.Entities.Tables;
using System.Linq.Expressions;

namespace SmartRestaurant.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<ModifierGroup> ModifierGroups => Set<ModifierGroup>();
    public DbSet<ModifierOption> ModifierOptions => Set<ModifierOption>();
    public DbSet<DiningTable> Tables => Set<DiningTable>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<EventPackage> EventPackages => Set<EventPackage>();
    public DbSet<EventBooking> EventBookings => Set<EventBooking>();
    public DbSet<EventTask> EventTasks => Set<EventTask>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RestaurantSetting> Settings => Set<RestaurantSetting>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // Global soft-delete filter for every BaseEntity
        foreach (var entityType in mb.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var param = Expression.Parameter(entityType.ClrType, "e");
                var body = Expression.Equal(
                    Expression.Property(param, nameof(BaseEntity.IsDeleted)),
                    Expression.Constant(false));
                mb.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, param));
            }
        }

        mb.Entity<AppUser>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.PinCode);
            e.Property(u => u.FullName).IsRequired().HasMaxLength(120);
            e.Property(u => u.Email).IsRequired().HasMaxLength(160);
            e.Property(u => u.BaseSalary).HasPrecision(18, 2);
        });

        mb.Entity<Category>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(80);
            e.HasIndex(c => c.SortOrder);
        });

        mb.Entity<MenuItem>(e =>
        {
            e.Property(m => m.Name).IsRequired().HasMaxLength(120);
            e.Property(m => m.Price).HasPrecision(18, 2);
            e.Property(m => m.Cost).HasPrecision(18, 2);
            e.Property(m => m.Description).HasMaxLength(600);
            e.HasIndex(m => m.CategoryId);
        });

        mb.Entity<ModifierOption>(e => e.Property(o => o.Name).HasMaxLength(80));

        mb.Entity<DiningTable>(e =>
        {
            e.HasIndex(t => t.Number).IsUnique();
            e.HasIndex(t => t.QrToken).IsUnique();
        });

        mb.Entity<Order>(e =>
        {
            e.Property(o => o.OrderNumber).IsRequired().HasMaxLength(24);
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.HasIndex(o => o.Status);
            e.HasIndex(o => o.CreatedAt);
            e.Property(o => o.Subtotal).HasPrecision(18, 2);
            e.Property(o => o.DiscountAmount).HasPrecision(18, 2);
            e.Property(o => o.TaxAmount).HasPrecision(18, 2);
            e.Property(o => o.ServiceCharge).HasPrecision(18, 2);
            e.Property(o => o.DeliveryFee).HasPrecision(18, 2);
            e.Property(o => o.Tip).HasPrecision(18, 2);
            e.Property(o => o.Total).HasPrecision(18, 2);
        });

        mb.Entity<OrderItem>(e =>
        {
            e.Property(i => i.UnitPrice).HasPrecision(18, 2);
            e.Property(i => i.LineTotal).HasPrecision(18, 2);
            e.Property(i => i.CostSnapshot).HasPrecision(18, 2);
            e.HasIndex(i => i.Status);
        });

        mb.Entity<Payment>(e =>
        {
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.Property(p => p.ChangeGiven).HasPrecision(18, 2);
        });

        mb.Entity<Ingredient>(e =>
        {
            e.Property(i => i.CostPerUnit).HasPrecision(18, 4);
            e.Property(i => i.StockQty).HasPrecision(18, 3);
            e.Property(i => i.MinStock).HasPrecision(18, 3);
        });

        mb.Entity<PurchaseOrder>(e => e.Property(p => p.Total).HasPrecision(18, 2));
        mb.Entity<PurchaseOrderItem>(e =>
        {
            e.Property(i => i.Quantity).HasPrecision(18, 3);
            e.Property(i => i.UnitCost).HasPrecision(18, 4);
        });
        mb.Entity<StockMovement>(e =>
        {
            e.Property(m => m.Quantity).HasPrecision(18, 3);
            e.Property(m => m.StockAfter).HasPrecision(18, 3);
        });

        mb.Entity<EventPackage>(e => e.Property(p => p.PricePerPerson).HasPrecision(18, 2));
        mb.Entity<EventBooking>(e =>
        {
            e.Property(b => b.QuoteAmount).HasPrecision(18, 2);
            e.Property(b => b.DepositAmount).HasPrecision(18, 2);
        });

        mb.Entity<Expense>(e => e.Property(x => x.Amount).HasPrecision(18, 2));
        mb.Entity<Payroll>(e =>
        {
            e.Property(p => p.BaseSalary).HasPrecision(18, 2);
            e.Property(p => p.OvertimePay).HasPrecision(18, 2);
            e.Property(p => p.Bonus).HasPrecision(18, 2);
            e.Property(p => p.Deductions).HasPrecision(18, 2);
            e.Property(p => p.NetPay).HasPrecision(18, 2);
            e.HasIndex(p => p.Period);
        });

        mb.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Phone);
            e.Property(c => c.TotalSpent).HasPrecision(18, 2);
        });

        mb.Entity<Coupon>(e =>
        {
            e.HasIndex(c => c.Code).IsUnique();
            e.Property(c => c.Value).HasPrecision(18, 2);
            e.Property(c => c.MinOrderAmount).HasPrecision(18, 2);
        });

        mb.Entity<Reservation>(e => e.HasIndex(r => r.DateTime));
    }
}
