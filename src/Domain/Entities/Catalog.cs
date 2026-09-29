using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Emoji { get; set; } = "🍽️";
    public int SortOrder { get; set; }
    public Station Station { get; set; } = Station.HotKitchen;
    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}

public class MenuItem : BaseEntity, IBranchScoped
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Price { get; set; }                       // Toman
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int PrepMinutes { get; set; } = 15;
    public string? ImageUrl { get; set; }
    public bool IsSpicy { get; set; }
    public bool IsVegetarian { get; set; }
    public int Calories { get; set; }
    public Guid? BranchId { get; set; }                      // null = all branches
    public ICollection<RecipeItem> Recipe { get; set; } = new List<RecipeItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

/// <summary>Bill-of-materials line: how much of an ingredient one unit of the dish consumes.</summary>
public class RecipeItem : BaseEntity
{
    public Guid MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }
    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public decimal Quantity { get; set; }                    // in ingredient unit
}
