using Lone.App.Controles;
using Lone.Cliente.ViewModels.Territorios;

namespace Lone.App.Views;

/// <summary>Só aparência: arrumação lista/ficha conforme a largura.</summary>
public partial class MapasTerritoriaisPage : ContentPage
{
    private readonly MapasTerritoriaisViewModel _viewModel;
    private readonly LayoutMestreDetalhe _layout;
    private bool _carregado;

    public MapasTerritoriaisPage(MapasTerritoriaisViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _layout = new LayoutMestreDetalhe(this, Grade, viewModel);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_carregado) return;
        _carregado = true;
        _viewModel.CarregarCommand.Execute(null);
    }

    protected override bool OnBackButtonPressed() => _layout.TratarVoltar() || base.OnBackButtonPressed();
}
