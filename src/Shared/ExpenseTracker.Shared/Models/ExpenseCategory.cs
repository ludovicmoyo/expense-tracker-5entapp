using System.Text.Json.Serialization;

namespace ExpenseTracker.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExpenseCategory
{
    Travel = 0,
    Meals = 1,
    Accommodation = 2,
    Supplies = 3,
    Software = 4,
    Training = 5,
    Other = 99
}
