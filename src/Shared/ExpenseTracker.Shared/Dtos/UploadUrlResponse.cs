namespace ExpenseTracker.Shared.Dtos;

public class UploadUrlRequest
{
    public string ContentType { get; set; } = "image/jpeg";
}

public class UploadUrlResponse
{
    public required string UploadUrl { get; set; }
    public required string S3Key { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
