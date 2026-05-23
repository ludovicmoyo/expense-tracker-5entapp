namespace ExpenseTracker.Shared.Dtos;

public class ErrorResponse
{
    public required string Code { get; set; }
    public required string Message { get; set; }
}
