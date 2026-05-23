using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using ExpenseTracker.Lambdas.Common;
using ExpenseTracker.Shared.Dtos;

namespace ExpenseTracker.Lambdas.Functions;

/// <summary>
/// GET /expenses/{id}/receipt-url  —  Returns a short-lived S3 GET URL for the receipt.
/// Authorization:
///   - Employee: must be the owner.
///   - Finance Manager: must pass ?ownerSub=... (came from the queue listing).
/// The S3 bucket itself blocks public access; this URL is the only way in.
/// </summary>
public class GetReceiptUrlFunction
{
    public Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
        => ApiGatewayHelpers.SafeAsync(context, async () =>
        {
            var user = UserContext.From(request);

            var expenseId = request.PathParameters?.GetValueOrDefault("id")
                ?? throw new BadRequestException("MISSING_ID", "Path parameter 'id' is required.");

            string ownerSub;
            if (user.IsEmployee)
            {
                ownerSub = user.Sub;
            }
            else if (user.IsFinance)
            {
                ownerSub = request.QueryStringParameters?.GetValueOrDefault("ownerSub")
                    ?? throw new BadRequestException("MISSING_OWNER", "Query 'ownerSub' is required.");
            }
            else
            {
                return ApiGatewayHelpers.Forbidden("Unknown role.");
            }

            var expense = await ServiceFactory.Repository.GetAsync(ownerSub, expenseId)
                          ?? throw new NotFoundException($"Expense '{expenseId}' not found.");

            if (string.IsNullOrEmpty(expense.ReceiptS3Key))
                throw new NotFoundException("No receipt attached to this expense.");

            var (url, expiresAt) = ServiceFactory.Receipts.GetDownloadUrl(expense.ReceiptS3Key);
            return ApiGatewayHelpers.Ok(new ReceiptUrlResponse
            {
                DownloadUrl = url,
                ExpiresAt = expiresAt
            });
        });
}
