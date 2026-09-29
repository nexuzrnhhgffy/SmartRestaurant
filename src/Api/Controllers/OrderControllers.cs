using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    public OrdersController(IOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? branchId, [FromQuery] OrderStatus? status, [FromQuery] OrderType? type, [FromQuery] DateTime? from)
        => Ok(await _orders.ListAsync(branchId, status, type, from));

    [HttpGet("active")]
    public async Task<IActionResult> Active([FromQuery] Guid? branchId) => Ok(await _orders.ListActiveAsync(branchId));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id) => Ok(await _orders.GetAsync(id));

    [HttpPost]
    [Authorize(Policy = "")]
    public async Task<IActionResult> Create(OrderCreateDto dto)
    {
        var me = Me();
        // customers & guests can order online; staff create any type
        return Ok(await _orders.CreateAsync(dto, me));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, OrderStatusUpdateDto dto) => Ok(await _orders.UpdateStatusAsync(id, dto, Me()));

    [HttpPut("items/{itemId}/status")]
    public async Task<IActionResult> UpdateItemStatus(Guid itemId, [FromBody] KdsItemStatusUpdate dto) => Ok(await _orders.UpdateItemStatusAsync(itemId, dto.Status, Me()));

    private IUserContext Me() => (IUserContext)HttpContext.RequestServices.GetRequiredService<IUserContext>();
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class KdsController : ControllerBase
{
    private readonly IOrderService _orders;
    public KdsController(IOrderService orders) => _orders = orders;

    [HttpGet("tickets")]
    public async Task<IActionResult> Tickets([FromQuery] Guid? branchId, [FromQuery] Station? station)
        => Ok(await _orders.GetKdsTicketsAsync(branchId, station));
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TablesController : ControllerBase
{
    private readonly ITableService _tables;
    public TablesController(ITableService tables) => _tables = tables;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? branchId) => Ok(await _tables.GetTablesAsync(branchId));

    [HttpPost]
    public async Task<IActionResult> Upsert(TableDto dto) => Ok(await _tables.UpsertAsync(dto));

    [HttpPut("{id}/free")]
    public async Task<IActionResult> Free(Guid id) { await _tables.FreeAsync(id); return NoContent(); }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id) { await _tables.DeleteAsync(id); return NoContent(); }
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservations;
    public ReservationsController(IReservationService reservations) => _reservations = reservations;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? branchId, [FromQuery] DateTime? day) => Ok(await _reservations.GetAllAsync(branchId, day));

    [HttpPost]
    public async Task<IActionResult> Upsert(ReservationUpsertDto dto) => Ok(await _reservations.UpsertAsync(dto));

    [HttpPut("{id}/status/{status}")]
    public async Task<IActionResult> SetStatus(Guid id, ReservationStatus status) => Ok(await _reservations.SetStatusAsync(id, status));
}
