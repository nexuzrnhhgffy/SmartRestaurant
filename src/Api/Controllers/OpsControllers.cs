using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Api.Middleware;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;

namespace SmartRestaurant.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class CamerasController : ControllerBase
{
    private readonly ICameraService _cameras;
    public CamerasController(ICameraService cameras) => _cameras = cameras;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromServices] IUserContext me) => Ok(await _cameras.GetAllAsync(me));

    [HttpPost]
    public async Task<IActionResult> Upsert(CameraUpsertDto dto) => Ok(await _cameras.UpsertAsync(dto));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id) { await _cameras.DeleteAsync(id); return NoContent(); }

    /// <summary>
    /// Live MJPEG stream. Accepts:
    ///  • demo:*    → server-side generated animated frames (works with ZERO hardware)
    ///  • http(s):// → proxied MJPEG (fixes CORS / mixed-content for LAN cameras)
    ///  • rtsp://    → documented: put MediaMTX/go2rtc in front (README), which exposes MJPEG/HLS.
    /// </summary>
    [HttpGet("{id}/live")]
    [AllowAnonymous]
    public async Task Live(Guid id)
    {
        Response.Headers.Append("Cache-Control", "no-store");
        var cam = await _cameras.FindAsync(id);
        if (cam == null || !cam.IsActive) { Response.StatusCode = 404; return; }

        if (cam.Type == CameraType.Demo || cam.Url.StartsWith("demo:", StringComparison.OrdinalIgnoreCase))
        {
            await DemoMjpegStream.WriteAsync(Response, cam.Name, cam.Url, HttpContext.RequestAborted);
            return;
        }
        if (cam.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            await ProxyMjpegStream.WriteAsync(Response, cam.Url, HttpContext.RequestAborted);
            return;
        }
        Response.StatusCode = 400;
        await Response.WriteAsync($"Camera type {cam.Type} requires a transcoding gateway (MediaMTX) — see README.");
    }
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PrintersController : ControllerBase
{
    private readonly IPrintService _printer;
    public PrintersController(IPrintService printer) => _printer = printer;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? branchId) => Ok(await _printer.GetPrintersAsync(branchId));

    [HttpPost]
    public async Task<IActionResult> Upsert(PrinterDto dto) => Ok(await _printer.UpsertAsync(dto));

    [HttpPost("{id}/test")]
    public async Task<IActionResult> Test(Guid id) => Ok(new { success = await _printer.TestPrinterAsync(id) });

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id) { await _printer.DeletePrinterAsync(id); return NoContent(); }
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> Get([FromServices] IUserContext me, int take = 50) => Ok(await _notifications.GetForUserAsync(me, take));

    [HttpPut("{id}/read")]
    public async Task<IActionResult> Read(Guid id) { await _notifications.MarkReadAsync(id); return NoContent(); }

    [HttpPut("read-all")]
    public async Task<IActionResult> ReadAll([FromServices] IUserContext me) { await _notifications.MarkAllReadAsync(me); return NoContent(); }
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;
    public SettingsController(ISettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _settings.GetAllAsync());

    [HttpPut]
    public async Task<IActionResult> Set([FromBody] SettingDto dto)
    {
        var me = (IUserContext)HttpContext.RequestServices.GetRequiredService<IUserContext>();
        if (!me.IsSuperAdmin) throw AppException.Forbidden("SuperAdmin only.");
        await _settings.SetAsync(dto.Key, dto.Value);
        return NoContent();
    }
}
