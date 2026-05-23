using ExpenseTracker.App.Models;
using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.App.Services;

/// <summary>
/// Local demo authentication. Replaces Cognito during development so we can run
/// the UI on the MacBook without any AWS plumbing. Two hard-coded accounts cover
/// the two roles required by the brief.
/// </summary>
public sealed class MockAuthService : IAuthService
{
    private readonly ISessionStore _store;

    private record MockUser(string Sub, string Email, string Password, string DisplayName, UserRole Role);

    private static readonly MockUser[] _users =
    {
        new("00000000-0000-0000-0000-000000000001", "alice@demo",   "demo", "Alice (Employee)",  UserRole.Employee),
        new("00000000-0000-0000-0000-000000000002", "bob@demo",     "demo", "Bob (Finance)",     UserRole.FinanceManager),
        new("00000000-0000-0000-0000-000000000003", "charlie@demo", "demo", "Charlie (Employee)", UserRole.Employee),
    };

    public MockAuthService(ISessionStore store) => _store = store;

    public Task<UserSession> SignInAsync(string email, string password, CancellationToken ct = default)
    {
        var user = _users.FirstOrDefault(u =>
            string.Equals(u.Email, email?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user is null || user.Password != password)
            throw new AuthException("Invalid email or password.");

        // Fake "JWT" — in mock mode no one validates it. The real CognitoAuthService
        // returns a genuine IdToken to be sent as Authorization: Bearer ...
        var fakeJwt = $"mock.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(user.Sub))}.signature";

        var session = new UserSession(user.Sub, user.Email, user.DisplayName, user.Role, fakeJwt);
        _store.Set(session);
        return Task.FromResult(session);
    }

    public Task SignOutAsync(CancellationToken ct = default)
    {
        _store.Set(null);
        return Task.CompletedTask;
    }
}
