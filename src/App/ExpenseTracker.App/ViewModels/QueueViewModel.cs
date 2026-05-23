using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.App.Services;
using ExpenseTracker.App.Views;
using ExpenseTracker.Shared.Dtos;

namespace ExpenseTracker.App.ViewModels;

public partial class QueueViewModel : ViewModelBase
{
    private readonly IExpenseApi _api;

    public ObservableCollection<ExpenseDto> Items { get; } = new();

    [ObservableProperty] private bool _isRefreshing;

    public QueueViewModel(IExpenseApi api) => _api = api;

    [RelayCommand]
    public Task LoadAsync() => RunAsync(async () =>
    {
        var list = await _api.ListQueueAsync();
        Items.Clear();
        foreach (var item in list) Items.Add(item);
    });

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try { await LoadAsync(); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private Task OpenAsync(ExpenseDto item)
        => Shell.Current.GoToAsync(
            $"{nameof(DecisionPage)}?id={item.ExpenseId}&ownerSub={item.OwnerSub}");
}
