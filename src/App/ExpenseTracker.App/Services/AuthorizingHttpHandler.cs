using System.Net.Http.Headers;

namespace ExpenseTracker.App.Services;

/// <summary>
/// Attaches the active user's Cognito IdToken as a Bearer header on every outgoing
/// request to API Gateway. Reads the session at send-time, so sign-in/out is picked
/// up without needing to rebuild the HttpClient.
/// </summary>
public sealed class AuthorizingHttpHandler : DelegatingHandler
{
    private readonly ISessionStore _session;

    public AuthorizingHttpHandler(ISessionStore session) => _session = session;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _session.Current?.IdToken;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
