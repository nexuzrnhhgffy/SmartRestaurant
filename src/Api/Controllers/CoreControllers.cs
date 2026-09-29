using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Application.Contracts;

namespace SmartRestaurant.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
        => Ok(await _auth.LoginAsync(req, HttpContext.Connection.RemoteIpAddress?.ToString()));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req)
        => Ok(await _auth.RefreshAsync(req, HttpContext.Connection.RemoteIpAddress?.ToString()));

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterDto dto)
        => Ok(await _auth.RegisterCustomerAsync(dto.FullName, dto.UserName, dto.Email, dto.Password, dto.Phone));

    public record RegisterDto(string FullName, string UserName, string Email, string Password, string? Phone);
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;
    public UsersController(IUserService users) => _users = users;

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll([FromQuery] Guid? branchId) => Ok(await _users.GetAllAsync(branchId));

    [HttpPost]
    public async Task<ActionResult<UserDto>> Upsert(UserUpsertDto dto)
    {
        var me = (IUserContext)HttpContext.RequestServices.GetRequiredService<IUserContext>();
        if (me.Role != UserRole.SuperAdmin && me.Role != UserRole.Manager) throw AppException.Forbidden("Only SuperAdmin/Manager manage users.");
        return Ok(await _users.UpsertAsync(dto));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Deactivate(Guid id) { await _users.DeactivateAsync(id); return NoContent(); }
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branches;
    public BranchesController(IBranchService branches) => _branches = branches;

    [HttpGet] public async Task<IActionResult> GetAll() => Ok(await _branches.GetAllAsync());
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromServices] IUserContext me) => Ok(await _branches.GetForUserAsync(me));
    [HttpPost]
    public async Task<IActionResult> Upsert(BranchUpsertDto dto)
    {
        RequireSuperAdmin();
        return Ok(await _branches.UpsertAsync(dto));
    }
    private void RequireSuperAdmin()
    {
        var me = (IUserContext)HttpContext.RequestServices.GetRequiredService<IUserContext>();
        if (!me.IsSuperAdmin) throw AppException.Forbidden("SuperAdmin only.");
    }
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MenuController : ControllerBase
{
    private readonly IMenuService _menu;
    private readonly ISettingsService _settings;
    public MenuController(IMenuService menu, ISettingsService settings) => (_menu, _settings) = (menu, settings);

    [HttpGet("categories")] public async Task<IActionResult> Categories() => Ok(await _menu.GetCategoriesAsync());
    [HttpGet("items")]
    public async Task<IActionResult> Items([FromQuery] Guid? branchId, [FromQuery] bool onlyAvailable = false)
        => Ok(await _menu.GetItemsAsync(branchId, onlyAvailable));
    [HttpPost("items")] public async Task<IActionResult> Upsert(MenuItemUpsertDto dto) => Ok(await _menu.UpsertAsync(dto));
    [HttpPatch("items/{id}/availability")]
    public async Task<IActionResult> Availability(Guid id, [FromBody] AvailabilityDto dto) { await _menu.SetAvailabilityAsync(id, dto.Available); return NoContent(); }
    [HttpDelete("items/{id}")] public async Task<IActionResult> Delete(Guid id) { await _menu.DeleteAsync(id); return NoContent(); }
    public record AvailabilityDto(bool Available);
}
