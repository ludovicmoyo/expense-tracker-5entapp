using ExpenseTracker.Shared.Dtos;

namespace ExpenseTracker.App.Services;

/// <summary>
/// Stable API contract used by ViewModels. Two implementations:
///   - InMemoryMockApi  : runs entirely on-device, no AWS required (UI development + demo fallback).
///   - RealExpenseApi   : talks to API Gateway with the Cognito JWT in Authorization header.
/// Both implementations share the same DTOs from ExpenseTracker.Shared, so ViewModels
/// never know which one is wired in MauiProgram.cs.
/// </summary>
public interface IExpenseApi
{
    // Employee
    Task<IReadOnlyList<ExpenseDto>> ListMyExpensesAsync(CancellationToken ct = default);
    Task<ExpenseDto> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default);
    Task<ExpenseDto> SubmitAsync(string expenseId, CancellationToken ct = default);

    // Finance
    Task<IReadOnlyList<ExpenseDto>> ListQueueAsync(CancellationToken ct = default);
    Task<ExpenseDto> DecideAsync(string expenseId, string ownerSub, DecisionRequest request, CancellationToken ct = default);

    // Shared
    Task<ExpenseDto> GetAsync(string expenseId, string? ownerSub = null, CancellationToken ct = default);

    // Receipts — pre-signed URL flow (mock returns file:// URLs, real returns S3 https URLs).
    Task<UploadUrlResponse> RequestUploadUrlAsync(string expenseId, string contentType, CancellationToken ct = default);
    Task UploadReceiptAsync(string uploadUrl, Stream content, string contentType, CancellationToken ct = default);
    Task<ReceiptUrlResponse> RequestReceiptUrlAsync(string expenseId, string? ownerSub = null, CancellationToken ct = default);
}

public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }

    public ApiException(int statusCode, string code, string message) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}
