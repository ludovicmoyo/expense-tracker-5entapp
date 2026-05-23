namespace ExpenseTracker.Shared.Dtos;

public class ReceiptUrlResponse
{
    public required string DownloadUrl { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
