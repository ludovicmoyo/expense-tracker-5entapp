using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using ExpenseTracker.Lambdas.Common;
using ExpenseTracker.Shared.Dtos;
using ExpenseTracker.Shared.Models;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace ExpenseTracker.Lambdas.Functions;

/// <summary>
/// POST /expenses  —  Employee creates a new Draft expense.
/// The Status is forced to Draft server-side; any value in the request body is ignored.
/// </summary>
public class CreateExpenseFunction
{
    public Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
        => ApiGatewayHelpers.SafeAsync(context, async () =>
        {
            var user = UserContext.From(request);
            if (!user.IsEmployee)
                return ApiGatewayHelpers.Forbidden("Only employees can create expenses.");

            var dto = ApiGatewayHelpers.DeserializeBody<CreateExpenseRequest>(request)
                      ?? throw new BadRequestException("INVALID_BODY", "Request body is required.");

            if (dto.Amount <= 0)
                throw new BadRequestException("INVALID_AMOUNT", "Amount must be greater than zero.");
            if (string.IsNullOrWhiteSpace(dto.Description))
                throw new BadRequestException("INVALID_DESCRIPTION", "Description is required.");

            var now = DateTimeOffset.UtcNow;
            var expense = new Expense
            {
                ExpenseId = Guid.NewGuid().ToString("N"),
                OwnerSub = user.Sub,
                OwnerDisplayName = user.Email,
                Amount = dto.Amount,
                Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "EUR" : dto.Currency.ToUpperInvariant(),
                Category = dto.Category,
                Description = dto.Description.Trim(),
                Status = ExpenseStatus.Draft,
                CreatedAt = now,
                UpdatedAt = now
            };

            await ServiceFactory.Repository.PutAsync(expense);
            return ApiGatewayHelpers.Created(ExpenseDto.FromDomain(expense));
        });
}
