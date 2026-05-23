using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ExpenseTracker.Shared.Dtos;

namespace ExpenseTracker.App.Services;

public sealed record ApiConfig(string BaseUrl);

/// <summary>
/// HTTP-backed implementation that talks to API Gateway. Selected over the mock
/// in <see cref="MauiProgram"/> once AWS is provisioned in Step 4.
/// The HttpClient is configured with <see cref="AuthorizingHttpHandler"/> which
/// attaches the Cognito IdToken from the active <see cref="ISessionStore"/>.
/// </summary>
public sealed class RealExpenseApi : IExpenseApi
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public RealExpenseApi(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<ExpenseDto>> ListMyExpensesAsync(CancellationToken ct = default)
        => await GetJsonAsync<List<ExpenseDto>>("expenses", ct) ?? new();

    public async Task<IReadOnlyList<ExpenseDto>> ListQueueAsync(CancellationToken ct = default)
        => await GetJsonAsync<List<ExpenseDto>>("expenses/queue", ct) ?? new();

    public Task<ExpenseDto> GetAsync(string expenseId, string? ownerSub = null, CancellationToken ct = default)
    {
        var url = ownerSub is null
            ? $"expenses/{Uri.EscapeDataString(expenseId)}"
            : $"expenses/{Uri.EscapeDataString(expenseId)}?ownerSub={Uri.EscapeDataString(ownerSub)}";
        return GetJsonAsync<ExpenseDto>(url, ct)!;
    }

    public Task<ExpenseDto> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default)
        => PostJsonAsync<CreateExpenseRequest, ExpenseDto>("expenses", request, ct);

    public Task<ExpenseDto> SubmitAsync(string expenseId, CancellationToken ct = default)
        => PostJsonAsync<object, ExpenseDto>($"expenses/{Uri.EscapeDataString(expenseId)}/submit", new { }, ct);

    public Task<ExpenseDto> DecideAsync(string expenseId, string ownerSub, DecisionRequest request, CancellationToken ct = default)
        => PostJsonAsync<DecisionRequest, ExpenseDto>(
            $"expenses/{Uri.EscapeDataString(expenseId)}/decision?ownerSub={Uri.EscapeDataString(ownerSub)}",
            request, ct);

    public Task<UploadUrlResponse> RequestUploadUrlAsync(string expenseId, string contentType, CancellationToken ct = default)
        => PostJsonAsync<UploadUrlRequest, UploadUrlResponse>(
            $"expenses/{Uri.EscapeDataString(expenseId)}/receipt-upload-url",
            new UploadUrlRequest { ContentType = contentType }, ct);

    public async Task UploadReceiptAsync(string uploadUrl, Stream content, string contentType, CancellationToken ct = default)
    {
        // Direct PUT to S3 — does NOT go through API Gateway, so we use a fresh
        // HttpClient with no auth header (the pre-signed URL embeds its own credentials).
        using var raw = new HttpClient();
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var resp = await raw.PutAsync(uploadUrl, streamContent, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new ApiException((int)resp.StatusCode, "UPLOAD_FAILED",
                $"S3 PUT failed: {resp.StatusCode} {body}");
        }
    }

    public Task<ReceiptUrlResponse> RequestReceiptUrlAsync(string expenseId, string? ownerSub = null, CancellationToken ct = default)
    {
        var url = ownerSub is null
            ? $"expenses/{Uri.EscapeDataString(expenseId)}/receipt-url"
            : $"expenses/{Uri.EscapeDataString(expenseId)}/receipt-url?ownerSub={Uri.EscapeDataString(ownerSub)}";
        return GetJsonAsync<ReceiptUrlResponse>(url, ct)!;
    }

    // -----------------------------------------------------------------------
    // HTTP helpers
    // -----------------------------------------------------------------------

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, ct);
        return await ReadAsync<T>(resp, ct);
    }

    private async Task<TRes> PostJsonAsync<TReq, TRes>(string url, TReq payload, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync(url, payload, _json, ct);
        return (await ReadAsync<TRes>(resp, ct))!;
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode)
            return resp.StatusCode == HttpStatusCode.NoContent
                ? default
                : await resp.Content.ReadFromJsonAsync<T>(_json, ct);

        var raw = await resp.Content.ReadAsStringAsync(ct);
        ErrorResponse? err = null;
        try { err = JsonSerializer.Deserialize<ErrorResponse>(raw, _json); } catch { /* non-JSON */ }
        throw new ApiException(
            (int)resp.StatusCode,
            err?.Code ?? resp.StatusCode.ToString(),
            err?.Message ?? raw);
    }
}
