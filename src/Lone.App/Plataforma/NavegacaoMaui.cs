using Lone.App.Views;
using Lone.Cliente.Navegacao;

namespace Lone.App.Plataforma;

/// <summary>
/// Troca a página principal da janela. As telas de entrada ficam fora do menu; "Sistema" cria um AppShell
/// novo, então o menu sempre reflete as permissões da empresa ativa.
/// </summary>
public sealed class NavegacaoMaui : INavegacao
{
    private readonly IServiceProvider _servicos;

    public NavegacaoMaui(IServiceProvider servicos)
    {
        _servicos = servicos;
    }

    private static Window Janela => Application.Current?.Windows.FirstOrDefault()
                                    ?? throw new InvalidOperationException("Nenhuma janela aberta.");

    public Task IrParaAsync(Tela tela, string? mensagem = null) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        Page pagina = tela switch
        {
            Tela.Login => Login(mensagem),
            Tela.PrimeiroAcesso => _servicos.GetRequiredService<PrimeiroAcessoPage>(),
            Tela.TrocaDeSenhaObrigatoria => _servicos.GetRequiredService<TrocarSenhaPage>(),
            Tela.EscolherEmpresa => _servicos.GetRequiredService<EscolherEmpresaPage>(),
            Tela.Sistema => _servicos.GetRequiredService<AppShell>(),
            _ => throw new ArgumentOutOfRangeException(nameof(tela), tela, null)
        };
        Janela.Page = pagina;
    });

    public Task AbrirTrocaDeSenhaAsync() => MainThread.InvokeOnMainThreadAsync(() =>
        Janela.Page!.Navigation.PushModalAsync(_servicos.GetRequiredService<TrocarSenhaPage>()));

    public Task FecharAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (Janela.Page?.Navigation.ModalStack.Count > 0)
            await Janela.Page.Navigation.PopModalAsync();
    });

    private LoginPage Login(string? mensagem)
    {
        var pagina = _servicos.GetRequiredService<LoginPage>();
        pagina.ViewModel.Receber(mensagem);
        return pagina;
    }
}
