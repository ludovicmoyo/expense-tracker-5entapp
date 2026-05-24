using Amazon.DynamoDBv2;
using Amazon.S3;

namespace ExpenseTracker.Lambdas.Common;

/// <summary>
/// Static, lazy-initialized service container. Each Lambda function class consumes these
/// via property access; the AWS SDK clients are created once per cold start and reused
/// across invocations (warm container).
/// </summary>
public static class ServiceFactory
{
    public static string TableName { get; } =
        Environment.GetEnvironmentVariable("TABLE_NAME")
        ?? throw new InvalidOperationException("Missing TABLE_NAME env var.");

    public static string ReceiptsBucket { get; } =
        Environment.GetEnvironmentVariable("RECEIPTS_BUCKET")
        ?? throw new InvalidOperationException("Missing RECEIPTS_BUCKET env var.");

    public static TimeSpan PresignedUrlTtl { get; } =
        TimeSpan.FromMinutes(int.TryParse(
            Environment.GetEnvironmentVariable("PRESIGNED_URL_TTL_MINUTES"),
            out var m) ? m : 15);

    private static readonly Amazon.RegionEndpoint _dynamoRegion =
        Amazon.RegionEndpoint.GetBySystemName(
            Environment.GetEnvironmentVariable("AWS_REGION") ?? "eu-north-1");

    // S3_REGION must match the bucket's actual region (separate from Lambda runtime region).
    private static readonly Amazon.RegionEndpoint _s3Region =
        Amazon.RegionEndpoint.GetBySystemName(
            Environment.GetEnvironmentVariable("S3_REGION") ?? "us-east-1");

    private static readonly Lazy<IAmazonDynamoDB> _dynamo = new(() => new AmazonDynamoDBClient(_dynamoRegion));
    private static readonly Lazy<IAmazonS3> _s3 = new(() => new AmazonS3Client(_s3Region));

    public static IAmazonDynamoDB Dynamo => _dynamo.Value;
    public static IAmazonS3 S3 => _s3.Value;

    public static ExpenseRepository Repository { get; } = new(Dynamo, TableName);
    public static ReceiptStorage Receipts { get; } = new(S3, ReceiptsBucket, PresignedUrlTtl);
}
