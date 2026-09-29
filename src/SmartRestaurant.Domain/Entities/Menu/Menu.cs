using SmartRestaurant.Domain.Entities.Common;

namespace SmartRestaurant.Domain.Entities.Menu;

public enum PrepStation { Bar = 1, Cold = 2, Grill = 3, Fry = 4, Pastry = 5, HotLine = 6 }

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}

public class MenuItem : BaseEntity
{
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; } // food cost for margin analytics
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsFeatured { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsSpicy { get; set; }
    public bool IsGlutenFree { get; set; }
    public int PrepTimeMinutes { get; set; } = 15;
    public int? Calories { get; set; }
    public string? Allergens { get; set; } // comma separated: nuts,dairy
    public PrepStation Station { get; set; } = PrepStation.HotLine;
    public int TimesOrdered { get; set; } // popularity counter
    public decimal? RatingAvg { get; set; }
    public ICollection<ModifierGroup> ModifierGroups { get; set; } = new List<ModifierGroup>();
}

/// <summary>Customization group attached to menu items, e.g. "Choose size", "Extras".</summary>
public class ModifierGroup : BaseEntity
{
    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinSelection { get; set; }
    public int MaxSelection { get; set; } = 3;
    public ICollection<ModifierOption> Options { get; set; } = new List<ModifierOption>();
}

public class ModifierOption : BaseEntity
{
    public int ModifierGroupId { get; set; }
    public ModifierGroup? ModifierGroup { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal ExtraPrice { get; set; }
}
