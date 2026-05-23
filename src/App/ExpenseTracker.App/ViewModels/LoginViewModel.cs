using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.App.Services;
using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.App.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _auth;
    private readonly Func<UserRole, Page> _rootPageFactory;

    [ObservableProperty] private string _email = "alice@demo";
    [ObservableProperty] private string _password = "demo";

    public LoginViewModel(IAuthService auth, Func<UserRole, Page> rootPageFactory)
    {
        _auth = auth;
        _rootPageFactory = rootPageFactory;
    }

    [RelayCommand]
    private Task SignInAsync() => RunAsync(async () =>
    {
        var session = await _auth.SignInAsync(Email, Password);
        Application.Current!.Windows[0].Page = _rootPageFactory(session.Role);
    });
}
