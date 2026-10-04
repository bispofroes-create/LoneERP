using Lone.Domain.Entidades;

namespace Lone.Application.Profissoes;

/// <summary>Persistência do cadastro de profissões. Nada é apagado: profissões são desativadas ou mescladas.</summary>
public interface IProfissaoRepositorio
{
    /// <summary>Em ordem alfabética, sem rastreamento.</summary>
    Task<List<Profissao>> ListarAsync(bool incluirInativas, CancellationToken ct);

    Task<Profissao?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>Nome já usado por outra profissão, sem diferenciar maiúsculas nem acentos (collation da coluna).</summary>
    Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct);

    /// <summary>
    /// Profissão para uma ocupação da CBO: a ativa ligada ao código; senão a de mesmo nome (maiúsculas e acentos não
    /// contam; pode estar desativada); senão nula.
    /// </summary>
    Task<Profissao?> ObterParaOcupacaoAsync(int codigo, string nome, CancellationToken ct);

    /// <summary>Quantos cadastros têm cada profissão (ou só a informada); as sem uso não aparecem no dicionário.</summary>
    Task<Dictionary<Guid, int>> ContarPessoasAsync(Guid? somenteProfissaoId, CancellationToken ct);

    /// <summary>Inclui ou altera, com auditoria e eventos; confere a versão.</summary>
    Task SalvarAsync(Profissao profissao, bool nova, CancellationToken ct);

    /// <summary>
    /// Numa transação: grava as duas profissões (a origem já desativada, com os eventos) e passa para o destino as
    /// pessoas que tinham a origem (cada uma fica com a mudança no próprio histórico). Devolve quantas eram.
    /// </summary>
    Task<int> MesclarAsync(Profissao origem, Profissao destino, CancellationToken ct);
}

/// <summary>Tabela oficial da CBO (ocupações). Só a importação do arquivo oficial altera; nada é apagado.</summary>
public interface IOcupacaoCboRepositorio
{
    Task<List<OcupacaoCbo>> ListarAtivasAsync(CancellationToken ct);

    Task<Dictionary<int, OcupacaoCbo>> ObterAsync(IReadOnlyCollection<int> codigos, CancellationToken ct);

    Task<(int Quantidade, DateTime? AtualizadaEm)> ObterSituacaoAsync(CancellationToken ct);

    /// <summary>Inclui as novas, atualiza as que mudaram e desativa as que saíram do arquivo.</summary>
    Task<(int Incluidas, int Alteradas, int Desativadas)> SincronizarAsync(
        IReadOnlyCollection<OcupacaoCbo> oficiais, DateTime agoraUtc, CancellationToken ct);
}
