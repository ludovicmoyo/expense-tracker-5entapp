using System.Text.Json.Serialization;

namespace ExpenseTracker.Shared.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DecisionKind
{
    Approve = 1,
    Reject = 2
}

public class DecisionRequest
{
    public DecisionKind Decision { get; set; }
    public string? Comment { get; set; }
}
