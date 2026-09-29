using SmartRestaurant.Domain.Entities.Common;

namespace SmartRestaurant.Domain.Entities.Accounting;

public enum ExpenseCategory
{
    Rent = 1, Utilities = 2, Salaries = 3, Supplies = 4,
    Marketing = 5, Maintenance = 6, Taxes = 7, Insurance = 8, Misc = 9
}
public enum PayrollStatus { Pending = 1, Approved = 2, Paid = 3 }

public class Expense : BaseEntity
{
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Misc;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? PaidTo { get; set; }
    public Orders.PaymentMethod Method { get; set; } = Orders.PaymentMethod.Cash;
    public string? ReceiptRef { get; set; }
    public int? CreatedById { get; set; }
    public Identity.AppUser? CreatedBy { get; set; }
}

public class Payroll : BaseEntity
{
    public int EmployeeId { get; set; }
    public Identity.AppUser? Employee { get; set; }
    public string Period { get; set; } = string.Empty; // "2026-09"
    public decimal BaseSalary { get; set; }
    public double OvertimeHours { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal Bonus { get; set; }
    public decimal Deductions { get; set; }
    public decimal NetPay { get; set; }
    public PayrollStatus Status { get; set; } = PayrollStatus.Pending;
    public DateTime? PaidAt { get; set; }
}
