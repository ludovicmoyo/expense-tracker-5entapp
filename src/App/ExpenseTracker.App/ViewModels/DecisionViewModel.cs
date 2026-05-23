using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.App.Services;
using ExpenseTracker.Shared.Dtos;
using ExpenseTracker.Shared.Models;
using ExpenseTracker.Shared.Workflow;

namespace ExpenseTracker.App.ViewModels;

[QueryProperty(nameof(ExpenseId), "id")]
[QueryProperty(nameof(OwnerSub), "ownerSub")]
public partial class DecisionViewModel : ViewModelBase
{
    private readonly IExpenseApi _api;
    private readonly ISessionStore _session;

    [ObservableProperty] private string? _expenseId;
    [ObservableProperty] private string? _ownerSub;
    [ObservableProperty] private ExpenseDto? _expense;
    [ObservableProperty] private string? _receiptUrl;

    [ObservableProperty] private string _comment = string.Empty;

    [ObservableProperty] private bool _canApprove;
    [ObservableProperty] private bool _canReject;

    public DecisionViewModel(IExpenseApi api, ISessionStore session)
    {
        _api = api;
        _session = session;
    }

    partial void OnExpenseIdChanged(string? value) => _ = LoadAsync();
    partial void OnOwnerSubChanged(string? value) => _ = LoadAsync();

    [RelayCommand]
    public Task LoadAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(ExpenseId) || string.IsNullOrWhiteSpace(OwnerSub)) return;

        Expense = await _api.GetAsync(ExpenseId, OwnerSub);
        var role = _session.Current?.Role ?? UserRole.Unknown;

        CanApprove = ExpenseStateMachine.CanApprove(Expense.Status, role);
        CanReject = ExpenseStateMachine.CanReject(Expense.Status, role);

        ReceiptUrl = null;
        if (Expense.HasReceipt)
        {
            try
            {
                var r = await _api.RequestReceiptUrlAsync(Expense.ExpenseId, OwnerSub);
                ReceiptUrl = r.DownloadUrl;
            }
            catch { /* non-blocking */ }
        }
    });

    [RelayCommand]
    private Task ApproveAsync() => DecideAsync(DecisionKind.Approve);

    [RelayCommand]
    private Task RejectAsync() => DecideAsync(DecisionKind.Reject);

    private Task DecideAsync(DecisionKind kind) => RunAsync(async () =>
    {
        if (Expense is null || string.IsNullOrWhiteSpace(OwnerSub)) return;

        // Mirror the rules the server enforces, so we fail fast with a clear message
        // instead of round-tripping for an obvious error.
        if (kind == DecisionKind.Reject && string.IsNullOrWhiteSpace(Comment))
            throw new InvalidOperationException("A comment is required to reject an expense.");
        if (kind == DecisionKind.Approve
            && ApprovalPolicy.RequiresSeniorApproval(Expense.Amount)
            && string.IsNullOrWhiteSpace(Comment))
            throw new InvalidOperationException(
                $"Amounts above {ApprovalPolicy.SeniorApprovalThreshold} € require a written justification when approving.");

        await _api.DecideAsync(Expense.ExpenseId, OwnerSub, new DecisionRequest
        {
            Decision = kind,
            Comment = string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim()
        });

        await Shell.Current.GoToAsync("..");
    });
}
