using Lone.Contracts.Empresas;

namespace Lone.Application.Empresas;

/// <summary>Empresas do próprio grupo (pessoas com papel EmpresaDoGrupo) e seus estabelecimentos.</summary>
public interface IEmpresaConsultas
{
    /// <summary>Estabelecimentos ativos das empresas do grupo ativas: são as opções de "empresa ativa".</summary>
    Task<List<EmpresaAtiva>> ListarEstabelecimentosAsync(CancellationToken ct = default);

    /// <summary>Empresas do grupo (a pessoa jurídica, não cada filial), para atribuir perfis por empresa.</summary>
    Task<List<EmpresaResumo>> ListarEmpresasAsync(CancellationToken ct = default);
}
