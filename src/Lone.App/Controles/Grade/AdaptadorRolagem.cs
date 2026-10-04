using Lone.Cliente.Grade;
using Lone.Cliente.Navegacao;

namespace Lone.App.Controles.Grade;

/// <summary>
/// Adaptador de rolagem da GradeLista ↔ Fase 3 (P2-B2, Etapa 4; plano, itens 9.2 e 12): liga a âncora lógica da grade
/// à <see cref="MemoriaRolagem"/> da tela usando <b>só a interface pública</b> dela (Registrar, Pendente, Concluir,
/// RestauracaoPedida). Substitui, nas listas em GradeLista, o <c>Rolagem.Preservar</c> da ScrollView.
/// <list type="bullet">
/// <item><b>Capturar:</b> a cada rolagem, <c>Registrar(chave, deslocamento lateral, índice do primeiro visível)</c> — nesta
/// chave o Y é um <b>índice lógico</b>, não pixels.</item>
/// <item><b>Restaurar</b> (a Fase 3 pede por último, depois de reler a lista): espera a grade ter tamanho e linhas; leva o
/// índice ao topo e confirma (<see cref="GradeLista.IrParaAsync"/>); reforço P3: se a linha marcada (aberta ou na
/// prévia) ficou fora da vista, ela vai ao topo; reaplica o deslocamento lateral; <c>Concluir</c>. Nunca trava: com
/// erro ou sem tamanho a tempo, desiste e conclui.</item>
/// </list>
/// Grade escondida (ex.: a ficha voltou aberta por cima da lista): espera ela aparecer, sem gastar as tentativas.
/// </summary>
internal sealed class AdaptadorRolagem
{
    private const int TentativasProntidao = 40;
    private static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(100);

    private readonly GradeLista _grade;
    private readonly string _chave;
    private MemoriaRolagem? _memoria;
    private int _versao;
    private bool _restaurando;

    public AdaptadorRolagem(GradeLista grade, string chave)
    {
        _grade = grade;
        _chave = chave;
        grade.BindingContextChanged += Religar;
        grade.Loaded += Religar;
        grade.Unloaded += AoDescarregar;
        grade.SizeChanged += AoMudarTamanho;
        grade.RolagemMudou += Rolou;
        Religar(null, EventArgs.Empty);
    }

    public void Soltar()
    {
        _grade.BindingContextChanged -= Religar;
        _grade.Loaded -= Religar;
        _grade.Unloaded -= AoDescarregar;
        _grade.SizeChanged -= AoMudarTamanho;
        _grade.RolagemMudou -= Rolou;
        TrocarMemoria(null);
    }

    private void AoDescarregar(object? sender, EventArgs e)
    {
        _versao++; // restauração em andamento para: a tela saiu
        _restaurando = false;
        TrocarMemoria(null);
    }

    private void Religar(object? sender, EventArgs e) => TrocarMemoria((_grade.BindingContext as IEstadoNavegavel)?.Rolagem);

    private void TrocarMemoria(MemoriaRolagem? memoria)
    {
        if (ReferenceEquals(memoria, _memoria)) return;
        if (_memoria is not null) _memoria.RestauracaoPedida -= Pedida;
        _memoria = memoria;
        if (memoria is null) return;
        memoria.RestauracaoPedida += Pedida;
        if (memoria.Pendente(_chave) is not null) Iniciar();
    }

    /// <summary>O usuário rolou: guarda o primeiro visível (índice) e o deslocamento lateral.</summary>
    private void Rolou(object? sender, EventArgs e)
    {
        if (_restaurando || _memoria is null || !Pronta) return;
        if (_grade.PrimeiroVisivel < 0) return;
        _memoria.Registrar(_chave, _grade.DeslocamentoLateral, _grade.PrimeiroVisivel);
    }

    private void Pedida(object? sender, string chave)
    {
        if (chave == _chave) Iniciar();
    }

    /// <summary>A grade apareceu (ex.: a ficha fechou): se ainda há posição pendente, aplica agora.</summary>
    private void AoMudarTamanho(object? sender, EventArgs e)
    {
        if (!_restaurando && Pronta && _memoria?.Pendente(_chave) is not null) Iniciar();
    }

    private bool Pronta => _grade.Handler is not null && _grade.Width > 0 && _grade.Height > 0;

    private void Iniciar()
    {
        var versao = ++_versao;
        _grade.Dispatcher.Dispatch(() => _ = RestaurarAsync(versao));
    }

    private async Task RestaurarAsync(int versao)
    {
        if (_memoria is not { } memoria || memoria.Pendente(_chave) is not { } alvo) return;
        // Escondida (sem tamanho): espera aparecer, sem gastar tentativas (AoMudarTamanho reinicia).
        if (!Pronta) return;
        _restaurando = true;
        try
        {
            // Lista desenhada: espera as linhas chegarem (a Fase 3 pede depois de reler; aqui é só a margem do desenho).
            for (var i = 0; i < TentativasProntidao && _grade.Conteudo.Linhas.Count == 0; i++)
            {
                await Task.Delay(Intervalo);
                if (versao != _versao) return;
            }
            var linhas = _grade.Conteudo.Linhas;
            if (AncoraLogica.Limitar((int)Math.Round(alvo.Y), linhas.Count) is { } indice)
            {
                await _grade.IrParaAsync(indice);
                if (versao != _versao) return;

                // Reforço P3: a linha aberta ou na prévia tem de estar visível (dados que mudaram de lugar, por exemplo).
                var marcada = IndiceMarcado(linhas);
                if (AncoraLogica.Reforco(marcada, _grade.PrimeiroVisivel, _grade.UltimoVisivel) is { } reforco)
                {
                    await _grade.IrParaAsync(reforco);
                    if (versao != _versao) return;
                }
            }
            if (alvo.X > 0) await _grade.RolarLateralAsync(alvo.X);
        }
        catch (Exception)
        {
            // Nunca trava a tela por causa da rolagem: desiste em silêncio.
        }
        finally
        {
            if (versao == _versao)
            {
                _restaurando = false;
                memoria.Concluir(_chave);
            }
        }
    }

    private static int? IndiceMarcado(IReadOnlyList<ILinhaGrade> linhas)
    {
        for (var i = 0; i < linhas.Count; i++)
            if (linhas[i].Selecionada) return i;
        return null;
    }
}
