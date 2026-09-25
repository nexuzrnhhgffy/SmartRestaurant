using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Dtos;
using SmartRestaurant.Application.Interfaces;
using SmartRestaurant.Domain.Entities.Menu;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

public class MenuService : IMenuService
{
    private readonly AppDbContext _db;
    public MenuService(AppDbContext db) => _db = db;

    public async Task<List<MenuItemDto>> GetMenuAsync(int? categoryId = null, bool includeUnavailable = true, string? search = null)
    {
        var q = _db.MenuItems.Include(m => m.Category).Include(m => m.ModifierGroups).ThenInclude(g => g.Options).AsNoTracking();
        if (!includeUnavailable) q = q.Where(m => m.IsAvailable);
        if (categoryId.HasValue) q = q.Where(m => m.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(m => m.Name.ToLower().Contains(search.ToLower()) || (m.Description != null && m.Description.ToLower().Contains(search.ToLower())));
        var list = await q.OrderBy(m => m.Category.SortOrder).ThenBy(m => m.Name).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<MenuItemDto?> GetItemAsync(int id)
    {
        var m = await _db.MenuItems.Include(x => x.Category).Include(x => x.ModifierGroups).ThenInclude(g => g.Options).FirstOrDefaultAsync(x => x.Id == id);
        return m == null ? null : ToDto(m);
    }

    public async Task<MenuItemDto> SaveItemAsync(SaveMenuItemDto dto)
    {
        MenuItem item;
        if (dto.Id is null or 0)
        {
            item = new MenuItem();
            _db.MenuItems.Add(item);
        }
        else
        {
            item = await _db.MenuItems.Include(m => m.ModifierGroups).FirstAsync(m => m.Id == dto.Id);
        }
        item.CategoryId = dto.CategoryId; item.Name = dto.Name; item.Description = dto.Description;
        item.Price = dto.Price; item.Cost = dto.Cost; item.ImageUrl = dto.ImageUrl;
        item.IsAvailable = dto.IsAvailable; item.IsFeatured = dto.IsFeatured;
        item.IsVegetarian = dto.IsVegetarian; item.IsSpicy = dto.IsSpicy; item.IsGlutenFree = dto.IsGlutenFree;
        item.PrepTimeMinutes = dto.PrepTimeMinutes; item.Calories = dto.Calories; item.Allergens = dto.Allergens;
        item.Station = (PrepStation)dto.Station; item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        item = await _db.MenuItems.Include(m => m.Category).Include(m => m.ModifierGroups).ThenInclude(g => g.Options).FirstAsync(x => x.Id == item.Id);
        return ToDto(item);
    }

    public async Task<bool> DeleteItemAsync(int id)
    {
        var m = await _db.MenuItems.FindAsync(id);
        if (m == null) return false;
        _db.MenuItems.Remove(m); // hard remove keeps orders via NameSnapshot
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetAvailabilityAsync(int id, bool available)
    {
        var m = await _db.MenuItems.FindAsync(id);
        if (m == null) return false;
        m.IsAvailable = available; m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<List<Category>> GetCategoriesAsync(bool includeInactive = false) =>
        _db.Categories.AsNoTracking().Where(c => includeInactive || c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();

    public async Task<Category> SaveCategoryAsync(SaveCategoryDto dto)
    {
        Category cat;
        if (dto.Id is null or 0)
        {
            cat = new Category();
            _db.Categories.Add(cat);
        }
        else cat = await _db.Categories.FirstAsync(c => c.Id == dto.Id);
        cat.Name = dto.Name; cat.Description = dto.Description; cat.SortOrder = dto.SortOrder;
        cat.IsActive = dto.IsActive; cat.ImageUrl = dto.ImageUrl; cat.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return cat;
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        var hasItems = await _db.MenuItems.AnyAsync(m => m.CategoryId == id);
        if (hasItems) return false;
        var c = await _db.Categories.FindAsync(id);
        if (c == null) return false;
        _db.Categories.Remove(c);
        await _db.SaveChangesAsync();
        return true;
    }

    internal static MenuItemDto ToDto(MenuItem m) => new(
        m.Id, m.CategoryId, m.Category?.Name ?? "-", m.Name, m.Description, m.Price, m.ImageUrl,
        m.IsAvailable, m.IsFeatured, m.IsVegetarian, m.IsSpicy, m.IsGlutenFree, m.PrepTimeMinutes,
        m.Calories, m.Allergens, (int)m.Station, m.TimesOrdered,
        m.ModifierGroups.Select(g => new ModifierGroupDto(g.Id, g.Name, g.MinSelection, g.MaxSelection,
            g.Options.Select(o => new ModifierOptionDto(o.Id, o.Name, o.ExtraPrice)).ToList())).ToList());
}
