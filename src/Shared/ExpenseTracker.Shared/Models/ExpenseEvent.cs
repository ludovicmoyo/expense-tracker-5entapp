namespace ExpenseTracker.Shared.Models;

public class ExpenseEvent
{
    public required string ExpenseId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public ExpenseStatus FromStatus { get; set; }
    public ExpenseStatus ToStatus { get; set; }
    public ExpenseAction Action { get; set; }
    public required string ActorSub { get; set; }
    public UserRole ActorRole { get; set; }
    public string? Comment { get; set; }
}
