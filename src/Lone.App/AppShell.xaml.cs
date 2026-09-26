using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Cadastros;

namespace Lone.App;

public partial class AppShell : Shell
{
    public AppShell(MenuViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        Unloaded += (_, _) => viewModel.Dispose();

        // Tela grande: menu fixo ao lado (vários painéis). Celular: menu recolhido para sobrar espaço.
        FlyoutBehavior = DeviceInfo.Current.Idiom == DeviceIdiom.Phone ? FlyoutBehavior.Flyout : FlyoutBehavior.Locked;

        // Cola de interface: o menu (ViewModel) pede a rota; o Shell navega e avisa qual tela ficou aberta.
        viewModel.Navegar = async rota =>
        {
            await GoToAsync("//" + rota);
            if (FlyoutBehavior == FlyoutBehavior.Flyout) FlyoutIsPresented = false;
        };
        Navigated += (_, _) => viewModel.DefinirRotaAtual(CurrentState?.Location?.OriginalString);
    }

    /// <summary>
    /// Trocar de tela pelo menu com uma ficha alterada e não salva: pergunta antes (mesma regra do Fechar/Descartar).
    /// </summary>
    protected override async void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);
        if (args.Cancelled || CurrentPage?.BindingContext is not IMestreDetalhe { TemAlteracoes: true } tela)
            return;

        var adiamento = args.GetDeferral();
        try
        {
            if (!await tela.PodeSairAsync())
                args.Cancel();
        }
        finally
        {
            adiamento.Complete();
        }
    }
}
