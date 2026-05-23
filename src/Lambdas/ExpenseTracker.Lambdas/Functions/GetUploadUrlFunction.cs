using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using ExpenseTracker.Lambdas.Common;
using ExpenseTracker.Shared.Dtos;
using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.Lambdas.Functions;

/// <summary>
/// POST /expenses/{id}/receipt-upload-url  —  Employee asks for a short-lived S3 PUT URL.
/// The bucket has no public access; the URL is the ONLY way to deposit a receipt.
/// Only the owner can request one, and only while the expense is editable (Draft or Rejected).
/// </summary>
public class GetUploadUrlFunction
{
    private static readonly HashSet<string> _allowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/heic", "application/pdf"
    };

    public Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
        => ApiGatewayHelpers.SafeAsync(context, async () =>
        {
            var user = UserContext.From(request);
            if (!user.IsEmployee)
                return ApiGatewayHelpers.Forbidden("Only employees can upload receipts.");

            var expenseId = request.PathParameters?.GetValueOrDefault("id")
                ?? throw new BadRequestException("MISSING_ID", "Path parameter 'id' is required.");

            var body = ApiGatewayHelpers.DeserializeBody<UploadUrlRequest>(request)
                       ?? new UploadUrlRequest();
            var contentType = string.IsNullOrWhiteSpace(body.ContentType) ? "image/jpeg" : body.ContentType;
            if (!_allowedContentTypes.Contains(contentType))
                throw new BadRequestException("UNSUPPORTED_CONTENT_TYPE",
                    $"Content type '{contentType}' is not allowed.");

            var expense = await ServiceFactory.Repository.GetAsync(user.Sub, expenseId)
                          ?? throw new NotFoundException($"Expense '{expenseId}' not found.");

            if (expense.Status is not (ExpenseStatus.Draft or ExpenseStatus.Rejected))
                throw new BadRequestException("NOT_EDITABLE",
                    $"Cannot attach a receipt while status is '{expense.Status}'.");

            var s3Key = ReceiptStorage.BuildKey(user.Sub, expenseId, contentType);
            var (url, expiresAt) = ServiceFactory.Receipts.GetUploadUrl(s3Key, contentType);

            // Persist the eventual key now so a subsequent submit can verify presence
            // without needing a HEAD on S3. The client uploads to this exact key.
            expense.ReceiptS3Key = s3Key;
            expense.UpdatedAt = DateTimeOffset.UtcNow;
            await ServiceFactory.Repository.PutAsync(expense);

            return ApiGatewayHelpers.Ok(new UploadUrlResponse
            {
                UploadUrl = url,
                S3Key = s3Key,
                ExpiresAt = expiresAt
            });
        });
}
