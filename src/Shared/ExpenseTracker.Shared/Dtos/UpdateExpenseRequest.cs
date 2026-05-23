using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.Shared.Dtos;

public class UpdateExpenseRequest
{
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public ExpenseCategory? Category { get; set; }
    public string? Description { get; set; }
}
