using ExpenseTracker.App.Models;

namespace ExpenseTracker.App.Services;

public interface IAuthService
{
    Task<UserSession> SignInAsync(string email, string password, CancellationToken ct = default);
    Task SignOutAsync(CancellationToken ct = default);
}

public sealed class AuthException : Exception
{
    public AuthException(string message) : base(message) { }
}
