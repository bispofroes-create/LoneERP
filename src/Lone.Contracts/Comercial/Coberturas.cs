using Lone.Domain.Enums;

namespace Lone.Contracts.Comercial;

/// <summary>Tipo de ausência (férias, folga, licença...).</summary>
public sealed class TipoAusenciaDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: coberturas (não canceladas) com este tipo.</summary>
    public int QuantidadeUsos { get; set; }
}

/// <summary>Parâmetros do módulo Comercial (um registro só).</summary>
public sealed class ParametrosComerciaisDto
{
    public byte[]? Versao { get; set; }
    public int DiasAvisoFimVinculo { get; set; } = 30;

    /// <summary>Até quantos dias antes de hoje valem o efeito de uma transferência e o início de uma cobertura.</summary>
    public int DiasRetroativosMaximo { get; set; } = 30;
    public RegraCreditoAusencia CreditoNaAusencia { get; set; }
    public decimal? PercentualSubstitutoPadrao { get; set; }
}

/// <summary>Cobertura de uma ausência: quem cobre a carteira do titular, de quando a quando, e o crédito das vendas.</summary>
public sealed class CoberturaDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public Guid TitularId { get; set; }
    public Guid TipoAusenciaId { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly FimEm { get; set; }
    public Guid? SubstitutoId { get; set; }
    public Guid? EquipeSubstitutaId { get; set; }
    public Guid? TipoCarteiraId { get; set; }
    public Guid? EmpresaId { get; set; }
    public RegraCreditoAusencia RegraCredito { get; set; }
    public decimal? PercentualSubstituto { get; set; }
    public bool PermiteAcesso { get; set; } = true;
    public string? Observacao { get; set; }
    public bool Cancelada { get; set; }
    public string? MotivoCancelamento { get; set; }

    /// <summary>Somente leitura: situação hoje e nomes, para a tela.</summary>
    public SituacaoCobertura Situacao { get; set; }
    public string? Titular { get; set; }
    public string? QuemCobre { get; set; }
    public string? TipoAusencia { get; set; }

    /// <summary>Somente leitura: clientes da carteira do titular no escopo da cobertura (vínculos vigentes no início).</summary>
    public int ClientesAfetados { get; set; }
}

/// <summary>Pedido de cancelamento (só antes de começar).</summary>
public sealed class CancelarCoberturaRequisicao
{
    public byte[]? Versao { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>Escolhas da tela de coberturas.</summary>
public sealed class CoberturaOpcoesDto
{
    /// <summary>Quem pode ser titular ou cobrir: pessoas que podem ocupar algum papel comercial (com classificações).</summary>
    public List<AtendenteOpcaoDto> Pessoas { get; set; } = new();
    public List<TipoAusenciaDto> TiposAusencia { get; set; } = new();
    public List<TipoCarteiraDto> Papeis { get; set; } = new();
    public List<Lone.Contracts.Colaboradores.PessoaOpcaoDto> Equipes { get; set; } = new();
    public List<Lone.Contracts.Empresas.EmpresaResumo> Empresas { get; set; } = new();
    public ParametrosComerciaisDto Parametros { get; set; } = new();
}

/// <summary>Cobertura vigente ou agendada, para o aviso na carteira da ficha do cliente.</summary>
public sealed record CoberturaAvisoDto(Guid TitularId, Guid? TipoCarteiraId, Guid? EmpresaId, DateOnly InicioEm, DateOnly FimEm, string Texto);

/// <summary>Vínculo da carteira que termina em breve (tela "Carteira vencendo").</summary>
public sealed class VinculoVencendoDto
{
    public Guid VinculoId { get; set; }
    public Guid ClienteId { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public string Papel { get; set; } = string.Empty;
    public string Pessoa { get; set; } = string.Empty;
    public string? Empresa { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly FimEm { get; set; }
    public int DiasRestantes { get; set; }
}
