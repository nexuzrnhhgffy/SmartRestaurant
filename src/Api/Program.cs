using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Infrastructure;
using SmartRestaurant.Api.Middleware;
using SmartRestaurant.Infrastructure.Data;
using SmartRestaurant.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// ── services ──────────────────────────────────────────────────────
builder.Services.AddSmartRestaurant(builder.Configuration);
builder.Services.AddSignalR(o => o.EnableDetailedErrors = true);
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SmartRestaurant API",
        Version = "v1",
        Description = "Full restaurant OS API — orders, KDS, payments (Zarinpal + Iranian PSPs), inventory, accounting, cameras, ESC/POS printing. Mobile apps consume the same JWT endpoints."
    });
    o.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization", Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = Microsoft.OpenApi.Models.ParameterLocation.Header, Description = "Paste JWT here"
    });
    o.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var jwtKey = builder.Configuration["Jwt:Key"] ?? "SR_DEV_KEY_CHANGE_ME_0123456789_ABCDEFGHIJ";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(1),
        RoleClaimType = "roleName"
    };
    // let SignalR receive the token from query string
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var accessToken = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                ctx.Token = accessToken;
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext>(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    if (http?.User.Identity?.IsAuthenticated != true) return new AnonymousUserContext();
    var claims = http.User.Claims;
    Guid? branch = Guid.TryParse(claims.FirstOrDefault(c => c.Type == "branchId")?.Value, out var b) ? b : null;
    UserRole? role = null;
    if (int.TryParse(claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value, out var r)) role = (UserRole)r;
    Guid? uid = Guid.TryParse(claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var u) ? u : null;
    return new JwtUserContext(uid, http.User.Identity.Name ?? claims.FirstOrDefault(c => c.Type == "fullName")?.Value, role, branch);
});

var app = builder.Build();

// ── migrate + seed ────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DataSeeder.SeedAsync(db);
    app.Logger.LogInformation("Database ready at {Path}", app.Configuration.GetConnectionString("Default"));
}

// ── pipeline ──────────────────────────────────────────────────────
app.UseMiddleware<ExceptionMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SmartRestaurant.Infrastructure.Hubs.KdsHub>("/hubs/kds");
app.MapHub<SmartRestaurant.Infrastructure.Hubs.NotificationHub>("/hubs/notifications");
if (app.Environment.IsDevelopment() || true) { app.UseSwagger(); app.UseSwaggerUI(); }

app.Logger.LogInformation("Zafaran SmartRestaurant API listening on http://0.0.0.0:5000");
app.Run();

// ── user context impls ────────────────────────────────────────────
file class JwtUserContext(Guid? id, string? name, UserRole? role, Guid? branch) : IUserContext
{
    public Guid? UserId => id;
    public string? UserName => name;
    public UserRole? Role => role;
    public Guid? BranchId => branch;
    public bool IsSuperAdmin => role == UserRole.SuperAdmin;
    public bool IsAuthenticated => true;
}
