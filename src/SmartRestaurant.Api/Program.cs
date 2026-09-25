using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartRestaurant.Application.Interfaces;
using SmartRestaurant.Infrastructure.Data;
using SmartRestaurant.Infrastructure.Services;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database (SQLite — zero-config, file based) ----------
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "smartrestaurant.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

// ---------- Services ----------
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<ITableService, TableService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IKitchenService, KitchenService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IAccountingService, AccountingService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();

// ---------- Controllers ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Aurora Smart Restaurant API",
        Version = "v1",
        Description = "Full restaurant management platform: dine-in POS, online ordering, kitchen display, events & catering, inventory, accounting and analytics."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT auth. Enter: Bearer {token}",
        Name = "Authorization", In = ParameterLocation.Header, Type = SecuritySchemeType.Http, Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

// ---------- JWT Auth ----------
var jwtKey = builder.Configuration["Jwt:Key"] ?? "AuroraSmartRestaurant_SuperSecretKey_ChangeMe_2026_0123456789ABCDEF";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AuroraRestaurant",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "AuroraRestaurantClients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

// ---------- CORS (for external frontends / mobile) ----------
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---------- Migrate + Seed ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db);
}

// Serve the custom HTML/SCSS/JS frontend from /frontend
var frontendPath = Path.Combine(Directory.GetParent(builder.Environment.ContentRootPath)!.FullName, "..", "frontend");
frontendPath = Path.GetFullPath(frontendPath);
if (!Directory.Exists(frontendPath)) frontendPath = builder.Environment.WebRootPath ?? frontendPath;

app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath) });
app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath),
    ServeUnknownFileTypes = true
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aurora Smart Restaurant API v1");
    c.DocumentTitle = "Aurora API Docs";
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SPA-ish fallbacks: root serves the public site, unknown non-API routes go to the admin dashboard
app.MapGet("/", async ctx =>
{
    ctx.Response.ContentType = "text/html";
    await ctx.Response.SendFileAsync(Path.Combine(frontendPath, "index.html"));
});
app.MapFallback(ctx =>
{
    ctx.Response.ContentType = "text/html";
    ctx.Response.Redirect("/admin.html");
    return Task.CompletedTask;
});

app.Run();
