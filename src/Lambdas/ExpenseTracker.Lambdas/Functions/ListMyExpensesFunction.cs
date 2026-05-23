using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using ExpenseTracker.Lambdas.Common;
using ExpenseTracker.Shared.Dtos;

namespace ExpenseTracker.Lambdas.Functions;

/// <summary>
/// GET /expenses  —  Employee lists their own expenses (all statuses, newest first).
/// </summary>
public class ListMyExpensesFunction
{
    public Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
        => ApiGatewayHelpers.SafeAsync(context, async () =>
        {
            var user = UserContext.From(request);
            if (!user.IsEmployee)
                return ApiGatewayHelpers.Forbidden("Only employees can list their own expenses.");

            var items = await ServiceFactory.Repository.ListByOwnerAsync(user.Sub);
            return ApiGatewayHelpers.Ok(items.Select(ExpenseDto.FromDomain).ToList());
        });
}
