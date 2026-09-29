using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _acc;
    public AccountingController(IAccountingService acc) => _acc = acc;

    [HttpGet("expenses")]
    public async Task<IActionResult> Expenses([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _acc.GetExpensesAsync(branchId, from, to));

    [HttpPost("expenses")]
    public async Task<IActionResult> AddExpense(ExpenseUpsertDto dto) => Ok(await _acc.AddExpenseAsync(dto));

    [HttpGet("journals")]
    public async Task<IActionResult> Journals([FromQuery] Guid? branchId, [FromQuery] int take = 100) => Ok(await _acc.GetJournalsAsync(branchId, take));

    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _acc.GetTrialBalanceAsync(branchId, from, to));

    [HttpGet("profit-loss")]
    public async Task<IActionResult> ProfitLoss([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _acc.GetProfitLossAsync(branchId, from, to));
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    public ReportsController(IReportService reports) => _reports = reports;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] Guid? branchId) => Ok(await _reports.GetDashboardStatsAsync(branchId));

    [HttpGet("sales-series")]
    public async Task<IActionResult> SalesSeries([FromQuery] Guid? branchId, DateTime from, DateTime to)
        => Ok(await _reports.GetSalesSeriesAsync(branchId, from, to));

    [HttpGet("top-items")]
    public async Task<IActionResult> TopItems([FromQuery] Guid? branchId, DateTime from, DateTime to, int take = 8)
        => Ok(await _reports.GetTopItemsAsync(branchId, from, to, take));

    [HttpGet("x-report")]
    public async Task<IActionResult> XReport([FromQuery] Guid? branchId) => Ok(await _reports.GetXReportAsync(branchId));
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly IEventService _events;
    public EventsController(IEventService events) => _events = events;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? branchId) => Ok(await _events.GetAllAsync(branchId));

    [HttpGet("running")]
    public async Task<IActionResult> Running([FromQuery] Guid? branchId)
    {
        var e = await _events.GetRunningAsync(branchId);
        return e == null ? NoContent() : Ok(e);
    }

    [HttpPost]
    public async Task<IActionResult> Upsert(EventDto dto) => Ok(await _events.UpsertAsync(dto));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Deactivate(Guid id) { await _events.DeactivateAsync(id); return NoContent(); }
}
