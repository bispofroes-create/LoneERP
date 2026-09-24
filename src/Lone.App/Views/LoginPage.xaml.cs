using Lone.Cliente.ViewModels;

namespace Lone.App.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = ViewModel = viewModel;
    }

    public LoginViewModel ViewModel { get; }
}
