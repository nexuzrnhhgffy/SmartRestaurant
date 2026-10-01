using SmartRestaurant.Application.Common;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;

namespace SmartRestaurant.Application.Contracts;

// ═══════════ Auth & Users ═══════════
public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest req, string? ip = null);
    Task<AuthResponse> RefreshAsync(RefreshRequest req, string? ip = null);
    Task<AuthResponse> RegisterCustomerAsync(string fullName, string userName, string email, string password, string? phone);
}

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync(Guid? branchId = null);
    Task<UserDto> UpsertAsync(UserUpsertDto dto);
    Task DeactivateAsync(Guid id);
}

public interface IBranchService
{
    Task<List<BranchDto>> GetAllAsync();
    Task<List<BranchDto>> GetForUserAsync(IUserContext user);
    Task<BranchDto> UpsertAsync(BranchUpsertDto dto);
}

// ═══════════ Catalog ═══════════
public interface IMenuService
{
    Task<List<CategoryDto>> GetCategoriesAsync();
    Task<List<MenuItemDto>> GetItemsAsync(Guid? branchId, bool onlyAvailable = false);
    Task<MenuItemDto> UpsertAsync(MenuItemUpsertDto dto);
    Task SetAvailabilityAsync(Guid id, bool available);
    Task DeleteAsync(Guid id);
}

public interface IEventService
{
    Task<List<EventDto>> GetAllAsync(Guid? branchId = null);
    Task<EventDto> UpsertAsync(EventDto dto);
    Task<EventDto?> GetRunningAsync(Guid? branchId);
    Task DeactivateAsync(Guid id);
}

// ═══════════ Orders / Tables / Reservations / KDS ═══════════
public interface IOrderService
{
    Task<OrderDto> CreateAsync(OrderCreateDto dto, IUserContext actor);
    Task<OrderDto> GetAsync(Guid id);
    Task<List<OrderDto>> ListAsync(Guid? branchId, OrderStatus? status, OrderType? type, DateTime? from, int take = 100);
    Task<List<OrderDto>> ListActiveAsync(Guid? branchId);
    Task<OrderDto> UpdateStatusAsync(Guid id, OrderStatusUpdateDto dto, IUserContext actor);
    Task<OrderDto> UpdateItemStatusAsync(Guid orderItemId, OrderItemStatus status, IUserContext actor);
    Task<List<KdsTicketDto>> GetKdsTicketsAsync(Guid? branchId, Station? station);
}

public interface ITableService
{
    Task<List<TableDto>> GetTablesAsync(Guid? branchId);
    Task<TableDto> UpsertAsync(TableDto dto);
    Task FreeAsync(Guid tableId);
    Task DeleteAsync(Guid id);
}

public interface IReservationService
{
    Task<List<ReservationDto>> GetAllAsync(Guid? branchId, DateTime? day = null);
    Task<ReservationDto> UpsertAsync(ReservationUpsertDto dto);
    Task<ReservationDto> SetStatusAsync(Guid id, ReservationStatus status);
}

// ═══════════ Inventory ═══════════
public interface IInventoryService
{
    Task<List<IngredientDto>> GetIngredientsAsync(Guid? branchId);
    Task<IngredientDto> UpsertAsync(IngredientUpsertDto dto);
    Task DeleteAsync(Guid id);
    Task ReceiveAsync(StockAdjustDto dto, IUserContext actor);
    Task DeductForOrderAsync(Guid orderId);                       // recipe → stock, called on completion
    Task<List<StockMovementDto>> GetMovementsAsync(Guid? branchId, Guid? ingredientId, int take = 200);
    Task<int> LowStockCountAsync(Guid? branchId);
}

// ═══════════ Payments ═══════════
public interface IPaymentService
{
    Task<PaymentResultDto> InitiateAsync(PaymentInitiateDto dto, string apiBaseUrl);
    Task<bool> HandleCallbackAsync(PaymentGatewayType gateway, Dictionary<string, string> queryParams);
    Task MarkManualPaidAsync(Guid orderId, PaymentGatewayType method, string? refId, IUserContext actor);
    Task<List<PaymentDto>> ListAsync(Guid? branchId, int take = 100);
    Task<List<GatewayConfigDto>> GetGatewayConfigsAsync();
    Task UpdateGatewayConfigAsync(GatewayConfigDto dto);
}

// ═══════════ Finance & Reports ═══════════
public interface IAccountingService
{
    Task PostSaleAsync(Guid orderId);                             // auto journal on completion
    Task<ExpenseDto> AddExpenseAsync(ExpenseUpsertDto dto);
    Task<List<ExpenseDto>> GetExpensesAsync(Guid? branchId, DateTime? from, DateTime? to);
    Task<List<JournalEntryDto>> GetJournalsAsync(Guid? branchId, int take = 100);
    Task<List<TrialBalanceRow>> GetTrialBalanceAsync(Guid? branchId, DateTime? from, DateTime? to);
    Task<ProfitLossDto> GetProfitLossAsync(Guid? branchId, DateTime? from, DateTime? to);
}

public interface IReportService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(Guid? branchId);
    Task<List<SalesPointDto>> GetSalesSeriesAsync(Guid? branchId, DateTime from, DateTime to);
    Task<List<TopItemDto>> GetTopItemsAsync(Guid? branchId, DateTime from, DateTime to, int take = 8);
    Task<XReportDto> GetXReportAsync(Guid? branchId);
}

// ═══════════ Ops ═══════════
public interface ICameraService
{
    Task<List<CameraDto>> GetAllAsync(IUserContext user);
    Task<CameraDto> UpsertAsync(CameraUpsertDto dto);
    Task DeleteAsync(Guid id);
    Task<Camera?> FindAsync(Guid id);
}

public interface IPrintService
{
    Task PrintKitchenTicketAsync(Guid orderId);
    Task PrintReceiptAsync(Guid orderId, Guid? paymentId);
    Task<bool> TestPrinterAsync(Guid printerId);
    Task<List<PrinterDto>> GetPrintersAsync(Guid? branchId);
    Task<PrinterDto> UpsertAsync(PrinterDto dto);
    Task DeletePrinterAsync(Guid id);
}

public interface INotificationService
{
    Task<NotificationDto> CreateAsync(NotificationType type, string title, string message,
        Guid? branchId, UserRole? targetRole = null, Guid? userId = null);
    Task<List<NotificationDto>> GetForUserAsync(IUserContext user, int take = 50);
    Task MarkReadAsync(Guid id);
    Task MarkAllReadAsync(IUserContext user);
}

public interface ISettingsService
{
    Task<List<SettingDto>> GetAllAsync();
    Task SetAsync(string key, string value);
    Task<decimal> GetTaxRateAsync();
    Task<string> GetAsync(string key, string fallback);
}
