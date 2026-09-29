using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartRestaurant.Application.Dtos;
using SmartRestaurant.Application.Interfaces;
using SmartRestaurant.Domain.Entities.Identity;
using SmartRestaurant.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SmartRestaurant.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return new AuthResponse(false, "Invalid email or password.", null, null, null);
        if (!user.IsActive)
            return new AuthResponse(false, "This account has been deactivated. Contact the manager.", null, null, null);

        user.LastLoginAt = DateTime.UtcNow;
        var (token, refresh) = GenerateTokens(user);
        user.RefreshToken = refresh;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync();
        return new AuthResponse(true, $"Welcome back, {user.FullName.Split(' ')[0]}!", token, refresh, ToDto(user));
    }

    public async Task<AuthResponse> PinLoginAsync(PinLoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.PinCode == request.Pin && u.IsActive);
        if (user == null)
            return new AuthResponse(false, "Invalid PIN code.", null, null, null);
        var (token, refresh) = GenerateTokens(user);
        return new AuthResponse(true, $"Station unlocked — {user.FullName}", token, refresh, ToDto(user));
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return new AuthResponse(false, "This email is already registered.", null, null, null);

        var user = new AppUser
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = AppRole.Customer,
            PinCode = null
        };
        _db.Users.Add(user);

        // mirror into loyalty CRM
        _db.Customers.Add(new Domain.Entities.Customers.Customer
        {
            FullName = request.FullName, Phone = request.Phone, Email = request.Email
        });
        await _db.SaveChangesAsync();

        var (token, refresh) = GenerateTokens(user);
        return new AuthResponse(true, "Account created — welcome to Aurora!", token, refresh, ToDto(user));
    }

    public async Task<UserDto?> GetUserAsync(int id)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        return u == null ? null : ToDto(u);
    }

    public async Task<List<UserDto>> GetStaffAsync() =>
        (await _db.Users.Where(u => u.Role != AppRole.Customer).OrderBy(u => u.Role).ToListAsync()).Select(ToDto).ToList();

    public async Task<UserDto> SaveStaffAsync(UserDto dto, string? password)
    {
        AppUser user;
        if (dto.Id == 0)
        {
            user = new AppUser { FullName = dto.FullName, Email = dto.Email, Phone = dto.Phone ?? "", Role = dto.Role, IsActive = dto.IsActive, JobTitle = dto.JobTitle, HireDate = dto.HireDate ?? DateTime.UtcNow, BaseSalary = dto.BaseSalary, PasswordHash = BCrypt.Net.BCrypt.HashPassword(string.IsNullOrWhiteSpace(password) ? "Welcome@123" : password), PinCode = null };
            _db.Users.Add(user);
        }
        else
        {
            user = await _db.Users.FirstAsync(u => u.Id == dto.Id);
            user.FullName = dto.FullName; user.Email = dto.Email; user.Phone = dto.Phone ?? "";
            user.Role = dto.Role; user.IsActive = dto.IsActive; user.JobTitle = dto.JobTitle;
            user.BaseSalary = dto.BaseSalary; user.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        }
        await _db.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task<bool> ToggleStaffActiveAsync(int id)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role != AppRole.SuperAdmin);
        if (u == null) return false;
        u.IsActive = !u.IsActive;
        await _db.SaveChangesAsync();
        return true;
    }

    private (string token, string refresh) GenerateTokens(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (new JwtSecurityTokenHandler().WriteToken(token), refresh);
    }

    private static UserDto ToDto(AppUser u) =>
        new(u.Id, u.FullName, u.Email, u.Phone, u.Role, u.IsActive, u.JobTitle, u.AvatarUrl, u.HireDate, u.BaseSalary, u.LastLoginAt);
}
