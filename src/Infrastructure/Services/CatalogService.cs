using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

/// <summary>Shared branch-scope resolution used by all services.</summary>
public static class Scope
{
    /// <summary>SuperAdmin may pass any branch (falls back to first); staff are locked to their own.</summary>
    public static async Task<Guid> ResolveAsync(AppDbContext db, IUserContext user, Guid? requested)
    {
        if (user.IsSuperAdmin)
        {
            if (requested is not null) return requested.Value;
            var first = await db.Branches.OrderBy(b => b.CreatedAt).FirstOrDefaultAsync(b => b.IsActive)
                ?? throw AppException.BadRequest("No active branch exists.");
            return first.Id;
        }
        if (user.BranchId is not null) return user.BranchId.Value;
        // customers ordering online: allow explicit branch
        if (requested is not null) return requested.Value;
        var fb = await db.Branches.OrderBy(b => b.CreatedAt).FirstOrDefaultAsync(b => b.IsActive)
            ?? throw AppException.BadRequest("No active branch exists.");
        return fb.Id;
    }
}

// ═══════════════════════════ Branches ═══════════════════════════
public class BranchService : IBranchService
{
    private readonly AppDbContext _db;
    public BranchService(AppDbContext db) => _db = db;

    public async Task<List<BranchDto>> GetAllAsync() =>
        await _db.Branches.OrderBy(b => b.Name).Select(b => new BranchDto { Id = b.Id, Name = b.Name, City = b.City, Address = b.Address, Phone = b.Phone, IsActive = b.IsActive }).ToListAsync();

    public async Task<List<BranchDto>> GetForUserAsync(IUserContext user)
    {
        if (user.IsSuperAdmin) return await GetAllAsync();
        if (user.BranchId is null) return new List<BranchDto>();
        return await _db.Branches.Where(b => b.Id == user.BranchId)
            .Select(b => new BranchDto { Id = b.Id, Name = b.Name, City = b.City, Address = b.Address, Phone = b.Phone, IsActive = b.IsActive }).ToListAsync();
    }

    public async Task<BranchDto> UpsertAsync(BranchUpsertDto dto)
    {
        Branch b;
        if (dto.Id is null) { b = new Branch(); _db.Branches.Add(b); }
        else b = await _db.Branches.FindAsync(dto.Id) ?? throw AppException.NotFound("Branch");
        b.Name = dto.Name; b.City = dto.City; b.Address = dto.Address; b.Phone = dto.Phone; b.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return new BranchDto { Id = b.Id, Name = b.Name, City = b.City, Address = b.Address, Phone = b.Phone, IsActive = b.IsActive };
    }
}

// ═══════════════════════════ Menu ═══════════════════════════
public class MenuService : IMenuService
{
    private readonly AppDbContext _db;
    public MenuService(AppDbContext db) => _db = db;

    public async Task<List<CategoryDto>> GetCategoriesAsync() =>
        await _db.Categories.OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto { Id = c.Id, Name = c.Name, Emoji = c.Emoji, SortOrder = c.SortOrder, Station = c.Station, ItemsCount = c.Items.Count })
            .ToListAsync();

    public async Task<List<MenuItemDto>> GetItemsAsync(Guid? branchId, bool onlyAvailable = false)
    {
        var q = _db.MenuItems.AsNoTracking().Include(m => m.Category).Include(m => m.Recipe).ThenInclude(r => r.Ingredient).AsQueryable();
        if (onlyAvailable) q = q.Where(m => m.IsAvailable);
        var items = await q.OrderBy(m => m.Category!.SortOrder).ThenBy(m => m.Name).ToListAsync();
        return items.Where(m => m.BranchId == null || m.BranchId == branchId).Select(ToDto).ToList();
    }

    public async Task<MenuItemDto> UpsertAsync(MenuItemUpsertDto dto)
    {
        MenuItem m;
        if (dto.Id is null) { m = new MenuItem(); _db.MenuItems.Add(m); }
        else m = await _db.MenuItems.Include(x => x.Recipe).FirstOrDefaultAsync(x => x.Id == dto.Id) ?? throw AppException.NotFound("Menu item");

        m.Name = dto.Name; m.Description = dto.Description; m.Price = dto.Price; m.CategoryId = dto.CategoryId;
        m.IsAvailable = dto.IsAvailable; m.PrepMinutes = dto.PrepMinutes; m.IsSpicy = dto.IsSpicy; m.IsVegetarian = dto.IsVegetarian;
        m.Calories = dto.Calories; m.BranchId = dto.BranchId; m.UpdatedAt = DateTime.UtcNow;

        m.Recipe.Clear();
        foreach (var line in dto.Recipe.Where(r => r.Quantity > 0))
        {
            if (!await _db.Ingredients.AnyAsync(i => i.Id == line.IngredientId)) continue;
            m.Recipe.Add(new RecipeItem { IngredientId = line.IngredientId, Quantity = line.Quantity });
        }
        await _db.SaveChangesAsync();
        m = await _db.MenuItems.Include(x => x.Category).Include(x => x.Recipe).ThenInclude(r => r.Ingredient).FirstAsync(x => x.Id == m.Id);
        return ToDto(m);
    }

    public async Task SetAvailabilityAsync(Guid id, bool available)
    {
        var m = await _db.MenuItems.FindAsync(id) ?? throw AppException.NotFound("Menu item");
        m.IsAvailable = available; m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var m = await _db.MenuItems.FindAsync(id) ?? throw AppException.NotFound("Menu item");
        m.IsDeleted = true; m.IsAvailable = false; m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    internal static MenuItemDto ToDto(MenuItem m) => new()
    {
        Id = m.Id, Name = m.Name, Description = m.Description, Price = m.Price, CategoryId = m.CategoryId,
        CategoryName = m.Category?.Name, CategoryEmoji = m.Category?.Emoji, IsAvailable = m.IsAvailable,
        PrepMinutes = m.PrepMinutes, IsSpicy = m.IsSpicy, IsVegetarian = m.IsVegetarian, Calories = m.Calories, BranchId = m.BranchId,
        Recipe = m.Recipe.Select(r => new RecipeItemDto { IngredientId = r.IngredientId, IngredientName = r.Ingredient?.Name ?? "?", Quantity = r.Quantity, Unit = r.Ingredient?.Unit ?? "" }).ToList()
    };
}

// ═══════════════════════════ Events ═══════════════════════════
public class EventService : IEventService
{
    private readonly AppDbContext _db;
    public EventService(AppDbContext db) => _db = db;

    public async Task<List<EventDto>> GetAllAsync(Guid? branchId = null)
    {
        var q = _db.Events.AsNoTracking().OrderByDescending(e => e.StartAt).AsQueryable();
        if (branchId != null) q = q.Where(e => e.BranchId == null || e.BranchId == branchId);
        var list = await q.ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<EventDto> UpsertAsync(EventDto dto)
    {
        RestaurantEvent e;
        if (dto.Id == Guid.Empty) { e = new RestaurantEvent(); _db.Events.Add(e); }
        else e = await _db.Events.FindAsync(dto.Id) ?? throw AppException.NotFound("Event");
        e.Title = dto.Title; e.Description = dto.Description; e.StartAt = dto.StartAt; e.EndAt = dto.EndAt;
        e.DiscountPercent = dto.DiscountPercent; e.BannerEmoji = dto.BannerEmoji; e.IsActive = dto.IsActive; e.BranchId = dto.BranchId;
        await _db.SaveChangesAsync();
        return ToDto(e);
    }

    public Task<EventDto?> GetRunningAsync(Guid? branchId)
    {
        var now = DateTime.UtcNow;
        var q = _db.Events.AsNoTracking().Where(e => e.IsActive && e.StartAt <= now && e.EndAt >= now && (e.BranchId == null || e.BranchId == branchId));
        return q.OrderBy(e => e.BranchId == null).FirstOrDefaultAsync().ContinueWith(t => t.Result == null ? null : ToDto(t.Result));
    }

    public async Task DeactivateAsync(Guid id)
    {
        var e = await _db.Events.FindAsync(id) ?? throw AppException.NotFound("Event");
        e.IsActive = false; e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    internal static EventDto ToDto(RestaurantEvent e)
    {
        var now = DateTime.UtcNow;
        return new EventDto { Id = e.Id, Title = e.Title, Description = e.Description, StartAt = e.StartAt, EndAt = e.EndAt, DiscountPercent = e.DiscountPercent, BannerEmoji = e.BannerEmoji, IsActive = e.IsActive, BranchId = e.BranchId, IsRunning = e.IsActive && e.StartAt <= now && e.EndAt >= now };
    }
}
