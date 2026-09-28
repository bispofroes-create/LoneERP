using Lone.Cliente.ViewModels.Comercial;

namespace Lone.App.Views;

public partial class ParametrosComerciaisPage : ContentPage
{
    private readonly ParametrosComerciaisViewModel _viewModel;

    public ParametrosComerciaisPage(ParametrosComerciaisViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.CarregarCommand.Execute(null);
    }
}
