using Lone.App.Controles;
using Lone.Cliente.ViewModels.Cadastros;

namespace Lone.App.Views;

/// <summary>Só aparência: arrumação lista/ficha conforme a largura e os botões de ordem de cada linha.</summary>
public partial class CamposPersonalizadosPage : ContentPage
{
    private readonly CamposPersonalizadosViewModel _viewModel;
    private readonly LayoutMestreDetalhe _layout;
    private bool _carregado;

    public CamposPersonalizadosPage(CamposPersonalizadosViewModel viewModel)
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

    private void Subir_Clicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: LinhaCampoPersonalizado linha })
            _viewModel.SubirCommand.Execute(linha);
    }

    private void Descer_Clicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: LinhaCampoPersonalizado linha })
            _viewModel.DescerCommand.Execute(linha);
    }
}
