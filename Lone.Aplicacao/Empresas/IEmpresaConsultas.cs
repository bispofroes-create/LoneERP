namespace Lone.Aplicacao.Empresas;

/// <summary>Empresas do próprio grupo (pessoas com papel EmpresaDoGrupo) e seus estabelecimentos.</summary>
public interface IEmpresaConsultas
{
    /// <summary>Estabelecimentos ativos das empresas do grupo ativas: são as opções de "empresa ativa" no login.</summary>
    Task<List<EmpresaAtiva>> ListarEstabelecimentosAsync(CancellationToken ct = default);

    /// <summary>Empresas do grupo (a pessoa jurídica, não cada filial), para atribuir perfis por empresa.</summary>
    Task<List<EmpresaResumo>> ListarEmpresasAsync(CancellationToken ct = default);
}

/// <summary>Empresa (pessoa) do grupo.</summary>
public sealed record EmpresaResumo(int Id, string Nome, bool Ativa);

/// <summary>Estabelecimento em que o usuário está trabalhando (define empresa e filial das operações).</summary>
public sealed record EmpresaAtiva(int EstabelecimentoId, int EmpresaId, string Nome, string? Cnpj, bool EhMatriz)
{
    public string CnpjFormatado => Lone.Core.Validacao.Documento.Formatar(Cnpj);
    public string Descricao => Cnpj is null ? Nome : $"{Nome} · {CnpjFormatado}{(EhMatriz ? " (matriz)" : string.Empty)}";
}
