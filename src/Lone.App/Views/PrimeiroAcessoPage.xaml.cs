using Lone.Cliente.ViewModels;

namespace Lone.App.Views;

public partial class PrimeiroAcessoPage : ContentPage
{
    public PrimeiroAcessoPage(PrimeiroAcessoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
