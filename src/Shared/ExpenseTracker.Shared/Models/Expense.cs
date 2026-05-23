namespace ExpenseTracker.Shared.Models;

public class Expense
{
    public required string ExpenseId { get; set; }
    public required string OwnerSub { get; set; }
    public string? OwnerDisplayName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;
    public string? ReceiptS3Key { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
    public string? DecisionBy { get; set; }
    public string? DecisionComment { get; set; }
}
