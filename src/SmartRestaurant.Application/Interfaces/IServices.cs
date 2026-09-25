using SmartRestaurant.Application.Dtos;
using SmartRestaurant.Domain.Entities.Identity;
using SmartRestaurant.Domain.Entities.Menu;

namespace SmartRestaurant.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> PinLoginAsync(PinLoginRequest request);
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<UserDto?> GetUserAsync(int id);
    Task<List<UserDto>> GetStaffAsync();
    Task<UserDto> SaveStaffAsync(UserDto dto, string? password);
    Task<bool> ToggleStaffActiveAsync(int id);
}

public interface IMenuService
{
    Task<List<MenuItemDto>> GetMenuAsync(int? categoryId = null, bool includeUnavailable = true, string? search = null);
    Task<MenuItemDto?> GetItemAsync(int id);
    Task<MenuItemDto> SaveItemAsync(SaveMenuItemDto dto);
    Task<bool> DeleteItemAsync(int id);
    Task<bool> SetAvailabilityAsync(int id, bool available);
    Task<List<Category>> GetCategoriesAsync(bool includeInactive = false);
    Task<Category> SaveCategoryAsync(SaveCategoryDto dto);
    Task<bool> DeleteCategoryAsync(int id);
}

public interface ITableService
{
    Task<List<TableDto>> GetTablesAsync();
    Task<TableDto> SaveTableAsync(SaveTableDto dto);
    Task<bool> UpdateStatusAsync(int id, int status);
    Task<bool> DeleteTableAsync(int id);
}

public interface IReservationService
{
    Task<List<ReservationDto>> GetAllAsync(DateTime? date = null, int? status = null);
    Task<ReservationDto> SaveAsync(SaveReservationDto dto);
    Task<bool> UpdateStatusAsync(int id, int status);
    Task<bool> CancelAsync(int id);
}

public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderDto dto, int? userId);
    Task<OrderDto?> GetByIdAsync(int id);
    Task<List<OrderDto>> GetAllAsync(int? type = null, int? status = null, DateTime? date = null, string? search = null);
    Task<OrderDto> UpdateStatusAsync(int id, int status);
    Task<OrderDto> UpdateItemStatusAsync(int orderItemId, int status);
    Task<bool> AddItemAsync(int orderId, CreateOrderItemDto dto);
    Task<bool> RemoveItemAsync(int orderItemId);
    Task<bool> CancelAsync(int id, string? reason);
    Task<OrderStatsDto> GetStatsAsync();
}

public interface IPaymentService
{
    Task<bool> PayAsync(PaymentDto dto, int cashierId);
    Task<List<PaymentRecordDto>> GetOrderPaymentsAsync(int orderId);
}

public interface IKitchenService
{
    Task<KitchenQueueDto> GetQueueAsync();
}

public interface IInventoryService
{
    Task<List<IngredientDto>> GetIngredientsAsync(bool lowOnly = false);
    Task<IngredientDto> SaveIngredientAsync(SaveIngredientDto dto);
    Task<bool> DeleteIngredientAsync(int id);
    Task<List<SupplierDto>> GetSuppliersAsync();
    Task<SupplierDto> SaveSupplierAsync(SaveSupplierDto dto);
    Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(int? status = null);
    Task<PurchaseOrderDto> SavePurchaseOrderAsync(SavePurchaseOrderDto dto, int userId);
    Task<bool> ReceivePurchaseOrderAsync(int id);
    Task<List<StockMovementDto>> GetMovementsAsync(int? ingredientId = null);
    Task<bool> AdjustStockAsync(StockAdjustDto dto, int userId);
}

public interface IEventService
{
    Task<List<EventPackageDto>> GetPackagesAsync();
    Task<EventPackageDto> SavePackageAsync(SaveEventPackageDto dto);
    Task<bool> DeletePackageAsync(int id);
    Task<List<EventBookingDto>> GetBookingsAsync(int? status = null);
    Task<EventBookingDto> SaveBookingAsync(SaveEventBookingDto dto);
    Task<bool> UpdateBookingStatusAsync(int id, int status);
    Task<bool> SetDepositAsync(int id, decimal amount, bool paid);
    Task<EventTaskDto> AddTaskAsync(int bookingId, SaveEventTaskDto dto);
    Task<bool> ToggleTaskAsync(int taskId);
}

public interface IAccountingService
{
    Task<List<ExpenseDto>> GetExpensesAsync(DateTime? from = null, DateTime? to = null);
    Task<ExpenseDto> SaveExpenseAsync(SaveExpenseDto dto, int userId);
    Task<bool> DeleteExpenseAsync(int id);
    Task<List<PayrollDto>> GetPayrollsAsync(string? period = null);
    Task<PayrollDto> SavePayrollAsync(SavePayrollDto dto);
    Task<bool> MarkPayrollPaidAsync(int id);
    Task<ProfitLossDto> GetProfitLossAsync(DateTime from, DateTime to);
}

public interface IStaffService
{
    Task<List<ShiftDto>> GetShiftsAsync(DateTime? from = null, DateTime? to = null);
    Task<ShiftDto> SaveShiftAsync(SaveShiftDto dto);
    Task<bool> DeleteShiftAsync(int id);
    Task<List<AttendanceDto>> GetAttendanceAsync(DateTime? date = null);
    Task<AttendanceDto> CheckInAsync(int employeeId);
    Task<AttendanceDto> CheckOutAsync(int employeeId);
}

public interface IDashboardService
{
    Task<DashboardKpiDto> GetKpisAsync();
    Task<SalesChartDto> GetSalesChartsAsync(int days = 7);
}

public interface ICustomerService
{
    Task<List<CustomerDto>> GetAllAsync(string? search = null);
    Task<CustomerDto> SaveAsync(SaveCustomerDto dto);
    Task<bool> AddPointsAsync(int id, int points);
    Task<CustomerDto?> GetByPhoneAsync(string phone);
}

public interface IReviewService
{
    Task<List<ReviewDto>> GetAllAsync(int? status = null);
    Task<ReviewDto> SubmitAsync(SaveReviewDto dto);
    Task<bool> ModerateAsync(int id, int status);
    Task<bool> ReplyAsync(int id, string reply);
}

public interface ICouponService
{
    Task<List<CouponDto>> GetAllAsync();
    Task<CouponDto> SaveAsync(SaveCouponDto dto);
    Task<bool> DeleteAsync(int id);
    Task<CouponValidationResult> ValidateAsync(ValidateCouponDto dto);
}

public interface INotificationService
{
    Task<List<NotificationDto>> GetAllAsync(int? role = null, bool unreadOnly = false);
    Task PushAsync(string title, string message, int type, Domain.Entities.Identity.AppRole? targetRole = null, string? link = null);
    Task<bool> MarkReadAsync(int id);
    Task<bool> MarkAllReadAsync(int? role = null);
}

public interface ISettingsService
{
    Task<SettingsDto> GetAsync();
    Task<SettingsDto> SaveAsync(SettingsDto dto);
}
