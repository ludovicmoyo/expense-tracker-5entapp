using ExpenseTracker.App.ViewModels;

namespace ExpenseTracker.App.Views;

public partial class MyExpensesPage : ContentPage
{
    private readonly MyExpensesViewModel _vm;

    public MyExpensesPage(MyExpensesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
