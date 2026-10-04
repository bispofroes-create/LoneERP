using Lone.Cliente.Navegacao;
using Lone.Cliente.ViewModels.Comercial;

namespace Lone.App.Views;

/// <summary>Só aparência (padrão de tela de cadastro, 03/10/2026: lista na página, ficha em página própria) e a ligação de "Abrir ficha" com o Shell.</summary>
public partial class TransferenciasPage : ContentPage
{
    private readonly TransferenciasViewModel _viewModel;
    private bool _carregado;

    public TransferenciasPage(TransferenciasViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.AbrirTela = rota => GerenciadorNavegacao.Padrao.IrParaTelaAsync(rota, OrigemNavegacao.Link);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_carregado) return;
        _carregado = true;
        _viewModel.CarregarCommand.Execute(null);
    }

    /// <summary>Voltar do Android com a ficha aberta: volta para a lista (pergunta se houver alterações).</summary>
    protected override bool OnBackButtonPressed()
    {
        if (!_viewModel.Editando) return base.OnBackButtonPressed();
        _viewModel.FecharFichaCommand.Execute(null);
        return true;
    }
}
