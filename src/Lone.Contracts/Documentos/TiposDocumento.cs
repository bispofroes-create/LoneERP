using Lone.Domain.Enums;

namespace Lone.Contracts.Documentos;

/// <summary>Tipo de documento do cadastro de tipos (RG, CNH, Alvará...).</summary>
public sealed class TipoDocumentoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: tipo de sistema ligado ao enum (nulo = criado pelo usuário).</summary>
    public TipoDocumento? TipoSistema { get; set; }

    public bool ExigeValidade { get; set; }
    public int DiasAvisoVencimento { get; set; } = 30;

    // ---- P1-8: regras do tipo (padrões = tipo novo criado pelo usuário, como sempre se comportou) ----

    public bool AplicaPessoaFisica { get; set; } = true;
    public bool AplicaPessoaJuridica { get; set; } = true;
    public bool AplicaEstrangeiro { get; set; } = true;
    public UsoCampoDocumento UsoOrgaoEmissor { get; set; } = UsoCampoDocumento.Oculto;
    public UsoCampoDocumento UsoUf { get; set; } = UsoCampoDocumento.Oculto;
    public UsoCampoDocumento UsoEmissao { get; set; } = UsoCampoDocumento.Opcional;
    public FormatoNumeroDocumento FormatoNumero { get; set; } = FormatoNumeroDocumento.Livre;
    public int? TamanhoMinimoNumero { get; set; }
    public int? TamanhoMaximoNumero { get; set; }
    public UnicidadeDocumento Unicidade { get; set; } = UnicidadeDocumento.Nenhuma;

    /// <summary>Somente leitura: quantos documentos ativos usam este tipo (só para quem gerencia os tipos).</summary>
    public int QuantidadeUsos { get; set; }
}
