using Lone.App.Controles;
using Lone.Cliente.ViewModels.Comercial;

namespace Lone.App.Views;

/// <summary>Só aparência: arrumação lista/ficha conforme a largura e a ligação de "Abrir ficha" com o Shell.</summary>
public partial class TransferenciasPage : ContentPage
{
    private readonly TransferenciasViewModel _viewModel;
    private readonly LayoutMestreDetalhe _layout;
    private bool _carregado;

    public TransferenciasPage(TransferenciasViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.AbrirTela = rota => Shell.Current.GoToAsync("//" + rota);
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
