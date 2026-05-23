using ExpenseTracker.App.Services;
using ExpenseTracker.App.Views;

namespace ExpenseTracker.App.Shells;

public partial class FinanceShell : Shell
{
    private readonly IAuthService _auth;
    private readonly IServiceProvider _services;

    public FinanceShell(IAuthService auth, IServiceProvider services)
    {
        InitializeComponent();
        _auth = auth;
        _services = services;

        Routing.RegisterRoute(nameof(DecisionPage), typeof(DecisionPage));

        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Sign out",
            Command = new Command(async () =>
            {
                await _auth.SignOutAsync();
                Application.Current!.Windows[0].Page = _services.GetRequiredService<LoginPage>();
            })
        });
    }
}
