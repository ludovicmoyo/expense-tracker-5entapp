using ExpenseTracker.App.ViewModels;

namespace ExpenseTracker.App.Views;

public partial class ExpenseDetailPage : ContentPage
{
    public ExpenseDetailPage(ExpenseDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
