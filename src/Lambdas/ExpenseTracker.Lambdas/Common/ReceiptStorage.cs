using Amazon.S3;
using Amazon.S3.Model;

namespace ExpenseTracker.Lambdas.Common;

public sealed class ReceiptStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly TimeSpan _ttl;

    public ReceiptStorage(IAmazonS3 s3, string bucket, TimeSpan ttl)
    {
        _s3 = s3;
        _bucket = bucket;
        _ttl = ttl;
    }

    public static string BuildKey(string ownerSub, string expenseId, string contentType)
    {
        var ext = contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/heic" => "heic",
            "application/pdf" => "pdf",
            _ => "bin"
        };
        return $"receipts/{ownerSub}/{expenseId}.{ext}";
    }

    public (string url, DateTimeOffset expiresAt) GetUploadUrl(string s3Key, string contentType)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(_ttl);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = s3Key,
            Verb = HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            ContentType = contentType,
            Protocol = Protocol.HTTPS
        };
        return (_s3.GetPreSignedURL(request), expiresAt);
    }

    public (string url, DateTimeOffset expiresAt) GetDownloadUrl(string s3Key)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(_ttl);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = s3Key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,
            Protocol = Protocol.HTTPS
        };
        return (_s3.GetPreSignedURL(request), expiresAt);
    }
}
