using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRestaurant.Application.Dtos;
using SmartRestaurant.Application.Interfaces;
using SmartRestaurant.Domain.Entities.Identity;
using System.Security.Claims;

namespace SmartRestaurant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req) => Ok(await _auth.LoginAsync(req));

    [HttpPost("pin-login")]
    [Authorize]
    public async Task<IActionResult> PinLogin([FromBody] PinLoginRequest req) => Ok(await _auth.PinLoginAsync(req));

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req) => Ok(await _auth.RegisterAsync(req));

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _auth.GetUserAsync(id);
        return user == null ? NotFound() : Ok(user);
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthService _auth;
    public UsersController(IAuthService auth) => _auth = auth;

    [HttpGet("staff")]
    public async Task<IActionResult> GetStaff() => Ok(await _auth.GetStaffAsync());

    [HttpPost("staff")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> SaveStaff([FromBody] UserDto dto, [FromQuery] string? password)
        => Ok(await _auth.SaveStaffAsync(dto, password));

    [HttpPost("staff/{id}/toggle")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> Toggle(int id) => (await _auth.ToggleStaffActiveAsync(id)) ? Ok() : NotFound();
}

[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly IMenuService _menu;
    public MenuController(IMenuService menu) => _menu = menu;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetMenu([FromQuery] int? categoryId, [FromQuery] bool includeUnavailable = true, [FromQuery] string? search = null)
        => Ok(await _menu.GetMenuAsync(categoryId, includeUnavailable, search));

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetItem(int id)
    {
        var item = await _menu.GetItemAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories([FromQuery] bool includeInactive = false) => Ok(await _menu.GetCategoriesAsync(includeInactive));

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> SaveItem([FromBody] SaveMenuItemDto dto) => Ok(await _menu.SaveItemAsync(dto));

    [HttpPost("categories")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> SaveCategory([FromBody] SaveCategoryDto dto) => Ok(await _menu.SaveCategoryAsync(dto));

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> DeleteItem(int id) => (await _menu.DeleteItemAsync(id)) ? Ok() : NotFound();

    [HttpDelete("categories/{id}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> DeleteCategory(int id) => (await _menu.DeleteCategoryAsync(id)) ? Ok() : BadRequest("Category has items.");

    [HttpPost("{id}/availability")]
    [Authorize(Roles = "SuperAdmin,Manager,Chef")]
    public async Task<IActionResult> SetAvailability(int id, [FromBody] MenuItemAvailabilityDto dto) => (await _menu.SetAvailabilityAsync(id, dto.IsAvailable)) ? Ok() : NotFound();
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TablesController : ControllerBase
{
    private readonly ITableService _tables;
    public TablesController(ITableService tables) => _tables = tables;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _tables.GetTablesAsync());

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> Save([FromBody] SaveTableDto dto) => Ok(await _tables.SaveTableAsync(dto));

    [HttpPost("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTableStatusDto dto) => (await _tables.UpdateStatusAsync(id, dto.Status)) ? Ok() : NotFound();

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> Delete(int id) => (await _tables.DeleteTableAsync(id)) ? Ok() : NotFound();
}

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _res;
    public ReservationsController(IReservationService res) => _res = res;

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? date, [FromQuery] int? status) => Ok(await _res.GetAllAsync(date, status));

    [HttpPost]
    [AllowAnonymous] // website booking form
    public async Task<IActionResult> Save([FromBody] SaveReservationDto dto) => Ok(await _res.SaveAsync(dto));

    [HttpPost("{id}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto) => (await _res.UpdateStatusAsync(id, dto.Status)) ? Ok() : NotFound();

    [HttpPost("{id}/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel(int id) => (await _res.CancelAsync(id)) ? Ok() : NotFound();
}

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    public OrdersController(IOrderService orders) => _orders = orders;

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] int? type, [FromQuery] int? status, [FromQuery] DateTime? date, [FromQuery] string? search)
        => Ok(await _orders.GetAllAsync(type, status, date, search));

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetById(int id)
    {
        var o = await _orders.GetByIdAsync(id);
        return o == null ? NotFound() : Ok(o);
    }

    [HttpGet("stats")]
    [Authorize]
    public async Task<IActionResult> GetStats() => Ok(await _orders.GetStatsAsync());

    [HttpPost]
    [AllowAnonymous] // guests order from the public website; staff POS sends the JWT
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
        var order = await _orders.CreateAsync(dto, userId);
        return Created($"/api/orders/{order.Id}", order);
    }

    [HttpPost("{id}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto) => Ok(await _orders.UpdateStatusAsync(id, dto.Status));

    [HttpPost("items/{itemId}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateItemStatus(int itemId, [FromBody] UpdateOrderItemStatusDto dto) => Ok(await _orders.UpdateItemStatusAsync(itemId, dto.Status));

    [HttpPost("{id}/items")]
    [Authorize]
    public async Task<IActionResult> AddItem(int id, [FromBody] CreateOrderItemDto dto) => (await _orders.AddItemAsync(id, dto)) ? Ok() : NotFound();

    [HttpDelete("items/{itemId}")]
    [Authorize]
    public async Task<IActionResult> RemoveItem(int itemId) => (await _orders.RemoveItemAsync(itemId)) ? Ok() : NotFound();

    [HttpPost("{id}/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel(int id, [FromQuery] string? reason) => (await _orders.CancelAsync(id, reason)) ? Ok() : Conflict("Order cannot be cancelled.");
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _pay;
    public PaymentsController(IPaymentService pay) => _pay = pay;

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager,Cashier")]
    public async Task<IActionResult> Pay([FromBody] PaymentDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return (await _pay.PayAsync(dto, userId)) ? Ok() : BadRequest("Payment failed.");
    }

    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetForOrder(int orderId) => Ok(await _pay.GetOrderPaymentsAsync(orderId));
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KitchenController : ControllerBase
{
    private readonly IKitchenService _kds;
    public KitchenController(IKitchenService kds) => _kds = kds;

    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue() => Ok(await _kds.GetQueueAsync());
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inv;
    public InventoryController(IInventoryService inv) => _inv = inv;

    [HttpGet("ingredients")]
    public async Task<IActionResult> GetIngredients([FromQuery] bool lowOnly = false) => Ok(await _inv.GetIngredientsAsync(lowOnly));

    [HttpPost("ingredients")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> SaveIngredient([FromBody] SaveIngredientDto dto) => Ok(await _inv.SaveIngredientAsync(dto));

    [HttpDelete("ingredients/{id}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> DeleteIngredient(int id) => (await _inv.DeleteIngredientAsync(id)) ? Ok() : NotFound();

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers() => Ok(await _inv.GetSuppliersAsync());

    [HttpPost("suppliers")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> SaveSupplier([FromBody] SaveSupplierDto dto) => Ok(await _inv.SaveSupplierAsync(dto));

    [HttpGet("purchase-orders")]
    public async Task<IActionResult> GetPOs([FromQuery] int? status) => Ok(await _inv.GetPurchaseOrdersAsync(status));

    [HttpPost("purchase-orders")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> SavePO([FromBody] SavePurchaseOrderDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _inv.SavePurchaseOrderAsync(dto, userId));
    }

    [HttpPost("purchase-orders/{id}/receive")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> Receive(int id) => (await _inv.ReceivePurchaseOrderAsync(id)) ? Ok() : Conflict("Already received.");

    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements([FromQuery] int? ingredientId) => Ok(await _inv.GetMovementsAsync(ingredientId));

    [HttpPost("adjust")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> Adjust([FromBody] StockAdjustDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return (await _inv.AdjustStockAsync(dto, userId)) ? Ok() : NotFound();
    }
}

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventService _events;
    public EventsController(IEventService events) => _events = events;

    [HttpGet("packages")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPackages() => Ok(await _events.GetPackagesAsync());

    [HttpPost("packages")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> SavePackage([FromBody] SaveEventPackageDto dto) => Ok(await _events.SavePackageAsync(dto));

    [HttpDelete("packages/{id}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> DeletePackage(int id) => (await _events.DeletePackageAsync(id)) ? Ok() : NotFound();

    [HttpGet("bookings")]
    [Authorize]
    public async Task<IActionResult> GetBookings([FromQuery] int? status) => Ok(await _events.GetBookingsAsync(status));

    [HttpPost("bookings")]
    [AllowAnonymous] // public inquiry form
    public async Task<IActionResult> SaveBooking([FromBody] SaveEventBookingDto dto) => Ok(await _events.SaveBookingAsync(dto));

    [HttpPost("bookings/{id}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto) => (await _events.UpdateBookingStatusAsync(id, dto.Status)) ? Ok() : NotFound();

    [HttpPost("bookings/{id}/deposit")]
    [Authorize(Roles = "SuperAdmin,Manager,Accountant")]
    public async Task<IActionResult> SetDeposit(int id, [FromQuery] decimal amount, [FromQuery] bool paid) => (await _events.SetDepositAsync(id, amount, paid)) ? Ok() : NotFound();

    [HttpPost("bookings/{id}/tasks")]
    [Authorize]
    public async Task<IActionResult> AddTask(int id, [FromBody] SaveEventTaskDto dto) => Ok(await _events.AddTaskAsync(id, dto));

    [HttpPost("tasks/{taskId}/toggle")]
    [Authorize]
    public async Task<IActionResult> ToggleTask(int taskId) => (await _events.ToggleTaskAsync(taskId)) ? Ok() : NotFound();
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Manager,Accountant")]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _acc;
    public AccountingController(IAccountingService acc) => _acc = acc;

    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses([FromQuery] DateTime? from, [FromQuery] DateTime? to) => Ok(await _acc.GetExpensesAsync(from, to));

    [HttpPost("expenses")]
    public async Task<IActionResult> SaveExpense([FromBody] SaveExpenseDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _acc.SaveExpenseAsync(dto, userId));
    }

    [HttpDelete("expenses/{id}")]
    public async Task<IActionResult> DeleteExpense(int id) => (await _acc.DeleteExpenseAsync(id)) ? Ok() : NotFound();

    [HttpGet("payroll")]
    public async Task<IActionResult> GetPayrolls([FromQuery] string? period) => Ok(await _acc.GetPayrollsAsync(period));

    [HttpPost("payroll")]
    public async Task<IActionResult> SavePayroll([FromBody] SavePayrollDto dto) => Ok(await _acc.SavePayrollAsync(dto));

    [HttpPost("payroll/{id}/pay")]
    public async Task<IActionResult> PayPayroll(int id) => (await _acc.MarkPayrollPaidAsync(id)) ? Ok() : NotFound();

    [HttpGet("profit-loss")]
    public async Task<IActionResult> ProfitLoss([FromQuery] DateTime from, [FromQuery] DateTime to) => Ok(await _acc.GetProfitLossAsync(from, to));
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StaffController : ControllerBase
{
    private readonly IStaffService _staff;
    public StaffController(IStaffService staff) => _staff = staff;

    [HttpGet("shifts")]
    public async Task<IActionResult> GetShifts([FromQuery] DateTime? from, [FromQuery] DateTime? to) => Ok(await _staff.GetShiftsAsync(from, to));

    [HttpPost("shifts")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> SaveShift([FromBody] SaveShiftDto dto) => Ok(await _staff.SaveShiftAsync(dto));

    [HttpDelete("shifts/{id}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> DeleteShift(int id) => (await _staff.DeleteShiftAsync(id)) ? Ok() : NotFound();

    [HttpGet("attendance")]
    public async Task<IActionResult> GetAttendance([FromQuery] DateTime? date) => Ok(await _staff.GetAttendanceAsync(date));

    [HttpPost("attendance/check-in/{employeeId}")]
    public async Task<IActionResult> CheckIn(int employeeId) => Ok(await _staff.CheckInAsync(employeeId));

    [HttpPost("attendance/check-out/{employeeId}")]
    public async Task<IActionResult> CheckOut(int employeeId)
    {
        try { return Ok(await _staff.CheckOutAsync(employeeId)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dash;
    public DashboardController(IDashboardService dash) => _dash = dash;

    [HttpGet("kpis")]
    [Authorize]
    public async Task<IActionResult> Kpis() => Ok(await _dash.GetKpisAsync());

    [HttpGet("sales")]
    [Authorize]
    public async Task<IActionResult> Sales([FromQuery] int days = 7) => Ok(await _dash.GetSalesChartsAsync(days));
}

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _cust;
    public CustomersController(ICustomerService cust) => _cust = cust;

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] string? search) => Ok(await _cust.GetAllAsync(search));

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Save([FromBody] SaveCustomerDto dto) => Ok(await _cust.SaveAsync(dto));

    [HttpPost("{id}/points")]
    [Authorize(Roles = "SuperAdmin,Manager,Cashier")]
    public async Task<IActionResult> AddPoints(int id, [FromQuery] int points) => (await _cust.AddPointsAsync(id, points)) ? Ok() : NotFound();

    [HttpGet("by-phone/{phone}")]
    [Authorize]
    public async Task<IActionResult> GetByPhone(string phone)
    {
        var c = await _cust.GetByPhoneAsync(phone);
        return c == null ? NotFound() : Ok(c);
    }
}

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviews;
    public ReviewsController(IReviewService reviews) => _reviews = reviews;

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] int? status) => Ok(await _reviews.GetAllAsync(status));

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic()
    {
        var all = await _reviews.GetAllAsync(2); // approved
        return Ok(all.Take(12));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Submit([FromBody] SaveReviewDto dto) => Ok(await _reviews.SubmitAsync(dto));

    [HttpPost("{id}/moderate")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> Moderate(int id, [FromBody] UpdateOrderStatusDto dto) => (await _reviews.ModerateAsync(id, dto.Status)) ? Ok() : NotFound();

    [HttpPost("{id}/reply")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyReviewDto dto) => (await _reviews.ReplyAsync(id, dto.Reply)) ? Ok() : NotFound();
}

[ApiController]
[Route("api/[controller]")]
public class CouponsController : ControllerBase
{
    private readonly ICouponService _coupons;
    public CouponsController(ICouponService coupons) => _coupons = coupons;

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll() => Ok(await _coupons.GetAllAsync());

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> Save([FromBody] SaveCouponDto dto) => Ok(await _coupons.SaveAsync(dto));

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    public async Task<IActionResult> Delete(int id) => (await _coupons.DeleteAsync(id)) ? Ok() : NotFound();

    [HttpPost("validate")]
    [AllowAnonymous]
    public async Task<IActionResult> Validate([FromBody] ValidateCouponDto dto) => Ok(await _coupons.ValidateAsync(dto));
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notif;
    public NotificationsController(INotificationService notif) => _notif = notif;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? role, [FromQuery] bool unreadOnly = false)
        => Ok(await _notif.GetAllAsync(role, unreadOnly));

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(int id) => (await _notif.MarkReadAsync(id)) ? Ok() : NotFound();

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead([FromQuery] int? role) => (await _notif.MarkAllReadAsync(role)) ? Ok() : Ok();
}

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;
    public SettingsController(ISettingsService settings) => _settings = settings;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get() => Ok(await _settings.GetAsync());

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Save([FromBody] SettingsDto dto) => Ok(await _settings.SaveAsync(dto));
}
