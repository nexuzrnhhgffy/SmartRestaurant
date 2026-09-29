using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Application.Security;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _hasher;

    public AuthService(AppDbContext db, ITokenService tokens, IPasswordHasher hasher)
        => (_db, _tokens, _hasher) = (db, tokens, hasher);

    public async Task<AuthResponse> LoginAsync(LoginRequest req, string? ip = null)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == req.UserName && !u.IsDeleted)
            ?? throw AppException.BadRequest("Invalid username or password.");
        if (!user.IsActive) throw AppException.Forbidden("This account is deactivated. Contact the administrator.");
        if (!_hasher.Verify(req.Password, user.PasswordHash)) throw AppException.BadRequest("Invalid username or password.");

        user.LastLoginAt = DateTime.UtcNow;
        var branchName = user.BranchId != null ? (await _db.Branches.FindAsync(user.BranchId))?.Name : null;
        var (token, exp) = _tokens.CreateAccessToken(user);
        var rt = new RefreshToken { UserId = user.Id, Token = _tokens.CreateRefreshToken(), ExpiresAt = DateTime.UtcNow.AddDays(14), CreatedByIp = ip };
        _db.RefreshTokens.Add(rt);
        _db.AuditLogs.Add(new AuditLog { UserId = user.Id, UserName = user.UserName, Action = "Login", Ip = ip, BranchId = user.BranchId });
        await _db.SaveChangesAsync();

        return new AuthResponse(token, rt.Token, exp, ToDto(user, branchName));
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest req, string? ip = null)
    {
        var rt = await _db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.Token == req.RefreshToken)
            ?? throw AppException.BadRequest("Invalid refresh token.");
        if (!rt.IsActive) throw AppException.Forbidden("Refresh token expired or revoked.");
        rt.RevokedAt = DateTime.UtcNow;
        var user = rt.User!;
        var (token, exp) = _tokens.CreateAccessToken(user);
        var branchName = user.BranchId != null ? (await _db.Branches.FindAsync(user.BranchId))?.Name : null;
        var newRt = new RefreshToken { UserId = user.Id, Token = _tokens.CreateRefreshToken(), ExpiresAt = DateTime.UtcNow.AddDays(14), CreatedByIp = ip };
        _db.RefreshTokens.Add(newRt);
        await _db.SaveChangesAsync();
        return new AuthResponse(token, newRt.Token, exp, ToDto(user, branchName));
    }

    public async Task<AuthResponse> RegisterCustomerAsync(string fullName, string userName, string email, string password, string? phone)
    {
        if (await _db.Users.AnyAsync(u => u.UserName == userName)) throw AppException.BadRequest("Username already taken.");
        var user = new AppUser { FullName = fullName, UserName = userName, Email = email, Phone = phone, PasswordHash = _hasher.Hash(password), Role = UserRole.Customer };
        _db.Users.Add(user);
        var (token, exp) = _tokens.CreateAccessToken(user);
        var rt = new RefreshToken { UserId = user.Id, Token = _tokens.CreateRefreshToken(), ExpiresAt = DateTime.UtcNow.AddDays(14) };
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync();
        return new AuthResponse(token, rt.Token, exp, ToDto(user, null));
    }

    internal static UserDto ToDto(AppUser u, string? branchName) => new()
    {
        Id = u.Id, FullName = u.FullName, UserName = u.UserName, Email = u.Email, Phone = u.Phone,
        Role = u.Role, BranchId = u.BranchId, BranchName = branchName, IsActive = u.IsActive, LastLoginAt = u.LastLoginAt
    };
}

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    public UserService(AppDbContext db, IPasswordHasher hasher) => (_db, _hasher) = (db, hasher);

    public async Task<List<UserDto>> GetAllAsync(Guid? branchId = null)
    {
        var q = _db.Users.AsNoTracking().Where(u => !u.IsDeleted);
        if (branchId != null) q = q.Where(u => u.BranchId == branchId);
        var users = await q.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync();
        var branchNames = await _db.Branches.ToDictionaryAsync(b => b.Id, b => b.Name);
        return users.Select(u => AuthService.ToDto(u, u.BranchId != null ? branchNames.GetValueOrDefault(u.BranchId.Value) : null)).ToList();
    }

    public async Task<UserDto> UpsertAsync(UserUpsertDto dto)
    {
        AppUser user;
        if (dto.Id is null)
        {
            if (string.IsNullOrWhiteSpace(dto.Password)) throw AppException.BadRequest("Password is required for new users.");
            if (await _db.Users.AnyAsync(u => u.UserName == dto.UserName)) throw AppException.BadRequest("Username already taken.");
            user = new AppUser { UserName = dto.UserName.Trim().ToLowerInvariant() };
            _db.Users.Add(user);
        }
        else
        {
            user = await _db.Users.FindAsync(dto.Id) ?? throw AppException.NotFound("User");
        }
        user.FullName = dto.FullName; user.Email = dto.Email; user.Phone = dto.Phone;
        user.Role = dto.Role; user.BranchId = dto.Role == UserRole.SuperAdmin ? null : dto.BranchId; user.IsActive = dto.IsActive;
        if (!string.IsNullOrWhiteSpace(dto.Password)) user.PasswordHash = _hasher.Hash(dto.Password);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var branchName = user.BranchId != null ? (await _db.Branches.FindAsync(user.BranchId))?.Name : null;
        return AuthService.ToDto(user, branchName);
    }

    public async Task DeactivateAsync(Guid id)
    {
        var user = await _db.Users.FindAsync(id) ?? throw AppException.NotFound("User");
        user.IsActive = false; user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
