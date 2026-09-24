using Lone.Domain.Validacao;

namespace Lone.Contracts.Empresas;

/// <summary>Empresa (pessoa) do grupo.</summary>
public sealed record EmpresaResumo(Guid Id, string Nome, bool Ativa);

/// <summary>Estabelecimento em que o usuário está trabalhando (define empresa e filial das operações).</summary>
public sealed record EmpresaAtiva(Guid EstabelecimentoId, Guid EmpresaId, string Nome, string? Cnpj, bool EhMatriz)
{
    public string CnpjFormatado => Documento.Formatar(Cnpj);
    public string Descricao => Cnpj is null ? Nome : $"{Nome} · {CnpjFormatado}{(EhMatriz ? " (matriz)" : string.Empty)}";
}
