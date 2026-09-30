using Lone.Cliente.ViewModels.Territorios;

namespace Lone.App.Views;

public partial class DivergenciasTerritoriaisPage : ContentPage
{
    private readonly DivergenciasTerritoriaisViewModel _viewModel;
    private bool _carregado;

    public DivergenciasTerritoriaisPage(DivergenciasTerritoriaisViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        viewModel.AbrirTela = rota => Shell.Current.GoToAsync("//" + rota);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_carregado) return;
        _carregado = true;
        _viewModel.CarregarCommand.Execute(null);
    }
}
