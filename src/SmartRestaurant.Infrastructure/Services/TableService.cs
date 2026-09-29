using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Dtos;
using SmartRestaurant.Application.Interfaces;
using SmartRestaurant.Domain.Entities.Orders;
using SmartRestaurant.Domain.Entities.Reservations;
using SmartRestaurant.Domain.Entities.Tables;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

public class TableService : ITableService
{
    private readonly AppDbContext _db;
    public TableService(AppDbContext db) => _db = db;

    public Task<List<TableDto>> GetTablesAsync() =>
        _db.Tables.AsNoTracking().OrderBy(t => t.Number).ToListAsync().ContinueWith(t =>
            t.Result.Select(ToDto).ToList());

    public async Task<TableDto> SaveTableAsync(SaveTableDto dto)
    {
        DiningTable table;
        if (dto.Id is null or 0)
        {
            table = new DiningTable { QrToken = $"T{dto.Number}-{Guid.NewGuid().ToString("N")[..8]}" };
            _db.Tables.Add(table);
        }
        else table = await _db.Tables.FirstAsync(t => t.Id == dto.Id);
        table.Number = dto.Number; table.Section = (TableSection)dto.Section;
        table.Capacity = dto.Capacity; table.Status = (TableStatus)dto.Status; table.Notes = dto.Notes;
        await _db.SaveChangesAsync();
        return ToDto(table);
    }

    public async Task<bool> UpdateStatusAsync(int id, int status)
    {
        var t = await _db.Tables.FindAsync(id);
        if (t == null) return false;
        t.Status = (TableStatus)status;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteTableAsync(int id)
    {
        var t = await _db.Tables.FindAsync(id);
        if (t == null) return false;
        _db.Tables.Remove(t);
        await _db.SaveChangesAsync();
        return true;
    }

    internal static TableDto ToDto(DiningTable t) => new(t.Id, t.Number, (int)t.Section, t.Section.ToString(), t.Capacity,
        (int)t.Status, t.Status.ToString(), t.QrToken, t.Notes);
}

public class ReservationService : IReservationService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notify;
    public ReservationService(AppDbContext db, INotificationService notify) { _db = db; _notify = notify; }

    public async Task<List<ReservationDto>> GetAllAsync(DateTime? date = null, int? status = null)
    {
        var q = _db.Reservations.Include(r => r.Table).AsNoTracking();
        if (date.HasValue) q = q.Where(r => r.DateTime.Date == date.Value.Date);
        if (status.HasValue) q = q.Where(r => (int)r.Status == status);
        var list = await q.OrderByDescending(r => r.DateTime).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ReservationDto> SaveAsync(SaveReservationDto dto)
    {
        Reservation r;
        bool isNew = dto.Id is null or 0;
        if (isNew)
        {
            r = new Reservation();
            _db.Reservations.Add(r);
        }
        else r = await _db.Reservations.FirstAsync(x => x.Id == dto.Id);

        r.CustomerName = dto.CustomerName; r.Phone = dto.Phone; r.Email = dto.Email;
        r.PartySize = dto.PartySize; r.TableId = dto.TableId; r.DateTime = dto.DateTime;
        r.Occasion = dto.Occasion; r.Notes = dto.Notes; r.Source = dto.Source ?? "Front Desk";
        r.Status = isNew ? ReservationStatus.Pending : r.Status;

        // hold the table
        if (r.TableId.HasValue && isNew)
        {
            var t = await _db.Tables.FindAsync(r.TableId);
            if (t != null) t.Status = TableStatus.Reserved;
        }

        await _db.SaveChangesAsync();
        if (isNew)
            await _notify.PushAsync("New reservation", $"{dto.CustomerName} — {dto.PartySize} guests at {dto.DateTime:HH:mm}", 3, Domain.Entities.Identity.AppRole.Manager, "/admin#reservations");
        return ToDto(r);
    }

    public async Task<bool> UpdateStatusAsync(int id, int status)
    {
        var r = await _db.Reservations.Include(x => x.Table).FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return false;
        r.Status = (ReservationStatus)status;
        if (r.TableId.HasValue)
        {
            var t = await _db.Tables.FindAsync(r.TableId);
            if (t != null)
            {
                t.Status = (ReservationStatus)status switch
                {
                    ReservationStatus.Seated => TableStatus.Occupied,
                    ReservationStatus.Cancelled or ReservationStatus.NoShow => TableStatus.Available,
                    _ => t.Status
                };
            }
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<bool> CancelAsync(int id) => UpdateStatusAsync(id, (int)ReservationStatus.Cancelled);

    internal static ReservationDto ToDto(Reservation r) => new(r.Id, r.CustomerName, r.Phone, r.Email, r.PartySize,
        r.TableId, r.DateTime, (int)r.Status, r.Status.ToString(), r.Occasion, r.Notes, r.Source);
}
