using Lone.Cliente.ViewModels;

namespace Lone.Cliente.Mensagens;

/// <summary>
/// Ordem de atendimento quando há mais mensagens do que cabem na tela (Fase 1: quase tudo é <see cref="Normal"/>).
/// A maior passa à frente das que esperam; a maior que a menor visível toma o lugar dela (que volta a esperar).
/// </summary>
public enum PrioridadeMensagem
{
    Baixa,
    Normal,
    Alta,
    Critica
}

/// <summary>
/// Como a mensagem é apresentada. Quem escolhe é o <see cref="ServicoMensagens"/> (classificação), não a tela.
/// Fase 1: só o toast (sucesso). Previstos para as próximas fases: faixa na tela (erro/aviso), diálogo de decisão
/// ("salve ou descarte") e progresso — entram aqui quando forem implementados, não antes.
/// </summary>
public enum ApresentacaoMensagem
{
    /// <summary>Aviso passageiro na camada global da janela (canto inferior direito; embaixo, na largura toda, no celular).</summary>
    Toast
}

/// <summary>
/// Padrões de texto das mensagens. Confirmação com o nome do registro ("Pessoa salva: Bruno"): o toast fica alguns segundos
/// e pode ser lido depois que o contexto mudou (outra ficha aberta, outro toast empilhado); o nome o torna compreensível
/// sozinho. "[objeto] [ação]: [nome]" evita a concordância de gênero do nome ("Edna salvo"). Campos alterados não entram:
/// quem mostra o que mudou é o Histórico.
/// </summary>
public static class TextosMensagem
{
    /// <summary>Nomes maiores que isto (razões sociais) são abreviados com "…".</summary>
    public const int TamanhoMaximoNome = 40;

    /// <summary>"Pessoa salva: Bruno". Sem nome, só o texto.</summary>
    public static string ComNome(string texto, string? nome)
    {
        var n = nome?.Trim() ?? string.Empty;
        if (n.Length == 0) return texto;
        if (n.Length > TamanhoMaximoNome) n = n[..(TamanhoMaximoNome - 1)].TrimEnd() + "…";
        return $"{texto}: {n}";
    }
}

/// <summary>
/// A única ação que um toast pode oferecer (ex.: "Abrir"). Só quando existe um próximo passo claramente útil:
/// o toast não é menu. <paramref name="Descricao"/> é o que o leitor de tela lê no botão (ex.: "Abrir a ficha de Ana").
/// </summary>
public sealed record AcaoMensagem(string Texto, Func<Task> Executar, string? Descricao = null);

/// <summary>
/// Uma mensagem ao usuário, só em memória (não é gravada, não é notificação persistente e não vai para a auditoria).
/// <see cref="Tipo"/> é a severidade (informação, sucesso, aviso, erro) — o mesmo <see cref="TipoMensagem"/> que as telas
/// já usam, sem uma escala paralela.
/// </summary>
public sealed class MensagemUsuario
{
    internal MensagemUsuario(string texto, TipoMensagem tipo, ApresentacaoMensagem apresentacao, PrioridadeMensagem prioridade,
        TimeSpan duracao, AcaoMensagem? acao, string? contexto, DateTimeOffset criadaEm)
    {
        Texto = texto;
        Tipo = tipo;
        Apresentacao = apresentacao;
        Prioridade = prioridade;
        Duracao = duracao;
        Acao = acao;
        Contexto = contexto;
        CriadaEm = criadaEm;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public string Texto { get; }
    public TipoMensagem Tipo { get; }
    public ApresentacaoMensagem Apresentacao { get; }
    public PrioridadeMensagem Prioridade { get; }

    /// <summary>Quanto tempo fica visível (calculado pelo serviço: <see cref="ServicoMensagens.DuracaoPara"/>).</summary>
    public TimeSpan Duracao { get; }

    public AcaoMensagem? Acao { get; }

    /// <summary>De onde veio (ex.: a ficha aberta). Entra na comparação de repetidas: o mesmo texto de outro contexto não se junta.</summary>
    public string? Contexto { get; }

    public DateTimeOffset CriadaEm { get; }

    /// <summary>Quantas vezes a mesma mensagem chegou enquanto estava na tela ou esperando (repetidas não empilham).</summary>
    public int Contagem { get; internal set; } = 1;

    internal bool MesmaQue(string texto, TipoMensagem tipo, string? contexto) =>
        Tipo == tipo && string.Equals(Texto, texto, StringComparison.Ordinal) && string.Equals(Contexto, contexto, StringComparison.Ordinal);
}
