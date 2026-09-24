using Lone.Cliente.Navegacao;

namespace Lone.App.Views;

public partial class CarregandoPage : ContentPage
{
    private readonly FluxoDeEntrada _fluxo;
    private readonly INavegacao _navegacao;
    private bool _iniciado;

    public CarregandoPage(FluxoDeEntrada fluxo, INavegacao navegacao)
    {
        InitializeComponent();
        _fluxo = fluxo;
        _navegacao = navegacao;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_iniciado) return;
        _iniciado = true;

        try
        {
            var (tela, mensagem) = await _fluxo.InicialAsync();
            await _navegacao.IrParaAsync(tela, mensagem);
        }
        catch (Exception ex)
        {
            // async void: um erro aqui fecharia o app. Cai no login, que permite corrigir o servidor.
            await _navegacao.IrParaAsync(Tela.Login, $"Não foi possível iniciar: {ex.Message}");
        }
    }
}
