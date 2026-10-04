using Lone.Cliente.ViewModels.Seguranca;

namespace Lone.App.Views;

/// <summary>Só aparência (padrão de tela de cadastro, 03/10/2026: lista na página, ficha em página própria) e repasse do "Remover" de cada perfil ao ViewModel.</summary>
public partial class UsuariosPage : ContentPage
{
    private readonly UsuariosViewModel _viewModel;
    private bool _carregado;

    public UsuariosPage(UsuariosViewModel viewModel)
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

    private void RemoverPerfil_Clicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: PerfilAtribuido perfil })
            _viewModel.RemoverPerfilCommand.Execute(perfil);
    }
}
