using ExpenseTracker.App.ViewModels;

namespace ExpenseTracker.App.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
