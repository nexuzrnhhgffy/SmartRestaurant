using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;

namespace SmartRestaurant.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RestaurantEvent> Events => Set<RestaurantEvent>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<DiningTable> Tables => Set<DiningTable>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentGatewayConfig> GatewayConfigs => Set<PaymentGatewayConfig>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    public DbSet<PrinterConfig> Printers => Set<PrinterConfig>();
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // global soft-delete filter
        mb.Model.GetEntityTypes().Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType))
            .ToList().ForEach(t =>
            {
                var p = Expression.Parameter(t.ClrType, "e");
                var body = Expression.Equal(Expression.Property(p, nameof(BaseEntity.IsDeleted)), Expression.Constant(false));
                mb.Entity(t.ClrType).HasQueryFilter(Expression.Lambda(body, p));
            });

        mb.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.UserName).HasMaxLength(64);
            e.Property(x => x.FullName).HasMaxLength(120);
        });
        mb.Entity<RefreshToken>(e => e.HasIndex(x => x.Token).IsUnique());
        mb.Entity<Category>(e => e.Property(x => x.Name).HasMaxLength(80));
        mb.Entity<MenuItem>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasOne(x => x.Category).WithMany(c => c.Items).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Recipe).WithOne(r => r.MenuItem).HasForeignKey(r => r.MenuItemId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<RecipeItem>(e =>
        {
            e.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<Ingredient>(e => { e.Property(x => x.CostPerUnit).HasPrecision(18, 2); e.Property(x => x.Stock).HasPrecision(18, 3); e.Property(x => x.MinStock).HasPrecision(18, 3); });
        mb.Entity<StockMovement>(e =>
        {
            e.HasOne(x => x.Ingredient).WithMany(i => i.Movements).HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
        });
        mb.Entity<DiningTable>(e => e.HasIndex(x => new { x.BranchId, x.Number }).IsUnique());
        mb.Entity<Order>(e =>
        {
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.Property(x => x.OrderNumber).HasMaxLength(16);
            e.HasMany(x => x.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Payments).WithOne(p => p.Order).HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.SubTotal).HasPrecision(18, 2);
            e.Property(x => x.Discount).HasPrecision(18, 2);
            e.Property(x => x.Tax).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
        });
        mb.Entity<OrderItem>(e =>
        {
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.HasOne(x => x.MenuItem).WithMany(m => m.OrderItems).HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<Payment>(e => { e.Property(x => x.Amount).HasPrecision(18, 2); e.Property(x => x.GatewayResponse).HasMaxLength(4000); });
        mb.Entity<PrintJob>(e => e.Property(x => x.Content).HasMaxLength(200_000));
        mb.Entity<JournalEntry>(e =>
        {
            e.HasMany(x => x.Lines).WithOne(l => l.JournalEntry).HasForeignKey(l => l.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.EntryNumber).IsUnique();
        });
        mb.Entity<JournalLine>(e => { e.Property(x => x.Debit).HasPrecision(18, 2); e.Property(x => x.Credit).HasPrecision(18, 2); e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict); });
        mb.Entity<Expense>(e => e.Property(x => x.Amount).HasPrecision(18, 2));
        mb.Entity<Account>(e => e.HasIndex(x => x.Code).IsUnique());
        mb.Entity<AppSetting>(e => e.HasIndex(x => x.Key).IsUnique());
        mb.Entity<Notification>(e => e.HasIndex(x => new { x.IsRead, x.CreatedAt }));
        mb.Entity<Reservation>(e => e.HasIndex(x => x.ReservedFor));
        mb.Entity<RestaurantEvent>(e => e.HasIndex(x => new { x.StartAt, x.EndAt }));
    }
}
