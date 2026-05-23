using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.Extensions.CognitoAuthentication;
using Amazon.Runtime;
using ExpenseTracker.App.Models;
using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.App.Services;

public sealed class CognitoAuthService : IAuthService
{
    private readonly ISessionStore _store;
    private readonly CognitoConfig _config;

    public CognitoAuthService(ISessionStore store, CognitoConfig config)
    {
        _store = store;
        _config = config;
    }

    public async Task<UserSession> SignInAsync(string email, string password, CancellationToken ct = default)
    {
        var region = RegionEndpoint.GetBySystemName(_config.Region);
        var provider = new AmazonCognitoIdentityProviderClient(new AnonymousAWSCredentials(), region);
        var pool = new CognitoUserPool(_config.UserPoolId, _config.ClientId, provider);
        var user = new CognitoUser(email, _config.ClientId, pool, provider);

        try
        {
            var authResponse = await user.StartWithSrpAuthAsync(
                new InitiateSrpAuthRequest { Password = password }).ConfigureAwait(false);

            var authResult = authResponse.AuthenticationResult
                ?? throw new AuthException(
                    authResponse.ChallengeName == "NEW_PASSWORD_REQUIRED"
                        ? "Password change required. Set a permanent password in the AWS console for this user."
                        : $"Unexpected auth challenge: {authResponse.ChallengeName}");

            var idToken = authResult.IdToken;
            var claims = DecodeJwtPayload(idToken);

            var sub = GetString(claims, "sub") ?? email;
            var displayName = GetString(claims, "email") ?? email;
            var role = ResolveRole(claims);

            var session = new UserSession(sub, email, displayName, role, idToken);
            _store.Set(session);
            return session;
        }
        catch (Exception ex) when (ex is not AuthException)
        {
            throw new AuthException(ex.Message);
        }
    }

    public Task SignOutAsync(CancellationToken ct = default)
    {
        _store.Set(null);
        return Task.CompletedTask;
    }

    private static UserRole ResolveRole(Dictionary<string, JsonElement> claims)
    {
        if (!claims.TryGetValue("cognito:groups", out var groupsEl))
            return UserRole.Employee;

        if (groupsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var g in groupsEl.EnumerateArray())
                if (g.GetString() == "finance")
                    return UserRole.FinanceManager;
        }

        return UserRole.Employee;
    }

    private static string? GetString(Dictionary<string, JsonElement> claims, string key) =>
        claims.TryGetValue(key, out var el) ? el.GetString() : null;

    private static Dictionary<string, JsonElement> DecodeJwtPayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
            throw new AuthException("Invalid token format.");

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload += (payload.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
               ?? [];
    }
}

public sealed record CognitoConfig(string Region, string UserPoolId, string ClientId);
