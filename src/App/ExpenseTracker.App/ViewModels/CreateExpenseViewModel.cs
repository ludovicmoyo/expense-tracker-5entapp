using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.App.Services;
using ExpenseTracker.Shared.Dtos;
using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.App.ViewModels;

public partial class CreateExpenseViewModel : ViewModelBase
{
    private readonly IExpenseApi _api;

    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string _currency = "EUR";
    [ObservableProperty] private ExpenseCategory _category = ExpenseCategory.Meals;
    [ObservableProperty] private string _description = string.Empty;

    public IReadOnlyList<ExpenseCategory> Categories { get; } =
        Enum.GetValues<ExpenseCategory>();

    public CreateExpenseViewModel(IExpenseApi api) => _api = api;

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (Amount <= 0)
            throw new InvalidOperationException("Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(Description))
            throw new InvalidOperationException("Description is required.");

        await _api.CreateAsync(new CreateExpenseRequest
        {
            Amount = Amount,
            Currency = string.IsNullOrWhiteSpace(Currency) ? "EUR" : Currency,
            Category = Category,
            Description = Description
        });

        await Shell.Current.GoToAsync("..");   // back to list
    });

    [RelayCommand]
    private Task CancelAsync() => Shell.Current.GoToAsync("..");
}
