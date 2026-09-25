namespace SmartRestaurant.Application.Dtos;

// ---------- Events / Catering ----------
public record EventPackageDto(int Id, string Name, string? Description, decimal PricePerPerson, int MinGuests,
    string? IncludedServices, string? ImageUrl, bool IsActive);
public record SaveEventPackageDto(int? Id, string Name, string? Description, decimal PricePerPerson, int MinGuests,
    string? IncludedServices, string? ImageUrl, bool IsActive);
public record EventBookingDto(int Id, string CustomerName, string Phone, string? Email, int Type, string TypeName,
    int? PackageId, string? PackageName, DateTime EventDate, TimeSpan? StartTime, TimeSpan? EndTime, int GuestCount,
    int Venue, string VenueName, string? Address, string? MenuNotes, decimal QuoteAmount, decimal DepositAmount,
    bool DepositPaid, int Status, string StatusName, string? Notes, List<EventTaskDto> Tasks);
public record SaveEventBookingDto(int? Id, string CustomerName, string Phone, string? Email, int Type, int? PackageId,
    DateTime EventDate, TimeSpan? StartTime, TimeSpan? EndTime, int GuestCount, int Venue, string? Address,
    string? MenuNotes, string? Notes);
public record EventTaskDto(int Id, string Title, int? AssignedToId, string? AssignedToName, DateTime? DueDate, bool IsDone);
public record SaveEventTaskDto(string Title, int? AssignedToId, DateTime? DueDate);

// ---------- Accounting ----------
public record ExpenseDto(int Id, int Category, string CategoryName, decimal Amount, string Description, DateTime Date,
    string? PaidTo, string? ReceiptRef, string? CreatedByName);
public record SaveExpenseDto(int? Id, int Category, decimal Amount, string Description, DateTime Date, string? PaidTo, string? ReceiptRef);
public record PayrollDto(int Id, int EmployeeId, string EmployeeName, string Period, decimal BaseSalary,
    double OvertimeHours, decimal OvertimePay, decimal Bonus, decimal Deductions, decimal NetPay, int Status, string StatusName, DateTime? PaidAt);
public record SavePayrollDto(int? Id, int EmployeeId, string Period, double OvertimeHours, decimal Bonus, decimal Deductions);
public record ProfitLossDto(decimal Revenue, decimal FoodCost, decimal Expenses, decimal Payroll, decimal NetProfit,
    decimal ProfitMargin, List<CategoryBreakdownDto> ExpenseBreakdown, List<DailyPointDto> RevenueTrend);
public record CategoryBreakdownDto(string Category, decimal Amount, double Percent);
public record DailyPointDto(string Label, decimal Value);

// ---------- Staff ----------
public record ShiftDto(int Id, int EmployeeId, string EmployeeName, DateTime StartTime, DateTime EndTime, int Status, string StatusName, string? Notes);
public record SaveShiftDto(int? Id, int EmployeeId, DateTime StartTime, DateTime EndTime, string? Notes);
public record AttendanceDto(int Id, int EmployeeId, string EmployeeName, DateTime? CheckIn, DateTime? CheckOut, double? HoursWorked, string? Notes);

// ---------- Dashboard ----------
public record DashboardKpiDto(decimal RevenueToday, int OrdersToday, decimal AvgOrderValue, int ActiveOrders,
    int ReservationsToday, int LowStockAlerts, int NewEventInquiries, decimal RevenueMonth, int CustomersCount,
    decimal FoodCostPct, double AvgRating, int PendingReviews);
public record SalesByHourDto(string Hour, decimal Revenue, int Orders);
public record TopItemDto(string Name, int TimesOrdered, decimal Revenue);
public record SalesChartDto(List<DailyPointDto> DailyRevenue, List<SalesByHourDto> Hourly, List<TopItemDto> TopItems,
    List<CategoryBreakdownDto> OrderTypes, List<CategoryBreakdownDto> PaymentMethods);

// ---------- Customers / Reviews / Notifications ----------
public record CustomerDto(int Id, string FullName, string Phone, string? Email, string? Address, int LoyaltyPoints,
    string Tier, decimal TotalSpent, int OrderCount, DateTime? BirthDate, DateTime CreatedAt);
public record SaveCustomerDto(int? Id, string FullName, string Phone, string? Email, string? Address, DateTime? BirthDate);
public record ReviewDto(int Id, int? CustomerId, string CustomerName, int Rating, string? Comment, int Type,
    string TypeName, int Status, string StatusName, string? Reply, DateTime CreatedAt);
public record SaveReviewDto(string CustomerName, int Rating, string? Comment, int Type, int? OrderId);
public record ReplyReviewDto(string Reply);
public record NotificationDto(int Id, string Title, string Message, int Type, string TypeName, bool IsRead, string? Link, DateTime CreatedAt);

// ---------- Settings ----------
public record SettingsDto(int Id, string Name, string? Tagline, string? Address, string? Phone, string? Email,
    string Currency, decimal TaxRate, decimal ServiceChargeRate, decimal DeliveryFee, string OpeningHours,
    int LoyaltyPointsPerDollar, string? FacebookUrl, string? InstagramUrl, bool OnlineOrderingEnabled,
    bool ReservationsEnabled, int MaxGuestsPerReservation, int EstimatedDeliveryMinutes);
