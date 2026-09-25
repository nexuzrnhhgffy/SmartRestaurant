using SmartRestaurant.Domain.Entities.Common;

namespace SmartRestaurant.Domain.Entities.Tables;

public enum TableStatus { Available = 1, Reserved = 2, Occupied = 3, Dirty = 4, OutOfService = 5 }
public enum TableSection { MainHall = 1, Terrace = 2, VIP = 3, Private = 4, Bar = 5 }

public class DiningTable : BaseEntity
{
    public int Number { get; set; }
    public TableSection Section { get; set; } = TableSection.MainHall;
    public int Capacity { get; set; } = 4;
    public TableStatus Status { get; set; } = TableStatus.Available;
    public string QrToken { get; set; } = string.Empty; // for self-order QR per table
    public string? Notes { get; set; }
}
