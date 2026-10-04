using Lone.Cliente.ViewModels.Metas;

namespace Lone.App.Views;

/// <summary>Só aparência. Padrão de tela de cadastro (03/10/2026): a lista ocupa a página; a ficha abre em página própria.</summary>
public partial class MetasPage : ContentPage
{
    private readonly MetasViewModel _viewModel;
    private bool _carregado;

    public MetasPage(MetasViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
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
