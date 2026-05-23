using Amazon.Lambda.APIGatewayEvents;
using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.Lambdas.Common;

/// <summary>
/// Snapshot of the caller, derived strictly from the Cognito-validated JWT claims
/// injected by API Gateway. NEVER trust values from the request body for authn/authz.
/// </summary>
public sealed record UserContext(string Sub, string? Email, UserRole Role)
{
    public bool IsEmployee => Role == UserRole.Employee;
    public bool IsFinance => Role == UserRole.FinanceManager;

    public static UserContext From(APIGatewayProxyRequest request)
    {
        var claims = request.RequestContext?.Authorizer?.Claims
                     ?? throw new UnauthorizedException("Missing authorizer claims.");

        if (!claims.TryGetValue("sub", out var sub) || string.IsNullOrWhiteSpace(sub))
            throw new UnauthorizedException("Missing 'sub' claim.");

        claims.TryGetValue("email", out var email);

        var groups = ExtractGroups(claims);
        var role = CognitoGroups.FromGroups(groups);
        if (role == UserRole.Unknown)
            throw new ForbiddenException("User belongs to no recognized group.");

        return new UserContext(sub, email, role);
    }

    private static IEnumerable<string> ExtractGroups(IDictionary<string, string> claims)
    {
        // Cognito injects 'cognito:groups' as either a JSON-array string ("[a, b]")
        // or a comma/space-separated string depending on integration. Handle both.
        if (!claims.TryGetValue("cognito:groups", out var raw) || string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        raw = raw.Trim();
        if (raw.StartsWith('[') && raw.EndsWith(']'))
            raw = raw[1..^1];

        return raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries
                                            | StringSplitOptions.TrimEntries);
    }
}

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}

public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public sealed class BadRequestException : Exception
{
    public string Code { get; }
    public BadRequestException(string code, string message) : base(message) { Code = code; }
}
