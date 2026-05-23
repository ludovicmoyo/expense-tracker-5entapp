using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.App.Services;
using ExpenseTracker.Shared.Dtos;
using ExpenseTracker.Shared.Models;
using ExpenseTracker.Shared.Workflow;

namespace ExpenseTracker.App.ViewModels;

[QueryProperty(nameof(ExpenseId), "id")]
public partial class ExpenseDetailViewModel : ViewModelBase
{
    private readonly IExpenseApi _api;
    private readonly ISessionStore _session;

    [ObservableProperty] private string? _expenseId;
    [ObservableProperty] private ExpenseDto? _expense;
    [ObservableProperty] private string? _receiptUrl;

    [ObservableProperty] private bool _canSubmit;
    [ObservableProperty] private bool _canResubmit;
    [ObservableProperty] private bool _canEditReceipt;

    public ExpenseDetailViewModel(IExpenseApi api, ISessionStore session)
    {
        _api = api;
        _session = session;
    }

    partial void OnExpenseIdChanged(string? value) => _ = LoadAsync();

    [RelayCommand]
    public Task LoadAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(ExpenseId)) return;

        Expense = await _api.GetAsync(ExpenseId);
        var role = _session.Current?.Role ?? UserRole.Unknown;
        var isOwner = _session.Current?.Sub == Expense.OwnerSub;

        CanSubmit = ExpenseStateMachine.CanSubmit(Expense.Status, role, isOwner);
        CanResubmit = ExpenseStateMachine.CanResubmit(Expense.Status, role, isOwner);
        CanEditReceipt = ExpenseStateMachine.CanEdit(Expense.Status, role, isOwner);

        ReceiptUrl = null;
        if (Expense.HasReceipt)
        {
            try
            {
                var r = await _api.RequestReceiptUrlAsync(Expense.ExpenseId);
                ReceiptUrl = r.DownloadUrl;
            }
            catch { /* receipt fetch is non-blocking for the detail view */ }
        }
    });

    [RelayCommand]
    private Task SubmitAsync() => RunAsync(async () =>
    {
        if (Expense is null) return;
        await _api.SubmitAsync(Expense.ExpenseId);
        await LoadAsync();
    });

    [RelayCommand]
    private Task PickReceiptAsync() => RunAsync(async () =>
    {
        if (Expense is null) return;

        FileResult? file = null;
        try
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                var chosen = await Application.Current!.Windows[0].Page!.DisplayActionSheetAsync(
                    "Attach receipt", "Cancel", null, "Take Photo", "Choose from Library");
                file = chosen switch
                {
                    "Take Photo" => await MediaPicker.Default.CapturePhotoAsync(),
                    "Choose from Library" => (await MediaPicker.Default.PickPhotosAsync()).FirstOrDefault(),
                    _ => null
                };
            }
            else
            {
                file = (await MediaPicker.Default.PickPhotosAsync()).FirstOrDefault();
            }
        }
        catch (FeatureNotSupportedException)
        {
            file = (await MediaPicker.Default.PickPhotosAsync()).FirstOrDefault();
        }

        if (file is null) return;

        var contentType = string.IsNullOrEmpty(file.ContentType)
            ? GuessContentType(file.FileName)
            : file.ContentType;

        // Same flow as the real S3 path: ask the backend for a pre-signed URL, then PUT to it.
        var url = await _api.RequestUploadUrlAsync(Expense.ExpenseId, contentType);
        await using var stream = await file.OpenReadAsync();
        await _api.UploadReceiptAsync(url.UploadUrl, stream, contentType);

        await LoadAsync();
    });

    private static string GuessContentType(string name)
        => Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".heic" => "image/heic",
            ".pdf" => "application/pdf",
            _ => "image/jpeg"
        };
}
