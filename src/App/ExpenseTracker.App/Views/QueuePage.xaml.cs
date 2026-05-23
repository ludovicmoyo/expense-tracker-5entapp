using ExpenseTracker.App.ViewModels;

namespace ExpenseTracker.App.Views;

public partial class QueuePage : ContentPage
{
    private readonly QueueViewModel _vm;

    public QueuePage(QueueViewModel vm)
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
