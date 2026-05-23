using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using ExpenseTracker.Lambdas.Common;
using ExpenseTracker.Shared.Dtos;
using ExpenseTracker.Shared.Models;
using ExpenseTracker.Shared.Workflow;

namespace ExpenseTracker.Lambdas.Functions;

/// <summary>
/// POST /expenses/{id}/submit  —  Employee transitions the expense forward.
///   Draft     → Submitted
///   Rejected  → Resubmitted
/// Server enforces the state machine; the request body is not required.
/// </summary>
public class SubmitExpenseFunction
{
    public Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
        => ApiGatewayHelpers.SafeAsync(context, async () =>
        {
            var user = UserContext.From(request);
            if (!user.IsEmployee)
                return ApiGatewayHelpers.Forbidden("Only employees can submit.");

            var expenseId = request.PathParameters?.GetValueOrDefault("id")
                ?? throw new BadRequestException("MISSING_ID", "Path parameter 'id' is required.");

            var expense = await ServiceFactory.Repository.GetAsync(user.Sub, expenseId)
                          ?? throw new NotFoundException($"Expense '{expenseId}' not found.");

            if (string.IsNullOrEmpty(expense.ReceiptS3Key))
                throw new BadRequestException("RECEIPT_REQUIRED",
                    "A receipt must be uploaded before submitting.");

            // Decide which action applies based on the current state.
            var action = expense.Status switch
            {
                ExpenseStatus.Draft => ExpenseAction.Submit,
                ExpenseStatus.Rejected => ExpenseAction.Resubmit,
                _ => throw new WorkflowException("INVALID_TRANSITION",
                    $"Cannot submit from status '{expense.Status}'.")
            };

            var nextStatus = ExpenseStateMachine.EnsureTransition(
                expense.Status, action, user.Role, isOwner: true);

            var now = DateTimeOffset.UtcNow;
            expense.Status = nextStatus;
            expense.SubmittedAt = now;
            expense.UpdatedAt = now;
            // Resubmitting clears the previous decision metadata; keep audit in event log only.
            if (action == ExpenseAction.Resubmit)
            {
                expense.DecisionAt = null;
                expense.DecisionBy = null;
                expense.DecisionComment = null;
            }

            await ServiceFactory.Repository.PutAsync(expense);
            return ApiGatewayHelpers.Ok(ExpenseDto.FromDomain(expense));
        });
}
