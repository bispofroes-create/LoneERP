using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Pessoas;

namespace Lone.App.Views;

/// <summary>
/// Só aparência. Pessoas usa uma tela de cada vez em qualquer largura (lista em tabela → ficha em tela cheia), por isso
/// fica sempre no "modo compacto" do mestre-detalhe. A largura da página decide quais colunas da tabela cabem.
/// </summary>
public partial class PessoasPage : ContentPage
{
    private readonly PessoasViewModel _viewModel;
    private bool _carregado;

    public PessoasPage(PessoasViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.ModoCompacto = true; // lista OU ficha, nunca lado a lado
        _viewModel.AbrirConfiguracoesDoModulo = () => Shell.Current.GoToAsync("//" + ModulosConfiguracao.Rota(ModulosConfiguracao.Pessoas));
        SizeChanged += (_, _) => _viewModel.DefinirLarguraDaLista(Width - Lista.Padding.HorizontalThickness);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_carregado) return;
        _carregado = true;
        _viewModel.CarregarCommand.Execute(null);
    }

    /// <summary>Botão voltar do Android com a ficha aberta: volta para a lista (pergunta se houver alterações).</summary>
    protected override bool OnBackButtonPressed()
    {
        if (!_viewModel.Editando) return base.OnBackButtonPressed();
        _viewModel.FecharFichaCommand.Execute(null);
        return true;
    }
}
