using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using ExpenseTracker.Lambdas.Common;
using ExpenseTracker.Shared.Dtos;

namespace ExpenseTracker.Lambdas.Functions;

/// <summary>
/// GET /expenses/{id}
///   - Employees see their own expenses (ownerSub = caller).
///   - Finance Managers must provide ?ownerSub=... (taken from the queue listing they came from).
///
/// Avoids adding a third GSI just for cross-owner lookups; the queue already exposes ownerSub.
/// </summary>
public class GetExpenseFunction
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
                    ?? throw new BadRequestException("MISSING_OWNER", "Query 'ownerSub' is required for finance access.");
            }
            else
            {
                return ApiGatewayHelpers.Forbidden("Unknown role.");
            }

            var expense = await ServiceFactory.Repository.GetAsync(ownerSub, expenseId)
                          ?? throw new NotFoundException($"Expense '{expenseId}' not found.");

            return ApiGatewayHelpers.Ok(ExpenseDto.FromDomain(expense));
        });
}
