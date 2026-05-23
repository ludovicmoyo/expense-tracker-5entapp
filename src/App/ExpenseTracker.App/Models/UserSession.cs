using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.App.Models;

/// <summary>
/// Snapshot of the signed-in user kept in memory on the client side.
/// Role is the trusted projection of the Cognito groups claim, decoded once at sign-in.
/// </summary>
public sealed record UserSession(
    string Sub,
    string Email,
    string DisplayName,
    UserRole Role,
    string IdToken)
{
    public bool IsEmployee => Role == UserRole.Employee;
    public bool IsFinance => Role == UserRole.FinanceManager;
}
