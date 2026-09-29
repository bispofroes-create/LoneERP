using Lone.Contracts.Metas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Application.Metas;

/// <summary>Persistência das equipes (membros nunca apagados: a saída encerra).</summary>
public interface IEquipeRepositorio
{
    Task<List<Equipe>> ListarAsync(CancellationToken ct);
    Task<Equipe?> ObterAsync(Guid id, CancellationToken ct);
    Task SalvarAsync(Equipe equipe, bool novo, CancellationToken ct);
}

public interface IIndicadorRepositorio
{
    Task<List<Indicador>> ListarAsync(CancellationToken ct);
    Task<Indicador?> ObterAsync(Guid id, CancellationToken ct);
    Task SalvarAsync(Indicador indicador, bool novo, CancellationToken ct);
}

public interface IMetaRepositorio
{
    Task<List<MetaResumoDto>> ListarAsync(bool incluirInativas, CancellationToken ct);

    /// <summary>A meta com itens, faixas, participantes e alvos (sem rastreamento).</summary>
    Task<Meta?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>Os participantes de cada meta (nível e referência), para o escopo da lista (Fase 2a-3).</summary>
    Task<Dictionary<Guid, List<(NivelParticipante Nivel, Guid ReferenciaId)>>> ParticipantesAsync(IReadOnlyCollection<Guid> metas, CancellationToken ct);

    /// <summary>Inclui ou altera a meta e os filhos (alvos e participantes que saíram do rascunho são removidos).</summary>
    Task SalvarAsync(Meta meta, bool novo, CancellationToken ct);
}

/// <summary>Nomes e opções de participantes (empresas, filiais, departamentos, equipes, colaboradores).</summary>
public interface IMetaConsultas
{
    Task<Dictionary<(NivelParticipante, Guid), string>> NomesAsync(IEnumerable<(NivelParticipante Nivel, Guid Id)> participantes, CancellationToken ct);
    Task<List<ParticipanteOpcaoDto>> OpcoesAsync(DateOnly hoje, CancellationToken ct);
    Task<Dictionary<Guid, string>> NomesPessoasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}

/// <summary>
/// Realizado dos indicadores calculados pelo cadastro (D6). Para cada participante, os clientes considerados são os
/// da carteira dos colaboradores daquele nível no período. Fontes de vendas entram aqui quando o módulo existir.
/// </summary>
public interface IFonteIndicadores
{
    Task<Dictionary<Guid, decimal>> CalcularAsync(FonteIndicador fonte, DateOnly inicio, DateOnly fim,
                                                  IReadOnlyList<MetaParticipante> participantes, CancellationToken ct);
}
