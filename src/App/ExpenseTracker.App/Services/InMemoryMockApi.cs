using System.Collections.Concurrent;
using ExpenseTracker.Shared.Dtos;
using ExpenseTracker.Shared.Models;
using ExpenseTracker.Shared.Workflow;

namespace ExpenseTracker.App.Services;

/// <summary>
/// On-device mock backend. Re-implements the exact same RBAC + state machine rules as
/// the Lambdas (via the shared <see cref="ExpenseStateMachine"/> and <see cref="ApprovalPolicy"/>),
/// so behaviour is identical whether the app is wired to mock or to the real API.
///
/// Receipts are written to <see cref="FileSystem.CacheDirectory"/> so MAUI's Image control
/// can load them via a file:// URI — mirroring the S3 pre-signed-URL flow without any AWS.
/// </summary>
public sealed class InMemoryMockApi : IExpenseApi
{
    private readonly ISessionStore _session;
    private readonly ConcurrentDictionary<string, Expense> _expenses = new();
    private readonly string _receiptsDir;
    private int _seeded;

    public InMemoryMockApi(ISessionStore session)
    {
        _session = session;
        _receiptsDir = Path.Combine(FileSystem.CacheDirectory, "mock-receipts");
        Directory.CreateDirectory(_receiptsDir);
    }

    // -----------------------------------------------------------------------
    // Auth helpers
    // -----------------------------------------------------------------------

    private (string sub, UserRole role) RequireUser()
    {
        var s = _session.Current
            ?? throw new ApiException(401, "UNAUTHORIZED", "Not signed in.");
        return (s.Sub, s.Role);
    }

    private void EnsureSeeded(string forSub)
    {
        if (Interlocked.Exchange(ref _seeded, 1) != 0) return;

        var now = DateTimeOffset.UtcNow;
        Seed(new Expense
        {
            ExpenseId = NewId(),
            OwnerSub = "00000000-0000-0000-0000-000000000001",
            OwnerDisplayName = "Alice (Employee)",
            Amount = 42.50m, Currency = "EUR",
            Category = ExpenseCategory.Meals,
            Description = "Lunch with client — Café de Paris",
            Status = ExpenseStatus.Draft,
            CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-3),
        });
        Seed(new Expense
        {
            ExpenseId = NewId(),
            OwnerSub = "00000000-0000-0000-0000-000000000001",
            OwnerDisplayName = "Alice (Employee)",
            Amount = 189.00m, Currency = "EUR",
            Category = ExpenseCategory.Travel,
            Description = "Train Paris → Marseille",
            Status = ExpenseStatus.Submitted,
            ReceiptS3Key = "demo/alice-train.png",
            CreatedAt = now.AddDays(-6), UpdatedAt = now.AddDays(-2), SubmittedAt = now.AddDays(-2),
        });
        Seed(new Expense
        {
            ExpenseId = NewId(),
            OwnerSub = "00000000-0000-0000-0000-000000000001",
            OwnerDisplayName = "Alice (Employee)",
            Amount = 1_200.00m, Currency = "EUR",            // > 500 → senior approval flag
            Category = ExpenseCategory.Training,
            Description = "International conference — registration fee",
            Status = ExpenseStatus.Submitted,
            ReceiptS3Key = "demo/alice-conf.png",
            CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1), SubmittedAt = now.AddDays(-1),
        });
        Seed(new Expense
        {
            ExpenseId = NewId(),
            OwnerSub = "00000000-0000-0000-0000-000000000001",
            OwnerDisplayName = "Alice (Employee)",
            Amount = 76.30m, Currency = "EUR",
            Category = ExpenseCategory.Supplies,
            Description = "Office chair cushion",
            Status = ExpenseStatus.Rejected,
            ReceiptS3Key = "demo/alice-chair.png",
            CreatedAt = now.AddDays(-10), UpdatedAt = now.AddDays(-7), SubmittedAt = now.AddDays(-9),
            DecisionAt = now.AddDays(-7),
            DecisionBy = "00000000-0000-0000-0000-000000000002",
            DecisionComment = "Receipt is unreadable — please re-photograph and resubmit.",
        });
        Seed(new Expense
        {
            ExpenseId = NewId(),
            OwnerSub = "00000000-0000-0000-0000-000000000003",
            OwnerDisplayName = "Charlie (Employee)",
            Amount = 320.00m, Currency = "EUR",
            Category = ExpenseCategory.Accommodation,
            Description = "Hotel night — client visit Lyon",
            Status = ExpenseStatus.Submitted,
            ReceiptS3Key = "demo/charlie-hotel.png",
            CreatedAt = now.AddDays(-4), UpdatedAt = now.AddDays(-4), SubmittedAt = now.AddDays(-4),
        });

        _ = forSub;
    }

    private void Seed(Expense e) => _expenses[e.ExpenseId] = e;
    private static string NewId() => Guid.NewGuid().ToString("N");

    // -----------------------------------------------------------------------
    // Employee endpoints
    // -----------------------------------------------------------------------

    public Task<IReadOnlyList<ExpenseDto>> ListMyExpensesAsync(CancellationToken ct = default)
    {
        var (sub, role) = RequireUser();
        if (role != UserRole.Employee) throw new ApiException(403, "FORBIDDEN", "Only employees.");
        EnsureSeeded(sub);

        IReadOnlyList<ExpenseDto> list = _expenses.Values
            .Where(e => e.OwnerSub == sub)
            .OrderByDescending(e => e.UpdatedAt)
            .Select(ExpenseDto.FromDomain)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<ExpenseDto> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default)
    {
        var (sub, role) = RequireUser();
        if (role != UserRole.Employee) throw new ApiException(403, "FORBIDDEN", "Only employees.");
        if (request.Amount <= 0)
            throw new ApiException(400, "INVALID_AMOUNT", "Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ApiException(400, "INVALID_DESCRIPTION", "Description is required.");

        var now = DateTimeOffset.UtcNow;
        var expense = new Expense
        {
            ExpenseId = NewId(),
            OwnerSub = sub,
            OwnerDisplayName = _session.Current?.DisplayName,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency.ToUpperInvariant(),
            Category = request.Category,
            Description = request.Description.Trim(),
            Status = ExpenseStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _expenses[expense.ExpenseId] = expense;
        return Task.FromResult(ExpenseDto.FromDomain(expense));
    }

    public Task<ExpenseDto> SubmitAsync(string expenseId, CancellationToken ct = default)
    {
        var (sub, role) = RequireUser();
        var e = GetOrThrow(expenseId);
        if (e.OwnerSub != sub)
            throw new ApiException(403, "FORBIDDEN", "Not the owner.");
        if (string.IsNullOrEmpty(e.ReceiptS3Key))
            throw new ApiException(400, "RECEIPT_REQUIRED",
                "A receipt must be uploaded before submitting.");

        var action = e.Status switch
        {
            ExpenseStatus.Draft => ExpenseAction.Submit,
            ExpenseStatus.Rejected => ExpenseAction.Resubmit,
            _ => throw new ApiException(409, "INVALID_TRANSITION",
                $"Cannot submit from status '{e.Status}'.")
        };

        try
        {
            e.Status = ExpenseStateMachine.EnsureTransition(e.Status, action, role, isOwner: true);
        }
        catch (WorkflowException wx) { throw new ApiException(409, wx.Code, wx.Message); }

        var now = DateTimeOffset.UtcNow;
        e.SubmittedAt = now;
        e.UpdatedAt = now;
        if (action == ExpenseAction.Resubmit)
        {
            e.DecisionAt = null;
            e.DecisionBy = null;
            e.DecisionComment = null;
        }
        return Task.FromResult(ExpenseDto.FromDomain(e));
    }

    // -----------------------------------------------------------------------
    // Finance endpoints
    // -----------------------------------------------------------------------

    public Task<IReadOnlyList<ExpenseDto>> ListQueueAsync(CancellationToken ct = default)
    {
        var (sub, role) = RequireUser();
        if (role != UserRole.FinanceManager) throw new ApiException(403, "FORBIDDEN", "Only finance.");
        EnsureSeeded(sub);

        IReadOnlyList<ExpenseDto> list = _expenses.Values
            .Where(e => e.Status is ExpenseStatus.Submitted or ExpenseStatus.Resubmitted)
            .OrderBy(e => e.SubmittedAt ?? DateTimeOffset.MaxValue)
            .Select(ExpenseDto.FromDomain)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<ExpenseDto> DecideAsync(string expenseId, string ownerSub, DecisionRequest request, CancellationToken ct = default)
    {
        var (sub, role) = RequireUser();
        if (role != UserRole.FinanceManager) throw new ApiException(403, "FORBIDDEN", "Only finance.");

        var e = GetOrThrow(expenseId);
        if (e.OwnerSub != ownerSub)
            throw new ApiException(404, "NOT_FOUND", "Expense not found for this owner.");

        var action = request.Decision switch
        {
            DecisionKind.Approve => ExpenseAction.Approve,
            DecisionKind.Reject => ExpenseAction.Reject,
            _ => throw new ApiException(400, "INVALID_DECISION", $"Unknown decision '{request.Decision}'.")
        };

        try
        {
            e.Status = ExpenseStateMachine.EnsureTransition(e.Status, action, role, isOwner: false, comment: request.Comment);
            ApprovalPolicy.EnsureDecisionComplies(e.Amount, action, request.Comment);
        }
        catch (WorkflowException wx)
        {
            var status = wx.Code == "FORBIDDEN" ? 403 : 409;
            throw new ApiException(status, wx.Code, wx.Message);
        }

        var now = DateTimeOffset.UtcNow;
        e.DecisionAt = now;
        e.DecisionBy = sub;
        e.DecisionComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        e.UpdatedAt = now;
        return Task.FromResult(ExpenseDto.FromDomain(e));
    }

    // -----------------------------------------------------------------------
    // Shared endpoints
    // -----------------------------------------------------------------------

    public Task<ExpenseDto> GetAsync(string expenseId, string? ownerSub = null, CancellationToken ct = default)
    {
        var (sub, role) = RequireUser();
        var e = GetOrThrow(expenseId);

        if (role == UserRole.Employee && e.OwnerSub != sub)
            throw new ApiException(403, "FORBIDDEN", "Not the owner.");
        if (role == UserRole.FinanceManager && ownerSub is not null && e.OwnerSub != ownerSub)
            throw new ApiException(404, "NOT_FOUND", "Expense not found for this owner.");

        return Task.FromResult(ExpenseDto.FromDomain(e));
    }

    // -----------------------------------------------------------------------
    // Receipts: emulate pre-signed PUT/GET URLs with local file:// URIs.
    // -----------------------------------------------------------------------

    public Task<UploadUrlResponse> RequestUploadUrlAsync(string expenseId, string contentType, CancellationToken ct = default)
    {
        var (sub, role) = RequireUser();
        if (role != UserRole.Employee) throw new ApiException(403, "FORBIDDEN", "Only employees.");

        var e = GetOrThrow(expenseId);
        if (e.OwnerSub != sub) throw new ApiException(403, "FORBIDDEN", "Not the owner.");
        if (e.Status is not (ExpenseStatus.Draft or ExpenseStatus.Rejected))
            throw new ApiException(400, "NOT_EDITABLE",
                $"Cannot attach a receipt while status is '{e.Status}'.");

        var ext = contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/heic" => "heic",
            "application/pdf" => "pdf",
            _ => throw new ApiException(400, "UNSUPPORTED_CONTENT_TYPE",
                $"Content type '{contentType}' not allowed.")
        };

        var s3Key = $"receipts/{sub}/{expenseId}.{ext}";
        var localPath = Path.Combine(_receiptsDir, $"{expenseId}.{ext}");
        // The "upload URL" carries the destination file path so UploadReceiptAsync
        // can write to disk without holding extra state.
        var uploadUrl = new Uri(localPath).AbsoluteUri;

        e.ReceiptS3Key = s3Key;
        e.UpdatedAt = DateTimeOffset.UtcNow;

        return Task.FromResult(new UploadUrlResponse
        {
            UploadUrl = uploadUrl,
            S3Key = s3Key,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        });
    }

    public async Task UploadReceiptAsync(string uploadUrl, Stream content, string contentType, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(uploadUrl, UriKind.Absolute, out var uri) || !uri.IsFile)
            throw new ApiException(400, "INVALID_UPLOAD_URL", "Mock upload URL must be a file:// URI.");

        await using var fs = File.Create(uri.LocalPath);
        await content.CopyToAsync(fs, ct);
    }

    public Task<ReceiptUrlResponse> RequestReceiptUrlAsync(string expenseId, string? ownerSub = null, CancellationToken ct = default)
    {
        var (sub, role) = RequireUser();
        var e = GetOrThrow(expenseId);

        if (role == UserRole.Employee && e.OwnerSub != sub)
            throw new ApiException(403, "FORBIDDEN", "Not the owner.");
        if (role == UserRole.FinanceManager && ownerSub is not null && e.OwnerSub != ownerSub)
            throw new ApiException(404, "NOT_FOUND", "Expense not found for this owner.");
        if (string.IsNullOrEmpty(e.ReceiptS3Key))
            throw new ApiException(404, "NO_RECEIPT", "No receipt attached.");

        var fileName = Path.GetFileName(e.ReceiptS3Key);
        var localPath = Path.Combine(_receiptsDir, fileName);
        if (!File.Exists(localPath))
        {
            // Pre-seeded items reference receipts we never actually shipped;
            // generate a tiny placeholder so the Image control has something to render.
            File.WriteAllBytes(localPath, PlaceholderPng);
        }

        return Task.FromResult(new ReceiptUrlResponse
        {
            DownloadUrl = new Uri(localPath).AbsoluteUri,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        });
    }

    private Expense GetOrThrow(string id)
        => _expenses.TryGetValue(id, out var e)
            ? e
            : throw new ApiException(404, "NOT_FOUND", $"Expense '{id}' not found.");

    // 1×1 grey PNG, used as placeholder for seeded receipts that have no real file behind them.
    private static readonly byte[] PlaceholderPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0x99, 0x63, 0x60, 0x60, 0x60, 0x00,
        0x00, 0x00, 0x04, 0x00, 0x01, 0x5C, 0xCD, 0xFF, 0x69, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
        0x44, 0xAE, 0x42, 0x60, 0x82
    };
}
