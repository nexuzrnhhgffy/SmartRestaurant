using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventory;
    public InventoryController(IInventoryService inventory) => _inventory = inventory;

    [HttpGet("ingredients")]
    public async Task<IActionResult> Ingredients([FromQuery] Guid? branchId) => Ok(await _inventory.GetIngredientsAsync(branchId));

    [HttpPost("ingredients")]
    public async Task<IActionResult> Upsert(IngredientUpsertDto dto) => Ok(await _inventory.UpsertAsync(dto));

    [HttpDelete("ingredients/{id}")]
    public async Task<IActionResult> Delete(Guid id) { await _inventory.DeleteAsync(id); return NoContent(); }

    [HttpPost("movements")]
    public async Task<IActionResult> Move(StockAdjustDto dto) { await _inventory.ReceiveAsync(dto, Me()); return NoContent(); }

    [HttpGet("movements")]
    public async Task<IActionResult> Movements([FromQuery] Guid? branchId, [FromQuery] Guid? ingredientId)
        => Ok(await _inventory.GetMovementsAsync(branchId, ingredientId));

    [HttpGet("low-stock-count")]
    public async Task<IActionResult> LowStock() => Ok(new { count = await _inventory.LowStockCountAsync(null) });

    private IUserContext Me() => (IUserContext)HttpContext.RequestServices.GetRequiredService<IUserContext>();
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;
    public PaymentsController(IPaymentService payments) => _payments = payments;

    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate(PaymentInitiateDto dto)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host.Value}";
        return Ok(await _payments.InitiateAsync(dto, baseUrl));
    }

    /// <summary>Bank redirect lands here (GET) — verifies with the PSP then redirects to a friendly result page.</summary>
    [HttpGet("callback/{gateway}")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(PaymentGatewayType gateway, [FromQuery] Dictionary<string, string> query)
    {
        var ok = await _payments.HandleCallbackAsync(gateway, query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()));
        var url = $"/pay/result.html?status={(ok ? "ok" : "failed")}";
        return Redirect(url);
    }

    /// <summary>POST variant for gateways that verify server-side (IDPay style).</summary>
    [HttpPost("verify/{gateway}")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify(PaymentGatewayType gateway, [FromForm] Dictionary<string, string> form, [FromQuery] Dictionary<string, string> query)
    {
        var merged = form.Concat(query).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.First().Value);
        var ok = await _payments.HandleCallbackAsync(gateway, merged);
        return Ok(new { success = ok });
    }

    /// <summary>Sandbox bank simulator — internal page that auto-captures after a moment.</summary>
    [HttpGet("sandbox/{paymentId:guid}")]
    [AllowAnonymous]
    public IActionResult SandboxPage(Guid paymentId)
    {
        var url = Url.ActionLink(nameof(Callback), "Payments", values: null) ?? "/api/v1/payments/callback";
        var html = SandboxBankHtml(paymentId, Url.Action(nameof(Callback), "Payments", new { gateway = 10, paymentId })!);
        return Content(html, "text/html");
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? branchId, [FromQuery] int take = 100) => Ok(await _payments.ListAsync(branchId, take));

    [HttpPost("manual/{orderId}")]
    public async Task<IActionResult> ManualPaid(Guid orderId, [FromBody] ManualPayDto dto, [FromServices] IUserContext me)
    {
        await _payments.MarkManualPaidAsync(orderId, dto.Method, dto.RefId, me);
        return NoContent();
    }

    [HttpGet("gateways")]
    public async Task<IActionResult> Gateways() => Ok(await _payments.GetGatewayConfigsAsync());

    [HttpPut("gateways")]
    public async Task<IActionResult> UpdateGateway(GatewayConfigDto dto)
    {
        var me = (IUserContext)HttpContext.RequestServices.GetRequiredService<IUserContext>();
        if (!me.IsSuperAdmin) throw AppException.Forbidden("SuperAdmin only.");
        await _payments.UpdateGatewayConfigAsync(dto);
        return NoContent();
    }

    public record ManualPayDto(PaymentGatewayType Method, string? RefId);

    private static string SandboxBankHtml(Guid paymentId, string callbackUrl)
    {
        const string template = """
        <!doctype html><html><head><meta charset="utf-8"><title>Sandbox Gateway</title>
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <style>
          body{font-family:Segoe UI,Tahoma,sans-serif;background:#0f1420;color:#eef1f7;display:grid;place-items:center;height:100vh;margin:0}
          .card{background:#171e2e;border:1px solid #2a3550;border-radius:18px;padding:40px 48px;text-align:center;box-shadow:0 30px 80px #0008;max-width:420px}
          .ring{width:84px;height:84px;border-radius:50%;border:4px solid #f4b942;border-top-color:transparent;margin:0 auto 24px;animation:spin 1s linear infinite}
          @keyframes spin{to{transform:rotate(360deg)}}
          h1{font-size:20px;margin:0 0 8px} p{color:#9aa7c0;font-size:14px;line-height:1.8}
          .amt{font-size:28px;color:#f4b942;font-weight:700;margin:16px 0}
        </style></head><body><div class="card"><div class="ring"></div>
        <h1>Sandbox Payment Gateway</h1><p>Simulated bank page (Zarinpal / Mellat / Saman ...).<br>Payment is auto-approved in a moment...</p>
        <div class="amt">درگاه پرداخت آزمایشی</div>
        <p>Payment ID: @PAYMENT_ID@</p></div>
        <script>setTimeout(function(){window.location.href="@CALLBACK_URL@";},2500);</script>
        </body></html>
        """;
        return template.Replace("@PAYMENT_ID@", paymentId.ToString("N")).Replace("@CALLBACK_URL@", callbackUrl);
    }
}
