using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.App.Services;
using ExpenseTracker.App.Views;
using ExpenseTracker.Shared.Dtos;

namespace ExpenseTracker.App.ViewModels;

public partial class MyExpensesViewModel : ViewModelBase
{
    private readonly IExpenseApi _api;

    public ObservableCollection<ExpenseDto> Items { get; } = new();

    [ObservableProperty] private bool _isRefreshing;

    public MyExpensesViewModel(IExpenseApi api) => _api = api;

    [RelayCommand]
    public Task LoadAsync() => RunAsync(async () =>
    {
        var list = await _api.ListMyExpensesAsync();
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
        => Shell.Current.GoToAsync($"{nameof(ExpenseDetailPage)}?id={item.ExpenseId}");

    [RelayCommand]
    private Task CreateAsync()
        => Shell.Current.GoToAsync($"{nameof(CreateExpensePage)}");
}
