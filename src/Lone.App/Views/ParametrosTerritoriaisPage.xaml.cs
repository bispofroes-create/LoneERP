using Lone.Cliente.ViewModels.Territorios;

namespace Lone.App.Views;

public partial class ParametrosTerritoriaisPage : ContentPage
{
    private readonly ParametrosTerritoriaisViewModel _viewModel;

    public ParametrosTerritoriaisPage(ParametrosTerritoriaisViewModel viewModel)
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
