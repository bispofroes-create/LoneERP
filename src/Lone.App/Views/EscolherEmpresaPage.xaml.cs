using Lone.Cliente.ViewModels;

namespace Lone.App.Views;

public partial class EscolherEmpresaPage : ContentPage
{
    public EscolherEmpresaPage(EscolherEmpresaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
