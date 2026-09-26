using Lone.Cliente.ViewModels;

namespace Lone.App.Views;

/// <summary>Só aparência. O módulo vem da rota do Shell ("configuracoes-pessoas" → Pessoas); cada rota tem a sua página.</summary>
public partial class ConfiguracoesPage : ContentPage
{
    private readonly ConfiguracoesViewModel _viewModel;
    private bool _moduloDefinido;

    public ConfiguracoesPage(ConfiguracoesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        // Cola de interface: a rota é a mesma do item do menu (AppShell.xaml); o Shell pergunta antes de sair de uma ficha alterada.
        _viewModel.Navegar = rota => Shell.Current.GoToAsync("//" + rota);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (!_moduloDefinido && ModulosConfiguracao.DaRota(Shell.Current?.CurrentState?.Location?.OriginalString) is { } modulo)
        {
            _viewModel.Modulo = modulo;
            _moduloDefinido = true;
        }
        _viewModel.AtualizarCommand.Execute(null); // permissões podem ter mudado (sessão relida)
    }
}
