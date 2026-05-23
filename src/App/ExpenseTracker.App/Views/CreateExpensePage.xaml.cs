using ExpenseTracker.App.ViewModels;

namespace ExpenseTracker.App.Views;

public partial class CreateExpensePage : ContentPage
{
    public CreateExpensePage(CreateExpenseViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
