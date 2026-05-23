using System.Text.Json.Serialization;

namespace ExpenseTracker.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExpenseAction
{
    Submit = 1,
    Approve = 2,
    Reject = 3,
    Resubmit = 4
}
