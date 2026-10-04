using Lone.Cliente.Navegacao;
using Lone.Cliente.ViewModels.Territorios;

namespace Lone.App.Views;

/// <summary>
/// Só aparência (padrão de tela de cadastro, 03/10/2026: lista na página, ficha em página própria): a operação pedida por outra tela e a vista levada ao topo da
/// ficha quando aparece uma mensagem (ex.: "Mudança incluída", lá no alto, depois de clicar em "Incluir na operação"
/// no fim da página).
/// </summary>
public partial class OperacoesTerritoriaisPage : ContentPage
{
    private readonly OperacoesTerritoriaisViewModel _viewModel;
    private readonly AberturaDeOperacaoTerritorial _abertura;
    private bool _carregado;

    public OperacoesTerritoriaisPage(OperacoesTerritoriaisViewModel viewModel, AberturaDeOperacaoTerritorial abertura)
    {
        _abertura = abertura;
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        viewModel.MensagemMostrada += (_, _) => Dispatcher.Dispatch(() => _ = Ficha.RolarParaOTopoAsync());
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

    /// <summary>Voltar do Android com a ficha aberta: volta para a lista (pergunta se houver alterações).</summary>
    protected override bool OnBackButtonPressed()
    {
        if (!_viewModel.Editando) return base.OnBackButtonPressed();
        _viewModel.FecharFichaCommand.Execute(null);
        return true;
    }
}
