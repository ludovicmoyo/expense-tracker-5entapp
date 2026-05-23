using System.Text.Json.Serialization;

namespace ExpenseTracker.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    Unknown = 0,
    Employee = 1,
    FinanceManager = 2
}

public static class CognitoGroups
{
    public const string Employees = "employees";
    public const string Finance = "finance";

    public static UserRole FromGroups(IEnumerable<string>? groups)
    {
        if (groups is null) return UserRole.Unknown;

        var set = new HashSet<string>(groups, StringComparer.OrdinalIgnoreCase);
        if (set.Contains(Finance)) return UserRole.FinanceManager;
        if (set.Contains(Employees)) return UserRole.Employee;
        return UserRole.Unknown;
    }
}
