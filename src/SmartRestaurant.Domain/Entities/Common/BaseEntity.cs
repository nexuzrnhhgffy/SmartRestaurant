namespace SmartRestaurant.Domain.Entities.Common;

/// <summary>Base class for all entities — audit fields included.</summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } // soft delete
}
