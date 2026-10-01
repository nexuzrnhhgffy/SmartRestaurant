using SmartRestaurant.Domain.Entities;

namespace SmartRestaurant.Application.Security;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>PBKDF2-SHA256, 100k iterations. Format: pbkdf2.{iterations}.{saltB64}.{keyB64}</summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(SaltSize);
        var key = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, System.Security.Cryptography.HashAlgorithmName.SHA256, KeySize);
        return $"pbkdf2.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        try
        {
            var parts = hash.Split('.');
            if (parts.Length != 4 || parts[0] != "pbkdf2") return false;
            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, System.Security.Cryptography.HashAlgorithmName.SHA256, expected.Length);
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }
}

public interface ITokenService
{
    (string token, DateTime expiresAtUtc) CreateAccessToken(AppUser user);
    string CreateRefreshToken();
}

public class TokenService : ITokenService
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _cfg;
    public TokenService(Microsoft.Extensions.Configuration.IConfiguration cfg) => _cfg = cfg;

    public (string, DateTime) CreateAccessToken(AppUser user)
    {
        var key = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var minutes = int.TryParse(_cfg["Jwt:AccessMinutes"], out var m) ? m : 120;
        var expires = DateTime.UtcNow.AddMinutes(minutes);
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(System.Security.Claims.ClaimTypes.Name, user.UserName),
            new("fullName", user.FullName),
            new(System.Security.Claims.ClaimTypes.Role, ((int)user.Role).ToString()),
            new("roleName", user.Role.ToString()),
        };
        if (user.BranchId is not null) claims.Add(new System.Security.Claims.Claim("branchId", user.BranchId.Value.ToString()));

        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)),
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"], audience: _cfg["Jwt:Audience"],
            claims: claims, expires: expires, signingCredentials: creds);
        return (new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
}
