using Lone.Contracts.Comercial;
using Lone.Domain.Entidades;

namespace Lone.Application.Comercial;

/// <summary>Persistência: tipos de ausência. Nada é apagado: é desativado.</summary>
public interface ITipoAusenciaRepositorio
{
    Task<List<TipoAusencia>> ListarAsync(CancellationToken ct);
    Task<TipoAusencia?> ObterAsync(Guid id, CancellationToken ct);
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct);
    Task SalvarAsync(TipoAusencia item, bool novo, CancellationToken ct);
}

/// <summary>Persistência: parâmetros do módulo Comercial (um registro só, criado pela migração).</summary>
public interface IParametrosComerciaisRepositorio
{
    Task<ParametrosComerciais> ObterAsync(CancellationToken ct);
    Task SalvarAsync(ParametrosComerciais parametros, CancellationToken ct);
}

/// <summary>Persistência: coberturas de ausência. Nunca apaga (cancela ou encerra).</summary>
public interface ICoberturaRepositorio
{
    Task<CoberturaComercial?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>As coberturas do titular (todas: a validação ignora as canceladas).</summary>
    Task<List<CoberturaComercial>> DoTitularAsync(Guid titularId, CancellationToken ct);

    /// <summary>Não canceladas que terminam em <paramref name="desde"/> ou depois; com <paramref name="incluirEncerradas"/>, todas.</summary>
    Task<List<CoberturaComercial>> ListarAsync(DateOnly desde, bool incluirEncerradas, CancellationToken ct);

    Task SalvarAsync(CoberturaComercial item, bool novo, CancellationToken ct);
}

/// <summary>Consultas de apoio às coberturas e à carteira (sem trazer a carteira inteira para a memória).</summary>
public interface ICoberturaConsultas
{
    /// <summary>Por cobertura: clientes com vínculo ativo do titular, no escopo (papel, empresa), vigente em algum dia da cobertura.</summary>
    Task<Dictionary<Guid, int>> ContarClientesAsync(IReadOnlyCollection<CoberturaComercial> coberturas, CancellationToken ct);

    /// <summary>Vínculos ativos da carteira que terminam entre as datas (inclusive), com nomes.</summary>
    Task<List<VinculoVencendoDto>> CarteiraVencendoAsync(DateOnly de, DateOnly ate, CancellationToken ct);

    /// <summary>Nomes das equipes (ativas e desativadas).</summary>
    Task<Dictionary<Guid, string>> NomesEquipesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
