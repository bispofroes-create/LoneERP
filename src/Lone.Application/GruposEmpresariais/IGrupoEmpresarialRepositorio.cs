using Lone.Contracts.GruposEmpresariais;
using Lone.Domain.Entidades;

namespace Lone.Application.GruposEmpresariais;

/// <summary>Persistência dos grupos empresariais. Nada é apagado: são desativados.</summary>
public interface IGrupoEmpresarialRepositorio
{
    /// <summary>Todos (ativos e desativados), sem rastreamento.</summary>
    Task<List<GrupoEmpresarial>> ListarAsync(CancellationToken ct);

    Task<GrupoEmpresarial?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>Quantas pessoas jurídicas fazem parte de cada grupo (ou só do informado).</summary>
    Task<Dictionary<Guid, int>> ContarEmpresasAsync(Guid? somenteId, CancellationToken ct);

    /// <summary>As pessoas jurídicas do grupo, com os seus estabelecimentos contados (matriz e filiais de cada uma).</summary>
    Task<List<EmpresaDoGrupoEmpresarialDto>> ListarEmpresasAsync(Guid grupoId, CancellationToken ct);

    Task SalvarAsync(GrupoEmpresarial item, bool novo, CancellationToken ct);
}
