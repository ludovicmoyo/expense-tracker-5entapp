using System.Globalization;
using Amazon.DynamoDBv2.Model;
using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.Lambdas.Common;

/// <summary>
/// Centralizes the single-table key shape so it can't drift across functions.
///
///   Expense item        PK = USER#{ownerSub}    SK = EXPENSE#{expenseId}
///   GSI1 (Finance queue, sparse)
///       set only when Status ∈ {Submitted, Resubmitted}:
///       GSI1PK = STATUS#{status}                GSI1SK = {submittedAt}#{expenseId}
///   GSI2 (Decider audit, sparse)
///       set only when Status ∈ {Approved, Rejected}:
///       GSI2PK = DECIDER#{deciderSub}           GSI2SK = {decisionAt}#{expenseId}
/// </summary>
public static class DynamoMapper
{
    public static string UserPk(string ownerSub) => $"USER#{ownerSub}";
    public static string ExpenseSk(string expenseId) => $"EXPENSE#{expenseId}";
    public static string StatusGsi1Pk(ExpenseStatus s) => $"STATUS#{s}";
    public static string DeciderGsi2Pk(string deciderSub) => $"DECIDER#{deciderSub}";

    public static string IsoTimestamp(DateTimeOffset dto)
        => dto.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    public static Dictionary<string, AttributeValue> ToItem(Expense e)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = S(UserPk(e.OwnerSub)),
            ["SK"] = S(ExpenseSk(e.ExpenseId)),
            ["expenseId"] = S(e.ExpenseId),
            ["ownerSub"] = S(e.OwnerSub),
            ["amount"] = N(e.Amount),
            ["currency"] = S(e.Currency),
            ["category"] = S(e.Category.ToString()),
            ["description"] = S(e.Description),
            ["status"] = S(e.Status.ToString()),
            ["createdAt"] = S(IsoTimestamp(e.CreatedAt)),
            ["updatedAt"] = S(IsoTimestamp(e.UpdatedAt)),
        };

        if (!string.IsNullOrEmpty(e.OwnerDisplayName))
            item["ownerDisplayName"] = S(e.OwnerDisplayName);

        if (!string.IsNullOrEmpty(e.ReceiptS3Key))
            item["receiptS3Key"] = S(e.ReceiptS3Key);

        if (e.SubmittedAt is { } sub)
            item["submittedAt"] = S(IsoTimestamp(sub));

        if (e.DecisionAt is { } dec)
            item["decisionAt"] = S(IsoTimestamp(dec));

        if (!string.IsNullOrEmpty(e.DecisionBy))
            item["decisionBy"] = S(e.DecisionBy);

        if (!string.IsNullOrEmpty(e.DecisionComment))
            item["decisionComment"] = S(e.DecisionComment);

        // Sparse GSI1: only items currently awaiting Finance attention.
        if (e.Status is ExpenseStatus.Submitted or ExpenseStatus.Resubmitted && e.SubmittedAt.HasValue)
        {
            item["GSI1PK"] = S(StatusGsi1Pk(e.Status));
            item["GSI1SK"] = S($"{IsoTimestamp(e.SubmittedAt.Value)}#{e.ExpenseId}");
        }

        // Sparse GSI2: audit of decisions by Finance Manager.
        if (e.Status is ExpenseStatus.Approved or ExpenseStatus.Rejected
            && e.DecisionAt.HasValue
            && !string.IsNullOrEmpty(e.DecisionBy))
        {
            item["GSI2PK"] = S(DeciderGsi2Pk(e.DecisionBy));
            item["GSI2SK"] = S($"{IsoTimestamp(e.DecisionAt.Value)}#{e.ExpenseId}");
        }

        return item;
    }

    public static Expense FromItem(Dictionary<string, AttributeValue> item)
    {
        var e = new Expense
        {
            ExpenseId = GetString(item, "expenseId"),
            OwnerSub = GetString(item, "ownerSub"),
            OwnerDisplayName = GetStringOrNull(item, "ownerDisplayName"),
            Amount = GetDecimal(item, "amount"),
            Currency = GetStringOrNull(item, "currency") ?? "EUR",
            Category = Enum.Parse<ExpenseCategory>(GetString(item, "category")),
            Description = GetStringOrNull(item, "description") ?? string.Empty,
            Status = Enum.Parse<ExpenseStatus>(GetString(item, "status")),
            ReceiptS3Key = GetStringOrNull(item, "receiptS3Key"),
            CreatedAt = GetTimestamp(item, "createdAt"),
            UpdatedAt = GetTimestamp(item, "updatedAt"),
            SubmittedAt = GetTimestampOrNull(item, "submittedAt"),
            DecisionAt = GetTimestampOrNull(item, "decisionAt"),
            DecisionBy = GetStringOrNull(item, "decisionBy"),
            DecisionComment = GetStringOrNull(item, "decisionComment"),
        };
        return e;
    }

    private static AttributeValue S(string v) => new() { S = v };
    private static AttributeValue N(decimal v) => new() { N = v.ToString(CultureInfo.InvariantCulture) };

    private static string GetString(Dictionary<string, AttributeValue> item, string key)
        => item.TryGetValue(key, out var v) && v.S is not null
            ? v.S
            : throw new InvalidOperationException($"Missing attribute '{key}'.");

    private static string? GetStringOrNull(Dictionary<string, AttributeValue> item, string key)
        => item.TryGetValue(key, out var v) ? v.S : null;

    private static decimal GetDecimal(Dictionary<string, AttributeValue> item, string key)
        => item.TryGetValue(key, out var v) && v.N is not null
            ? decimal.Parse(v.N, CultureInfo.InvariantCulture)
            : throw new InvalidOperationException($"Missing numeric attribute '{key}'.");

    private static DateTimeOffset GetTimestamp(Dictionary<string, AttributeValue> item, string key)
        => DateTimeOffset.Parse(GetString(item, key), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

    private static DateTimeOffset? GetTimestampOrNull(Dictionary<string, AttributeValue> item, string key)
        => GetStringOrNull(item, key) is { } s
            ? DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;
}
