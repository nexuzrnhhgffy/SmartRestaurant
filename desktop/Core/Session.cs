using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartRestaurant.Desktop.Core;

/// <summary>Current session (JWT + user) shared across all views.</summary>
public static class Session
{
    public static string? AccessToken { get; set; }
    public static string? RefreshToken { get; set; }
    public static UserDto? User { get; set; }
    public static Guid? BranchId => User?.BranchId;
    public static string Role => User?.RoleName ?? "?";
    public static string DisplayName => User?.FullName ?? "—";
    public static bool IsSuperAdmin => Role == "SuperAdmin";
    public static bool IsManagerOrAbove => Role is "SuperAdmin" or "Manager";
    public static void Clear() { AccessToken = null; RefreshToken = null; User = null; }
}

// ── lightweight DTO mirrors of the API contract ──
public class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public int Role { get; set; }
    public string RoleName { get; set; } = "";
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class AuthResponse
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public UserDto? User { get; set; }
}

public class BranchDto { public Guid Id { get; set; } public string Name { get; set; } = ""; public string? City { get; set; } public string? Address { get; set; } public string? Phone { get; set; } public bool IsActive { get; set; } }

public class DashboardStatsDto
{
    public decimal TodayRevenue { get; set; }
    public int TodayOrders { get; set; }
    public decimal AvgTicket { get; set; }
    public int OpenOrders { get; set; }
    public int LowStockCount { get; set; }
    public int TablesOccupied { get; set; }
    public int TablesTotal { get; set; }
    public List<SalesPointDto> Last7Days { get; set; } = new();
    public List<TopItemDto> TopItems { get; set; } = new();
    public List<BranchSalesDto> BranchSales { get; set; } = new();
    public EventDto? RunningEvent { get; set; }
}
public class SalesPointDto { public DateTime Date { get; set; } public decimal Revenue { get; set; } public int Orders { get; set; } }
public class TopItemDto { public string Name { get; set; } = ""; public int Qty { get; set; } public decimal Revenue { get; set; } }
public class BranchSalesDto { public Guid BranchId { get; set; } public string Name { get; set; } = ""; public decimal Revenue { get; set; } public int Orders { get; set; } }
public class EventDto { public Guid Id { get; set; } public string Title { get; set; } = ""; public string? Description { get; set; } public DateTime StartAt { get; set; } public DateTime EndAt { get; set; } public decimal DiscountPercent { get; set; } public string? BannerEmoji { get; set; } public bool IsRunning { get; set; } }

public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public int Type { get; set; }
    public string TypeName { get; set; } = "";
    public int Status { get; set; }
    public string StatusName { get; set; } = "";
    public int PaymentStatus { get; set; }
    public string PaymentStatusName { get; set; } = "";
    public Guid? TableId { get; set; }
    public int? TableNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public Guid? BranchId { get; set; }
    public string? CreatedByName { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}
public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public string ItemName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public int Status { get; set; }
    public int Station { get; set; }
    public string StationName { get; set; } = "";
    public DateTime? StartedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
}

public class KdsTicketDto
{
    public Guid OrderItemId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public string ItemName { get; set; } = "";
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public int Status { get; set; }
    public int Station { get; set; }
    public string? TableNumber { get; set; }
    public string TypeName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int ElapsedMinutes { get; set; }
    public bool IsLate { get; set; }
}

public class TableDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public int Seats { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = "";
    public Guid? CurrentOrderId { get; set; }
    public string? CurrentOrderNumber { get; set; }
    public decimal? OpenAmount { get; set; }
    public Guid? BranchId { get; set; }
}

public class MenuItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryEmoji { get; set; }
    public bool IsAvailable { get; set; }
    public int PrepMinutes { get; set; }
    public bool IsSpicy { get; set; }
    public bool IsVegetarian { get; set; }
    public int Calories { get; set; }
    public List<RecipeItemDto> Recipe { get; set; } = new();
}
public class RecipeItemDto { public Guid IngredientId { get; set; } public string IngredientName { get; set; } = ""; public decimal Quantity { get; set; } public string Unit { get; set; } = ""; }
public class CategoryDto { public Guid Id { get; set; } public string Name { get; set; } = ""; public string Emoji { get; set; } = ""; public int SortOrder { get; set; } public int ItemsCount { get; set; } }

public class IngredientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; }
    public decimal CostPerUnit { get; set; }
    public string? SupplierName { get; set; }
    public bool IsLow { get; set; }
    public decimal StockValue { get; set; }
}
public class StockMovementDto { public Guid Id { get; set; } public string IngredientName { get; set; } = ""; public int Type { get; set; } public decimal Quantity { get; set; } public decimal StockAfter { get; set; } public string? OrderNumber { get; set; } public string? Note { get; set; } public DateTime CreatedAt { get; set; } }

public class PaymentDto { public Guid Id { get; set; } public Guid OrderId { get; set; } public string OrderNumber { get; set; } = ""; public string GatewayName { get; set; } = ""; public decimal Amount { get; set; } public int Status { get; set; } public string? RefId { get; set; } public string? CardPanMasked { get; set; } public DateTime? PaidAt { get; set; } public DateTime CreatedAt { get; set; } }
public class PaymentResultDto { public Guid PaymentId { get; set; } public bool IsSandbox { get; set; } public string RedirectUrl { get; set; } = ""; public string GatewayName { get; set; } = ""; }
public class GatewayConfigDto { public Guid Id { get; set; } public string GatewayName { get; set; } = ""; public bool Enabled { get; set; } public bool Sandbox { get; set; } public string? MerchantKey { get; set; } }

public class ExpenseDto { public Guid Id { get; set; } public string Title { get; set; } = ""; public string? Category { get; set; } public decimal Amount { get; set; } public DateTime SpentAt { get; set; } public string? PaidTo { get; set; } public string? Note { get; set; } }
public class JournalEntryDto { public Guid Id { get; set; } public string EntryNumber { get; set; } = ""; public DateTime PostedAt { get; set; } public string Description { get; set; } = ""; public string? SourceType { get; set; } public List<JournalLineDto> Lines { get; set; } = new(); }
public class JournalLineDto { public string AccountCode { get; set; } = ""; public string AccountName { get; set; } = ""; public decimal Debit { get; set; } public decimal Credit { get; set; } public string? Memo { get; set; } }
public class ProfitLossDto { public decimal Revenue { get; set; } public decimal VatPayable { get; set; } public decimal Expenses { get; set; } public decimal NetProfit { get; set; } public int OrdersCount { get; set; } public decimal AvgTicket { get; set; } }
public class TrialBalanceRow { public string Code { get; set; } = ""; public string Name { get; set; } = ""; public int Group { get; set; } public decimal Debit { get; set; } public decimal Credit { get; set; } }
public class XReportDto { public string BranchName { get; set; } = ""; public DateTime GeneratedAt { get; set; } public int OrdersCount { get; set; } public int Guests { get; set; } public decimal GrossSales { get; set; } public decimal Discounts { get; set; } public decimal Vat { get; set; } public decimal NetSales { get; set; } public decimal CashCollected { get; set; } public decimal OnlineCollected { get; set; } public decimal CardCollected { get; set; } public int Voids { get; set; } }

public class CameraDto { public Guid Id { get; set; } public string Name { get; set; } = ""; public string Type { get; set; } = ""; public string Url { get; set; } = ""; public string? Location { get; set; } public bool IsActive { get; set; } public Guid? BranchId { get; set; } }
public class PrinterDto { public Guid Id { get; set; } public string Name { get; set; } = ""; public string Type { get; set; } = ""; public string Host { get; set; } = ""; public int Port { get; set; } public bool IsDefault { get; set; } public bool UseRasterMode { get; set; } }
public class ReservationDto { public Guid Id { get; set; } public string CustomerName { get; set; } = ""; public string Phone { get; set; } = ""; public DateTime ReservedFor { get; set; } public int PartySize { get; set; } public int? TableNumber { get; set; } public int Status { get; set; } public string? Note { get; set; } }
public class NotificationDto { public Guid Id { get; set; } public string Type { get; set; } = ""; public string Title { get; set; } = ""; public string Message { get; set; } = ""; public bool IsRead { get; set; } public DateTime CreatedAt { get; set; } }
public class SettingDto { public string Key { get; set; } = ""; public string Value { get; set; } = ""; public string? Description { get; set; } }

/// <summary>HTTP client with JWT attach + auto refresh + uniform error surfacing.</summary>
public class ApiClient
{
    private static readonly HttpClient Http = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions J = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
    private readonly DesktopSettings _settings;

    public ApiClient(DesktopSettings settings) => _settings = settings;

    public string BaseUrl => _settings.ServerUrl.TrimEnd('/');
    public void SetServer(string url) { _settings.ServerUrl = url; _settings.Save(); }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null, bool auth = true, bool retried = false)
    {
        using var req = new HttpRequestMessage(method, BaseUrl + path);
        if (auth && Session.AccessToken != null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Session.AccessToken);
        if (body != null) req.Content = JsonContent.Create(body);

        HttpResponseMessage res;
        try { res = await Http.SendAsync(req); }
        catch (Exception ex) { throw new ApiException($"Cannot reach server at {BaseUrl} — {ex.Message}"); }

        if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized && auth && !retried && Session.RefreshToken != null)
        {
            if (await TryRefreshAsync()) return await SendAsync<T>(method, path, body, auth, retried: true);
            Session.Clear();
            throw new ApiException("Session expired. Please sign in again.");
        }
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            string msg = text;
            try { msg = JsonSerializer.Deserialize<ApiError>(text, J)?.Error ?? text; } catch { }
            throw new ApiException($"{(int)res.StatusCode}: {msg}");
        }
        return JsonSerializer.Deserialize<T>(text, J)!;
    }

    private async Task<bool> TryRefreshAsync()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/api/v1/auth/refresh")
            { Content = JsonContent.Create(new { refreshToken = Session.RefreshToken }) };
            using var res = await Http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return false;
            var auth = JsonSerializer.Deserialize<AuthResponse>(await res.Content.ReadAsStringAsync(), J)!;
            Session.AccessToken = auth.AccessToken; Session.RefreshToken = auth.RefreshToken; Session.User = auth.User;
            return true;
        }
        catch { return false; }
    }

    // ── auth ──
    public Task<AuthResponse> LoginAsync(string user, string pass) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "/api/v1/auth/login", new { userName = user, password = pass }, auth: false);

    // ── generic REST surface used by views ──
    public Task<T> GetAsync<T>(string path) => SendAsync<T>(HttpMethod.Get, path);
    public Task<T> PostAsync<T>(string path, object body) => SendAsync<T>(HttpMethod.Post, path, body);
    public Task<T> PutAsync<T>(string path, object body) => SendAsync<T>(HttpMethod.Put, path, body);
    public Task DeleteAsync(string path) => SendAsync<object>(HttpMethod.Delete, path);

    public Task PingAsync() => SendAsync<object>(HttpMethod.Get, "/api/v1/reports/dashboard", auth: false);
}

public class ApiException : Exception { public ApiException(string msg) : base(msg) { } }
public class ApiError { public string Error { get; set; } = ""; }
