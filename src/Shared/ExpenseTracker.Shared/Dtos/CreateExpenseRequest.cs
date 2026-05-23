using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.Shared.Dtos;

public class CreateExpenseRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
}
