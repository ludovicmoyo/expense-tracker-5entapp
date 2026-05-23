using ExpenseTracker.App.ViewModels;

namespace ExpenseTracker.App.Views;

public partial class DecisionPage : ContentPage
{
    public DecisionPage(DecisionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
