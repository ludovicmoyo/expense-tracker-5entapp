using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.Lambdas.Common;

public sealed class ExpenseRepository
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _tableName;

    public ExpenseRepository(IAmazonDynamoDB dynamo, string tableName)
    {
        _dynamo = dynamo;
        _tableName = tableName;
    }

    public async Task PutAsync(Expense expense, CancellationToken ct = default)
    {
        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = DynamoMapper.ToItem(expense)
        }, ct);
    }

    public async Task<Expense?> GetAsync(string ownerSub, string expenseId, CancellationToken ct = default)
    {
        var resp = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = DynamoMapper.UserPk(ownerSub) },
                ["SK"] = new() { S = DynamoMapper.ExpenseSk(expenseId) }
            },
            ConsistentRead = true
        }, ct);

        return resp.IsItemSet ? DynamoMapper.FromItem(resp.Item) : null;
    }

    /// <summary>
    /// Lists all expenses belonging to a single user.
    /// One Query on the table partition — O(items) for that user, no scan.
    /// </summary>
    public async Task<IReadOnlyList<Expense>> ListByOwnerAsync(string ownerSub, CancellationToken ct = default)
    {
        var resp = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = DynamoMapper.UserPk(ownerSub) },
                [":skPrefix"] = new() { S = "EXPENSE#" }
            },
            ScanIndexForward = false   // newest first
        }, ct);

        return resp.Items.Select(DynamoMapper.FromItem).ToList();
    }

    /// <summary>
    /// Finance queue: all expenses in a given status, sorted by submission time.
    /// Uses GSI1, which is sparse → contains only Submitted/Resubmitted items.
    /// </summary>
    public async Task<IReadOnlyList<Expense>> ListByStatusAsync(ExpenseStatus status, CancellationToken ct = default)
    {
        var resp = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = DynamoMapper.StatusGsi1Pk(status) }
            },
            ScanIndexForward = true    // oldest submission first → FIFO queue
        }, ct);

        return resp.Items.Select(DynamoMapper.FromItem).ToList();
    }
}
