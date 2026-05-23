using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using ExpenseTracker.Lambdas.Common;
using ExpenseTracker.Shared.Dtos;
using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.Lambdas.Functions;

/// <summary>
/// GET /expenses/queue  —  Finance Manager lists the queue.
/// By default returns Submitted + Resubmitted (everything awaiting decision).
/// Optional query string: ?status=Submitted or ?status=Resubmitted to filter.
/// </summary>
public class ListSubmittedQueueFunction
{
    public Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
        => ApiGatewayHelpers.SafeAsync(context, async () =>
        {
            var user = UserContext.From(request);
            if (!user.IsFinance)
                return ApiGatewayHelpers.Forbidden("Only finance managers can read the queue.");

            var statusFilter = request.QueryStringParameters is { } q
                               && q.TryGetValue("status", out var raw)
                               && Enum.TryParse<ExpenseStatus>(raw, ignoreCase: true, out var parsed)
                ? parsed
                : (ExpenseStatus?)null;

            List<ExpenseDto> result = new();

            if (statusFilter is null || statusFilter == ExpenseStatus.Submitted)
            {
                var items = await ServiceFactory.Repository.ListByStatusAsync(ExpenseStatus.Submitted);
                result.AddRange(items.Select(ExpenseDto.FromDomain));
            }
            if (statusFilter is null || statusFilter == ExpenseStatus.Resubmitted)
            {
                var items = await ServiceFactory.Repository.ListByStatusAsync(ExpenseStatus.Resubmitted);
                result.AddRange(items.Select(ExpenseDto.FromDomain));
            }

            return ApiGatewayHelpers.Ok(result.OrderBy(r => r.SubmittedAt).ToList());
        });
}
