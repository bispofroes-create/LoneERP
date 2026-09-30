using Lone.App.Controles;
using Lone.Cliente.Navegacao;
using Lone.Cliente.ViewModels.Territorios;

namespace Lone.App.Views;

/// <summary>
/// Só aparência: arrumação lista/ficha conforme a largura, a operação pedida por outra tela e a vista levada ao topo da
/// ficha quando aparece uma mensagem (ex.: "Mudança incluída", lá no alto, depois de clicar em "Incluir na operação"
/// no fim da página).
/// </summary>
public partial class OperacoesTerritoriaisPage : ContentPage
{
    private readonly OperacoesTerritoriaisViewModel _viewModel;
    private readonly LayoutMestreDetalhe _layout;
    private readonly AberturaDeOperacaoTerritorial _abertura;
    private bool _carregado;

    public OperacoesTerritoriaisPage(OperacoesTerritoriaisViewModel viewModel, AberturaDeOperacaoTerritorial abertura)
    {
        _abertura = abertura;
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _layout = new LayoutMestreDetalhe(this, Grade, viewModel, larguraLista: 420);
        viewModel.MensagemMostrada += (_, _) => Dispatcher.Dispatch(() => _ = RolagemFicha.ScrollToAsync(0, 0, animated: true));
    }

    /// <summary>Carrega na primeira vez; depois, abre a operação pedida por outra tela, se houver.</summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_carregado)
        {
            _carregado = true;
            await _viewModel.CarregarCommand.ExecuteAsync(null);
        }
        if (_abertura.Retirar() is { } id) await _viewModel.AbrirOperacaoAsync(id);
    }

    protected override bool OnBackButtonPressed() => _layout.TratarVoltar() || base.OnBackButtonPressed();
}
