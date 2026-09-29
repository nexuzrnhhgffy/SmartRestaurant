using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Application.Payments;
using SmartRestaurant.Application.Security;
using SmartRestaurant.Infrastructure.Data;
using SmartRestaurant.Infrastructure.Services;

namespace SmartRestaurant.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddSmartRestaurant(this IServiceCollection services, IConfiguration cfg)
    {
        var cs = cfg.GetConnectionString("Default") ?? "Data Source=smartrestaurant.db";
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(cs));

        // security
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();

        // business services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ITableService, TableService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ICameraService, CameraService>();
        services.AddScoped<IPrintService, PrintService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISettingsService, SettingsService>();

        // payment gateways
        services.AddSingleton<IPaymentGateway, ZarinpalGateway>();
        services.AddSingleton<IPaymentGateway, ZibalGateway>();
        services.AddSingleton<IPaymentGateway, IdPayGateway>();
        services.AddSingleton<IPaymentGateway, PayIrGateway>();
        services.AddSingleton<IPaymentGateway, NextPayGateway>();
        services.AddSingleton<IPaymentGateway, MellatGateway>();
        services.AddSingleton<IPaymentGateway, SamanGateway>();
        services.AddSingleton<IPaymentGateway, ParsianGateway>();
        services.AddSingleton<IPaymentGateway, PasargadGateway>();
        services.AddSingleton<IPaymentGateway, NovinGateway>();
        services.AddSingleton<GatewayFactory>();

        return services;
    }
}
