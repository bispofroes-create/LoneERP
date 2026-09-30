using Lone.App.Views;
using Lone.Cliente.Mensagens;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Sessao;

namespace Lone.App;

public partial class App : Application
{
    private readonly IServiceProvider _servicos;

    public App(IServiceProvider servicos)
    {
        InitializeComponent();
        _servicos = servicos;

        // Avisos da sessão no meio do uso: o servidor encerrou a sessão ou passou a exigir troca de senha.
        var sessao = servicos.GetRequiredService<SessaoCliente>();
        sessao.Expirou += (_, mensagem) => IrSeEstiverNoSistema(Tela.Login, mensagem);
        sessao.TrocaDeSenhaExigida += (_, _) => IrSeEstiverNoSistema(Tela.TrocaDeSenhaObrigatoria, null);

        // Leitor de tela: cada toast é anunciado uma vez aqui (não em cada página que tem a camada de mensagens).
        servicos.GetRequiredService<ServicoMensagens>().Publicada += (_, mensagem) =>
            MainThread.BeginInvokeOnMainThread(() => SemanticScreenReader.Announce(mensagem.Texto));
    }

    /// <summary>Só age com o menu aberto; nas telas de entrada o próprio fluxo já trata o caso.</summary>
    private void IrSeEstiverNoSistema(Tela tela, string? mensagem) => MainThread.BeginInvokeOnMainThread(async () =>
    {
        try
        {
            if (Windows.FirstOrDefault()?.Page is AppShell)
                await _servicos.GetRequiredService<INavegacao>().IrParaAsync(tela, mensagem);
        }
        catch (Exception ex)
        {
            // Sem isto um erro aqui fecharia o aplicativo.
            System.Diagnostics.Debug.WriteLine($"Falha ao voltar à tela de entrada: {ex}");
        }
    });

    /// <summary>A janela abre na tela de carregamento, que decide entre login, primeiro acesso e sistema.</summary>
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_servicos.GetRequiredService<CarregandoPage>())
        {
            Title = "Lone ERP",
            MinimumWidth = 360,
            MinimumHeight = 560
        };
}
