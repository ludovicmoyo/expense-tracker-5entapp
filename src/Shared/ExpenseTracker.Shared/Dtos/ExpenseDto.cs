using ExpenseTracker.Shared.Models;
using ExpenseTracker.Shared.Workflow;

namespace ExpenseTracker.Shared.Dtos;

public class ExpenseDto
{
    public required string ExpenseId { get; set; }
    public required string OwnerSub { get; set; }
    public string? OwnerDisplayName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public ExpenseStatus Status { get; set; }
    public bool HasReceipt { get; set; }
    public bool RequiresSeniorApproval { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
    public string? DecisionBy { get; set; }
    public string? DecisionComment { get; set; }

    public static ExpenseDto FromDomain(Expense e) => new()
    {
        ExpenseId = e.ExpenseId,
        OwnerSub = e.OwnerSub,
        OwnerDisplayName = e.OwnerDisplayName,
        Amount = e.Amount,
        Currency = e.Currency,
        Category = e.Category,
        Description = e.Description,
        Status = e.Status,
        HasReceipt = !string.IsNullOrEmpty(e.ReceiptS3Key),
        RequiresSeniorApproval = ApprovalPolicy.RequiresSeniorApproval(e.Amount),
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        SubmittedAt = e.SubmittedAt,
        DecisionAt = e.DecisionAt,
        DecisionBy = e.DecisionBy,
        DecisionComment = e.DecisionComment
    };
}
