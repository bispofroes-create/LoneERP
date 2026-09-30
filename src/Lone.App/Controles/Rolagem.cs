using Lone.Cliente.Navegacao;

namespace Lone.App.Controles;

/// <summary>
/// Preservação de rolagem <b>opt-in</b>: <c>c:Rolagem.Preservar="lista"</c> num <see cref="ScrollView"/> faz a posição dele
/// entrar no estado de navegação da tela (a tela precisa ser <see cref="IEstadoNavegavel"/>, como a base dos cadastros).
/// Só as rolagens marcadas participam — nada de capturar toda rolagem do sistema. Enquanto o usuário rola, a posição é
/// registrada; quando a tela é restaurada, a posição é reaplicada assim que o conteúdo tiver altura para recebê-la
/// (a lista/ficha já carregada), com tentativas limitadas. Sem código de rolagem nas páginas.
/// </summary>
public static class Rolagem
{
    public static readonly BindableProperty PreservarProperty =
        BindableProperty.CreateAttached("Preservar", typeof(string), typeof(Rolagem), null, propertyChanged: PreservarMudou);

    private static readonly BindableProperty LigacaoProperty =
        BindableProperty.CreateAttached("Ligacao", typeof(LigacaoRolagem), typeof(Rolagem), null);

    public static string? GetPreservar(BindableObject objeto) => (string?)objeto.GetValue(PreservarProperty);

    public static void SetPreservar(BindableObject objeto, string? chave) => objeto.SetValue(PreservarProperty, chave);

    private static void PreservarMudou(BindableObject objeto, object antigo, object novo)
    {
        if (objeto is not ScrollView rolagem) return;
        (rolagem.GetValue(LigacaoProperty) as LigacaoRolagem)?.Soltar();
        rolagem.SetValue(LigacaoProperty, novo is string { Length: > 0 } chave ? new LigacaoRolagem(rolagem, chave) : null);
    }

    /// <summary>Liga uma rolagem à memória de rolagem da tela dela (o BindingContext).</summary>
    private sealed class LigacaoRolagem
    {
        private const int MaximoTentativas = 40;              // ~4 s: lista/ficha grande terminando de carregar
        private static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(100);

        private readonly ScrollView _rolagem;
        private readonly string _chave;
        private MemoriaRolagem? _memoria;
        private int _versao;

        public LigacaoRolagem(ScrollView rolagem, string chave)
        {
            _rolagem = rolagem;
            _chave = chave;
            rolagem.BindingContextChanged += Religar;
            rolagem.Loaded += Religar;
            rolagem.Unloaded += AoDescarregar;
            rolagem.Scrolled += Rolou;
            Religar(null, EventArgs.Empty);
        }

        public void Soltar()
        {
            _rolagem.BindingContextChanged -= Religar;
            _rolagem.Loaded -= Religar;
            _rolagem.Unloaded -= AoDescarregar;
            _rolagem.Scrolled -= Rolou;
            TrocarMemoria(null);
        }

        private void AoDescarregar(object? sender, EventArgs e) => TrocarMemoria(null); // sem referência presa à tela

        private void Religar(object? sender, EventArgs e) =>
            TrocarMemoria((_rolagem.BindingContext as IEstadoNavegavel)?.Rolagem);

        private void TrocarMemoria(MemoriaRolagem? memoria)
        {
            if (ReferenceEquals(memoria, _memoria)) return;
            if (_memoria is not null) _memoria.RestauracaoPedida -= Pedida;
            _memoria = memoria;
            if (memoria is null) return;
            memoria.RestauracaoPedida += Pedida;
            if (memoria.Pendente(_chave) is not null) Tentar(++_versao, 0);
        }

        private void Rolou(object? sender, ScrolledEventArgs e) => _memoria?.Registrar(_chave, e.ScrollX, e.ScrollY);

        private void Pedida(object? sender, string chave)
        {
            if (chave == _chave) Tentar(++_versao, 0);
        }

        /// <summary>Aplica a posição pendente quando a rolagem estiver visível e com conteúdo suficiente.</summary>
        private void Tentar(int versao, int tentativa)
        {
            if (versao != _versao || _memoria?.Pendente(_chave) is not { } alvo) return;

            var pronta = _rolagem.IsVisible && _rolagem.Height > 0 && _rolagem.Handler is not null;
            var alturaMaxima = Math.Max(0, _rolagem.ContentSize.Height - _rolagem.Height);
            var larguraMaxima = Math.Max(0, _rolagem.ContentSize.Width - _rolagem.Width);
            var cabe = alvo.Y <= alturaMaxima + 1 && alvo.X <= larguraMaxima + 1;

            if (pronta && (cabe || tentativa >= MaximoTentativas))
            {
                // Conteúdo menor que antes (ex.: a lista encolheu): vai até onde dá.
                var y = Math.Min(alvo.Y, alturaMaxima);
                var x = Math.Min(alvo.X, larguraMaxima);
                _ = AplicarAsync(versao, x, y);
                return;
            }
            if (tentativa >= MaximoTentativas)
            {
                _memoria.Concluir(_chave); // não ficou visível a tempo: desiste sem travar nada
                return;
            }
            _rolagem.Dispatcher.DispatchDelayed(Intervalo, () => Tentar(versao, tentativa + 1));
        }

        private async Task AplicarAsync(int versao, double x, double y)
        {
            try
            {
                await _rolagem.ScrollToAsync(x, y, animated: false);
            }
            finally
            {
                if (versao == _versao) _memoria?.Concluir(_chave);
            }
        }
    }
}
