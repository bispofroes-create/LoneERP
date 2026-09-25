using Lone.App.Controles;
using Lone.Cliente.ViewModels.Metas;

namespace Lone.App.Views;

/// <summary>Só aparência: arrumação lista/ficha conforme a largura.</summary>
public partial class MetasPage : ContentPage
{
    private readonly MetasViewModel _viewModel;
    private readonly LayoutMestreDetalhe _layout;
    private bool _carregado;

    public MetasPage(MetasViewModel viewModel)
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
