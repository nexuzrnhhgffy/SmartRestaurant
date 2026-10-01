using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Domain.Entities;

/// <summary>Chart of accounts (Iranian standard coded chart).</summary>
public class Account : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    /// <summary>1 Asset, 2 Liability, 3 Equity, 4 Revenue, 5 Expense</summary>
    public int Group { get; set; }
    public bool IsLeaf { get; set; } = true;
}

public class JournalEntry : BaseEntity, IBranchScoped
{
    public string EntryNumber { get; set; } = default!;
    public DateTime PostedAt { get; set; }
    public string Description { get; set; } = default!;
    public string? SourceType { get; set; }                  // Sale / Expense / Manual / StockWaste
    public Guid? SourceId { get; set; }
    public Guid? BranchId { get; set; }
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
    public decimal TotalDebit => Lines?.Sum(l => l.Debit) ?? 0;
    public decimal TotalCredit => Lines?.Sum(l => l.Credit) ?? 0;
}

public class JournalLine : BaseEntity
{
    public Guid JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Memo { get; set; }
}

public class Expense : BaseEntity, IBranchScoped
{
    public string Title { get; set; } = default!;
    public string? Category { get; set; }                    // Rent / Salary / Utilities / Supplies
    public decimal Amount { get; set; }
    public DateTime SpentAt { get; set; }
    public string? PaidTo { get; set; }
    public string? Note { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? BranchId { get; set; }
}
