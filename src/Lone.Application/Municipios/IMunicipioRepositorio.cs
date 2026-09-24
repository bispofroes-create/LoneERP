using Lone.Contracts.Municipios;
using Lone.Domain.Entidades;

namespace Lone.Application.Municipios;

/// <summary>Município como vem da fonte oficial (código IBGE de 7 dígitos e nome).</summary>
public sealed record MunicipioOficial(int Codigo, string Nome);

/// <summary>Fonte oficial da lista de municípios (serviço de localidades do IBGE). Implementado em Lone.Infrastructure.</summary>
public interface IMunicipiosOficiais
{
    Task<IReadOnlyList<MunicipioOficial>> ListarAsync(CancellationToken ct);
}

/// <summary>Como uma pendência foi resolvida (ou por que não foi).</summary>
public sealed record ResultadoConciliacao(Municipio? Municipio, string? Observacao);

/// <summary>Persistência da tabela de municípios e das pendências de textos antigos.</summary>
public interface IMunicipioRepositorio
{
    Task<List<MunicipioDto>> ListarDaUfAsync(string uf, CancellationToken ct);

    /// <summary>Municípios pelos códigos (os que não existem ficam de fora).</summary>
    Task<Dictionary<int, Municipio>> ObterAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    Task<SituacaoMunicipios> ObterSituacaoAsync(CancellationToken ct);

    /// <summary>
    /// Inclui os novos, atualiza nomes que mudaram e desativa os que saíram da lista (nunca apaga).
    /// Devolve (incluídos, alterados, desativados).
    /// </summary>
    Task<(int Incluidos, int Alterados, int Desativados)> SincronizarAsync(
        IReadOnlyCollection<Municipio> oficiais, DateTime agoraUtc, CancellationToken ct);

    /// <summary>
    /// Cria pendência para endereço no Brasil ainda sem município e tenta resolver as pendências abertas com o
    /// <paramref name="resolver"/>. O que resolver é gravado no cadastro (com auditoria, origem "sistema").
    /// Devolve (resolvidas agora, ainda abertas).
    /// </summary>
    Task<(int Resolvidas, int Abertas)> ConciliarAsync(
        Func<PendenciaMunicipio, ResultadoConciliacao> resolver, DateTime agoraUtc, CancellationToken ct);

    /// <summary>Todos os municípios ativos (para montar o índice da conciliação).</summary>
    Task<List<Municipio>> ListarTodosAsync(CancellationToken ct);
}
