using Lone.Domain.Enums;

namespace Lone.Contracts.Pessoas;

/// <summary>
/// Privacidade da pessoa (aba "Privacidade"): consentimentos por finalidade (situação calculada pela regra do domínio),
/// todos os períodos (histórico) e, para cada telefone/e-mail ativo, a decisão da regra central por finalidade.
/// Só leitura: conceder e revogar são ações próprias.
/// </summary>
public sealed class PrivacidadeDto
{
    /// <summary>Finalidades que aceitam consentimento (ativas, base legal consentimento), com a situação geral.</summary>
    public List<FinalidadeConsentimentoDto> Finalidades { get; set; } = new();

    /// <summary>Todos os períodos, do mais recente para o mais antigo (inclui "Registro anterior").</summary>
    public List<ConsentimentoDto> Consentimentos { get; set; } = new();

    /// <summary>Telefones e e-mails ativos com a decisão por finalidade.</summary>
    public List<CanalPrivacidadeDto> Canais { get; set; } = new();
}

/// <summary>Situação de uma finalidade para a pessoa (sem canal específico: consentimento geral).</summary>
public sealed class FinalidadeConsentimentoDto
{
    public Guid FinalidadeId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public BaseLegal BaseLegal { get; set; }
    public SituacaoConsentimento Situacao { get; set; }
    public DateTime? Desde { get; set; }
}

/// <summary>Um período de consentimento (concedido e, se for o caso, revogado).</summary>
public sealed class ConsentimentoDto
{
    public Guid Id { get; set; }
    public Guid FinalidadeId { get; set; }
    public string Finalidade { get; set; } = string.Empty;
    /// <summary>Registro de antes das finalidades (somente histórico: não se revoga).</summary>
    public bool SomenteHistorico { get; set; }
    /// <summary>Nulo = qualquer canal.</summary>
    public CanalComunicacao? Canal { get; set; }
    public bool EmVigor { get; set; }
    public DateTime? ConcedidoEm { get; set; }
    public string? ConcedidoPor { get; set; }
    public string? Motivo { get; set; }
    public string? VersaoTermo { get; set; }
    public string? Origem { get; set; }
    public DateTime? RevogadoEm { get; set; }
    public string? RevogadoPor { get; set; }
    public string? MotivoRevogacao { get; set; }
}

/// <summary>Telefone/e-mail com as suas marcas e o resultado da regra central para cada finalidade.</summary>
public sealed class CanalPrivacidadeDto
{
    public Guid MeioContatoId { get; set; }
    public TipoContato Tipo { get; set; }
    public string Valor { get; set; } = string.Empty;
    public bool AceitaComunicacoes { get; set; }
    /// <summary>Só e-mail tem a marca "Uso para marketing" (nulo nos telefones).</summary>
    public bool? UsoParaMarketing { get; set; }
    public List<DecisaoCanalDto> Decisoes { get; set; } = new();
}

public sealed class DecisaoCanalDto
{
    public Guid FinalidadeId { get; set; }
    public string Finalidade { get; set; } = string.Empty;
    public CanalComunicacao Canal { get; set; }
    public ResultadoComunicacao Resultado { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Conceder: finalidade, canal opcional (nulo = qualquer canal), motivo obrigatório, versão e origem opcionais.</summary>
public sealed class ConcederConsentimentoRequisicao
{
    public Guid FinalidadeId { get; set; }
    public CanalComunicacao? Canal { get; set; }
    public string? Motivo { get; set; }
    public string? VersaoTermo { get; set; }
    public string? Origem { get; set; }
}

public sealed class RevogarConsentimentoRequisicao
{
    public string? Motivo { get; set; }
}

/// <summary>Finalidade de tratamento do cadastro (Configurações › Finalidades de tratamento).</summary>
public sealed class FinalidadeTratamentoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public BaseLegal BaseLegal { get; set; } = BaseLegal.Consentimento;
    public ClassificacaoCanal ClassificacaoExigida { get; set; }
    public bool SomenteHistorico { get; set; }
    public int Ordem { get; set; }
    public bool DoSistema { get; set; }
    public bool Ativo { get; set; } = true;
    /// <summary>Somente leitura: quantos períodos de consentimento usam a finalidade.</summary>
    public int QuantidadeUsos { get; set; }
}
