using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using ExpenseTracker.Shared.Dtos;
using ExpenseTracker.Shared.Workflow;

namespace ExpenseTracker.Lambdas.Common;

public static class ApiGatewayHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Dictionary<string, string> _corsHeaders = new()
    {
        ["Content-Type"] = "application/json",
        ["Access-Control-Allow-Origin"] = "*",
        ["Access-Control-Allow-Headers"] = "Content-Type,Authorization",
        ["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE,OPTIONS"
    };

    public static APIGatewayProxyResponse Ok<T>(T payload)
        => Json(200, payload);

    public static APIGatewayProxyResponse Created<T>(T payload)
        => Json(201, payload);

    public static APIGatewayProxyResponse NoContent()
        => new() { StatusCode = 204, Headers = _corsHeaders, Body = string.Empty };

    public static APIGatewayProxyResponse BadRequest(string code, string message)
        => Json(400, new ErrorResponse { Code = code, Message = message });

    public static APIGatewayProxyResponse Unauthorized(string message)
        => Json(401, new ErrorResponse { Code = "UNAUTHORIZED", Message = message });

    public static APIGatewayProxyResponse Forbidden(string message)
        => Json(403, new ErrorResponse { Code = "FORBIDDEN", Message = message });

    public static APIGatewayProxyResponse NotFound(string message)
        => Json(404, new ErrorResponse { Code = "NOT_FOUND", Message = message });

    public static APIGatewayProxyResponse Conflict(string code, string message)
        => Json(409, new ErrorResponse { Code = code, Message = message });

    public static APIGatewayProxyResponse InternalError(string message)
        => Json(500, new ErrorResponse { Code = "INTERNAL", Message = message });

    public static APIGatewayProxyResponse Json<T>(int statusCode, T payload)
        => new()
        {
            StatusCode = statusCode,
            Headers = _corsHeaders,
            Body = JsonSerializer.Serialize(payload, JsonOptions)
        };

    public static T? DeserializeBody<T>(APIGatewayProxyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Body)) return default;
        try { return JsonSerializer.Deserialize<T>(request.Body, JsonOptions); }
        catch (JsonException ex) { throw new BadRequestException("INVALID_JSON", ex.Message); }
    }

    /// <summary>
    /// Wraps a handler body so every exception we know about turns into a clean HTTP response,
    /// and unknown exceptions get logged + return 500. Avoids duplicating try/catch in every Lambda.
    /// </summary>
    public static string? GetValueOrDefault(this IDictionary<string, string> dict, string key) =>
        dict.TryGetValue(key, out var v) ? v : null;

    public static async Task<APIGatewayProxyResponse> SafeAsync(
        ILambdaContext context,
        Func<Task<APIGatewayProxyResponse>> action)
    {
        try
        {
            return await action();
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Code, ex.Message);
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (ForbiddenException ex)
        {
            return Forbidden(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (WorkflowException ex)
        {
            var status = ex.Code == "FORBIDDEN" ? 403 : 409;
            return Json(status, new ErrorResponse { Code = ex.Code, Message = ex.Message });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Unhandled exception: {ex}");
            return InternalError("An internal error occurred.");
        }
    }
}
