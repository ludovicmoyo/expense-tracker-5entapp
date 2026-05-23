using System.Text.Json.Serialization;

namespace ExpenseTracker.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExpenseStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Resubmitted = 4
}
