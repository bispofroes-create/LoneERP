namespace Lone.Cliente.Navegacao;

/// <summary>Posição de uma rolagem marcada (<c>Rolagem.Preservar</c> no XAML).</summary>
public sealed record PosicaoRolagem(double X, double Y);

/// <summary>
/// Retrato do <b>estado de navegação</b> de uma tela — como o usuário a deixou: pesquisa, ficha aberta, rolagens (e o
/// que cada tela acrescentar, ex.: filtros de Pessoas). Não é dado de negócio: nunca guarda o conteúdo editado de uma
/// ficha, só <i>qual</i> registro estava aberto (ele reabre como está gravado). Só em memória; some a cada Shell novo.
/// A pesquisa pode conter dado pessoal digitado pelo usuário: nunca vai para banco, arquivo, endereço, log ou dica.
/// </summary>
public record EstadoTela
{
    public string Busca { get; init; } = string.Empty;

    /// <summary>Ficha aberta (nunca um registro novo, ainda não gravado).</summary>
    public ReferenciaRegistro? Registro { get; init; }

    public IReadOnlyDictionary<string, PosicaoRolagem> Rolagens { get; init; } = new Dictionary<string, PosicaoRolagem>();
}

/// <summary>
/// Tela que sabe entregar e reaplicar o próprio estado de navegação. A restauração é tolerante (o que não vale mais é
/// ignorado; nada vira erro) e idempotente (aplicar duas vezes dá o mesmo resultado). Nos cadastros quem implementa é a
/// base (<c>CadastroViewModelBase</c>): pesquisa + ficha para todas as telas; cada tela pode acrescentar o seu.
/// </summary>
public interface IEstadoNavegavel : ITelaNavegavel
{
    EstadoTela CapturarEstado();

    /// <summary>
    /// Reaplica o estado. <paramref name="cancelamento"/>: o usuário foi para outro lugar antes de terminar — a tela para
    /// no ponto em que está (nenhum passo seguinte começa e nada de uma leitura ainda em andamento é aplicado).
    /// </summary>
    Task RestaurarEstadoAsync(EstadoTela estado, CancellationToken cancelamento = default);

    /// <summary>Posições das rolagens marcadas desta tela (a plataforma registra e reaplica).</summary>
    MemoriaRolagem Rolagem { get; }
}

/// <summary>
/// Posições das rolagens marcadas de uma tela, por chave ("lista", "ficha"). A plataforma registra enquanto o usuário
/// rola; ao restaurar, a posição fica <i>pendente</i> até a rolagem conseguir recebê-la (conteúdo carregado) — nesse meio
/// tempo, rolagens causadas pelo carregamento não sobrescrevem a posição a restaurar.
/// </summary>
public sealed class MemoriaRolagem
{
    private readonly Dictionary<string, PosicaoRolagem> _atuais = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PosicaoRolagem> _pendentes = new(StringComparer.Ordinal);

    /// <summary>Uma posição foi pedida para a chave (a rolagem marcada tenta aplicá-la).</summary>
    public event EventHandler<string>? RestauracaoPedida;

    public void Registrar(string chave, double x, double y)
    {
        if (_pendentes.ContainsKey(chave)) return;
        _atuais[chave] = new PosicaoRolagem(x, y);
    }

    public IReadOnlyDictionary<string, PosicaoRolagem> Capturar() => new Dictionary<string, PosicaoRolagem>(_atuais, StringComparer.Ordinal);

    /// <summary>Pede as posições (a mesma posição de novo não muda nada: idempotente).</summary>
    public void Restaurar(IReadOnlyDictionary<string, PosicaoRolagem> posicoes)
    {
        foreach (var (chave, posicao) in posicoes)
        {
            _atuais[chave] = posicao;
            _pendentes[chave] = posicao;
            RestauracaoPedida?.Invoke(this, chave);
        }
    }

    public PosicaoRolagem? Pendente(string chave) => _pendentes.GetValueOrDefault(chave);

    /// <summary>A rolagem chegou à posição (ou desistiu): volta a registrar o que o usuário fizer.</summary>
    public void Concluir(string chave) => _pendentes.Remove(chave);
}
