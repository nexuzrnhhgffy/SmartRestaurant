using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Dtos;
using SmartRestaurant.Application.Interfaces;
using SmartRestaurant.Domain.Entities.Events;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

public class EventService : IEventService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notify;
    public EventService(AppDbContext db, INotificationService notify) { _db = db; _notify = notify; }

    public async Task<List<EventPackageDto>> GetPackagesAsync()
    {
        var list = await _db.EventPackages.AsNoTracking().ToListAsync();
        return list.OrderBy(p => p.PricePerPerson) // in-memory: SQLite cannot ORDER BY decimal
            .Select(p => new EventPackageDto(p.Id, p.Name, p.Description, p.PricePerPerson, p.MinGuests, p.IncludedServices, p.ImageUrl, p.IsActive)).ToList();
    }

    public async Task<EventPackageDto> SavePackageAsync(SaveEventPackageDto dto)
    {
        EventPackage p;
        if (dto.Id is null or 0) { p = new EventPackage(); _db.EventPackages.Add(p); }
        else p = await _db.EventPackages.FirstAsync(x => x.Id == dto.Id);
        p.Name = dto.Name; p.Description = dto.Description; p.PricePerPerson = dto.PricePerPerson;
        p.MinGuests = dto.MinGuests; p.IncludedServices = dto.IncludedServices; p.ImageUrl = dto.ImageUrl; p.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return new EventPackageDto(p.Id, p.Name, p.Description, p.PricePerPerson, p.MinGuests, p.IncludedServices, p.ImageUrl, p.IsActive);
    }

    public async Task<bool> DeletePackageAsync(int id)
    {
        var p = await _db.EventPackages.FindAsync(id);
        if (p == null) return false;
        _db.EventPackages.Remove(p);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<EventBookingDto>> GetBookingsAsync(int? status = null)
    {
        var q = _db.EventBookings.Include(b => b.Package).Include(b => b.Tasks).ThenInclude(t => t.AssignedTo).AsNoTracking();
        if (status.HasValue) q = q.Where(b => (int)b.Status == status);
        var list = await q.OrderByDescending(b => b.EventDate).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<EventBookingDto> SaveBookingAsync(SaveEventBookingDto dto)
    {
        EventBooking b;
        var isNew = dto.Id is null or 0;
        if (isNew)
        {
            b = new EventBooking();
            _db.EventBookings.Add(b);
        }
        else b = await _db.EventBookings.Include(x => x.Tasks).FirstAsync(x => x.Id == dto.Id);

        b.CustomerName = dto.CustomerName; b.Phone = dto.Phone; b.Email = dto.Email; b.Type = (EventType)dto.Type;
        b.PackageId = dto.PackageId; b.EventDate = dto.EventDate; b.StartTime = dto.StartTime; b.EndTime = dto.EndTime;
        b.GuestCount = dto.GuestCount; b.Venue = (EventVenue)dto.Venue; b.Address = dto.Address; b.MenuNotes = dto.MenuNotes; b.Notes = dto.Notes;

        if (b.PackageId.HasValue)
        {
            var pkg = await _db.EventPackages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == b.PackageId);
            if (pkg != null) b.QuoteAmount = pkg.PricePerPerson * b.GuestCount;
        }

        await _db.SaveChangesAsync();
        if (isNew)
            await _notify.PushAsync("New event inquiry", $"{dto.CustomerName} — {((EventType)dto.Type)} for {dto.GuestCount} guests on {dto.EventDate:MMM dd}", 4, Domain.Entities.Identity.AppRole.Manager, "/admin#events");
        return ToDto(await _db.EventBookings.Include(x => x.Package).Include(x => x.Tasks).ThenInclude(t => t.AssignedTo).FirstAsync(x => x.Id == b.Id));
    }

    public async Task<bool> UpdateBookingStatusAsync(int id, int status)
    {
        var b = await _db.EventBookings.FindAsync(id);
        if (b == null) return false;
        b.Status = (EventStatus)status;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetDepositAsync(int id, decimal amount, bool paid)
    {
        var b = await _db.EventBookings.FindAsync(id);
        if (b == null) return false;
        b.DepositAmount = amount; b.DepositPaid = paid;
        if (paid && b.Status < EventStatus.Confirmed) b.Status = EventStatus.Confirmed;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<EventTaskDto> AddTaskAsync(int bookingId, SaveEventTaskDto dto)
    {
        var t = new EventTask { EventBookingId = bookingId, Title = dto.Title, AssignedToId = dto.AssignedToId, DueDate = dto.DueDate };
        _db.EventTasks.Add(t);
        await _db.SaveChangesAsync();
        var name = dto.AssignedToId.HasValue ? (await _db.Users.FindAsync(dto.AssignedToId))?.FullName : null;
        return new EventTaskDto(t.Id, t.Title, t.AssignedToId, name, t.DueDate, t.IsDone);
    }

    public async Task<bool> ToggleTaskAsync(int taskId)
    {
        var t = await _db.EventTasks.FindAsync(taskId);
        if (t == null) return false;
        t.IsDone = !t.IsDone;
        await _db.SaveChangesAsync();
        return true;
    }

    internal static EventBookingDto ToDto(EventBooking b) => new(b.Id, b.CustomerName, b.Phone, b.Email, (int)b.Type,
        b.Type.ToString(), b.PackageId, b.Package?.Name, b.EventDate, b.StartTime, b.EndTime, b.GuestCount,
        (int)b.Venue, b.Venue.ToString(), b.Address, b.MenuNotes, b.QuoteAmount, b.DepositAmount, b.DepositPaid,
        (int)b.Status, b.Status.ToString(), b.Notes,
        b.Tasks.OrderBy(t => t.IsDone).ThenBy(t => t.DueDate).Select(t => new EventTaskDto(t.Id, t.Title, t.AssignedToId, t.AssignedTo?.FullName, t.DueDate, t.IsDone)).ToList());
}
