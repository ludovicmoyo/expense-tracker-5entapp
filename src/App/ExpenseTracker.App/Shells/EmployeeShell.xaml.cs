using ExpenseTracker.App.Services;
using ExpenseTracker.App.Views;

namespace ExpenseTracker.App.Shells;

public partial class EmployeeShell : Shell
{
    private readonly IAuthService _auth;
    private readonly IServiceProvider _services;

    public EmployeeShell(IAuthService auth, IServiceProvider services)
    {
        InitializeComponent();
        _auth = auth;
        _services = services;

        // Register navigable detail routes (Shell needs them mapped to types).
        Routing.RegisterRoute(nameof(CreateExpensePage), typeof(CreateExpensePage));
        Routing.RegisterRoute(nameof(ExpenseDetailPage), typeof(ExpenseDetailPage));

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
