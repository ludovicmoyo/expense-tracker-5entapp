using ExpenseTracker.App.Models;

namespace ExpenseTracker.App.Services;

public interface ISessionStore
{
    UserSession? Current { get; }
    event EventHandler<UserSession?>? SessionChanged;
    void Set(UserSession? session);
}

/// <summary>
/// Minimal in-process session holder. JWT is kept only in RAM for the demo;
/// a production app would also persist refresh tokens in SecureStorage and
/// rehydrate on launch. Out of scope for this academic project.
/// </summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private UserSession? _current;
    public UserSession? Current => _current;
    public event EventHandler<UserSession?>? SessionChanged;

    public void Set(UserSession? session)
    {
        _current = session;
        SessionChanged?.Invoke(this, session);
    }
}
