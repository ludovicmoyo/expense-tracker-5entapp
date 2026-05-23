using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using ExpenseTracker.Lambdas.Common;
using ExpenseTracker.Shared.Dtos;
using ExpenseTracker.Shared.Models;
using ExpenseTracker.Shared.Workflow;

namespace ExpenseTracker.Lambdas.Functions;

/// <summary>
/// POST /expenses/{id}/decision  —  Finance Manager approves or rejects.
/// Body: { decision: "Approve" | "Reject", comment?: string }
/// Reject requires a non-empty comment (enforced by ExpenseStateMachine).
/// Caller must provide ?ownerSub=... (same convention as GetExpense).
/// </summary>
public class DecideExpenseFunction
{
    public Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
        => ApiGatewayHelpers.SafeAsync(context, async () =>
        {
            var user = UserContext.From(request);
            if (!user.IsFinance)
                return ApiGatewayHelpers.Forbidden("Only finance managers can decide.");

            var expenseId = request.PathParameters?.GetValueOrDefault("id")
                ?? throw new BadRequestException("MISSING_ID", "Path parameter 'id' is required.");

            var ownerSub = request.QueryStringParameters?.GetValueOrDefault("ownerSub")
                ?? throw new BadRequestException("MISSING_OWNER", "Query 'ownerSub' is required.");

            var body = ApiGatewayHelpers.DeserializeBody<DecisionRequest>(request)
                       ?? throw new BadRequestException("INVALID_BODY", "Request body is required.");

            var action = body.Decision switch
            {
                DecisionKind.Approve => ExpenseAction.Approve,
                DecisionKind.Reject => ExpenseAction.Reject,
                _ => throw new BadRequestException("INVALID_DECISION", $"Unknown decision '{body.Decision}'.")
            };

            var expense = await ServiceFactory.Repository.GetAsync(ownerSub, expenseId)
                          ?? throw new NotFoundException($"Expense '{expenseId}' not found.");

            // 1) Lifecycle rule: state transition + base rules (Reject needs a comment, etc.)
            var nextStatus = ExpenseStateMachine.EnsureTransition(
                expense.Status, action, user.Role, isOwner: false, comment: body.Comment);

            // 2) Monetary policy: amounts above the threshold need a written justification
            //    even when approving — addresses the "missing approval threshold" scenario.
            ApprovalPolicy.EnsureDecisionComplies(expense.Amount, action, body.Comment);

            var now = DateTimeOffset.UtcNow;
            expense.Status = nextStatus;
            expense.DecisionAt = now;
            expense.DecisionBy = user.Sub;
            expense.DecisionComment = string.IsNullOrWhiteSpace(body.Comment) ? null : body.Comment.Trim();
            expense.UpdatedAt = now;

            await ServiceFactory.Repository.PutAsync(expense);
            return ApiGatewayHelpers.Ok(ExpenseDto.FromDomain(expense));
        });
}
