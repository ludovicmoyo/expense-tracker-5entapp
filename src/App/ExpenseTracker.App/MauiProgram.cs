using CommunityToolkit.Maui;
using ExpenseTracker.App.Services;
using ExpenseTracker.App.Shells;
using ExpenseTracker.App.ViewModels;
using ExpenseTracker.App.Views;
using ExpenseTracker.Shared.Models;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        RegisterServices(builder.Services);
        RegisterViewModelsAndPages(builder.Services);
        RegisterRootPageFactory(builder.Services);

        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ISessionStore, InMemorySessionStore>();

        services.AddSingleton(new CognitoConfig(
            Region: "eu-north-1",
            UserPoolId: "eu-north-1_5Gh4yACKF",
            ClientId: "4ib3c040qo917etv22j9sic0jv"));
        services.AddSingleton(new ApiConfig(BaseUrl: "https://aqervpkypa.execute-api.eu-north-1.amazonaws.com/dev/"));
        services.AddSingleton<IAuthService, CognitoAuthService>();

        services.AddTransient<AuthorizingHttpHandler>();
        services.AddHttpClient<IExpenseApi, RealExpenseApi>((sp, http) =>
        {
            var cfg = sp.GetRequiredService<ApiConfig>();
            http.BaseAddress = new Uri(cfg.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(30);
        }).AddHttpMessageHandler<AuthorizingHttpHandler>();
    }

    private static void RegisterViewModelsAndPages(IServiceCollection services)
    {
        // Authentication flow
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginPage>();

        // Employee
        services.AddTransient<MyExpensesViewModel>();
        services.AddTransient<MyExpensesPage>();
        services.AddTransient<CreateExpenseViewModel>();
        services.AddTransient<CreateExpensePage>();
        services.AddTransient<ExpenseDetailViewModel>();
        services.AddTransient<ExpenseDetailPage>();
        services.AddTransient<EmployeeShell>();

        // Finance
        services.AddTransient<QueueViewModel>();
        services.AddTransient<QueuePage>();
        services.AddTransient<DecisionViewModel>();
        services.AddTransient<DecisionPage>();
        services.AddTransient<FinanceShell>();
    }

    private static void RegisterRootPageFactory(IServiceCollection services)
    {
        // LoginViewModel calls this with the resolved role to install the right Shell.
        // Centralizes the role-to-Shell mapping in one place.
        services.AddSingleton<Func<UserRole, Page>>(sp => role =>
            role switch
            {
                UserRole.Employee => sp.GetRequiredService<EmployeeShell>(),
                UserRole.FinanceManager => sp.GetRequiredService<FinanceShell>(),
                _ => throw new InvalidOperationException($"No root page for role '{role}'.")
            });
    }
}
