using Lone.Cliente.ViewModels;

namespace Lone.App.Views;

public partial class ConfiguracoesPage : ContentPage
{
    private readonly ConfiguracoesViewModel _viewModel;

    public ConfiguracoesPage(ConfiguracoesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        // Cola de interface: a rota é a mesma do item do menu (AppShell.xaml); o Shell pergunta antes de sair de uma ficha alterada.
        _viewModel.Navegar = rota => Shell.Current.GoToAsync("//" + rota);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AtualizarCommand.Execute(null); // permissões podem ter mudado (sessão relida)
    }
}
