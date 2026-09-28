using Lone.Domain.Enums;

namespace Lone.Contracts.Metas;

public sealed class EquipeDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public Guid? DepartamentoId { get; set; }

    /// <summary>Somente leitura: o líder de hoje (membro com papel Líder); o servidor ignora o que vier aqui.</summary>
    public Guid? LiderId { get; set; }

    /// <summary>Somente leitura: nome do líder.</summary>
    public string? Lider { get; set; }

    /// <summary>Equipe acima desta na hierarquia (nula = topo).</summary>
    public Guid? EquipePaiId { get; set; }

    /// <summary>Somente leitura: nome da equipe acima.</summary>
    public string? EquipePai { get; set; }
    public bool Ativo { get; set; } = true;
    public List<MembroEquipeDto> Membros { get; set; } = new();
}

public sealed class MembroEquipeDto
{
    public Guid Id { get; set; }
    public Guid PessoaId { get; set; }

    /// <summary>Somente leitura: nome da pessoa.</summary>
    public string? Pessoa { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public Lone.Domain.Enums.PapelNaEquipe Papel { get; set; }
}

public sealed class IndicadorDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public FonteIndicador Fonte { get; set; }
    public UnidadeIndicador Unidade { get; set; }
    public SentidoIndicador Sentido { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: indicador de sistema (fonte do cadastro; não muda de fonte nem de código).</summary>
    public bool DoSistema { get; set; }
}

public sealed class MetaResumoDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateOnly InicioEm { get; set; }
    public DateOnly FimEm { get; set; }
    public SituacaoMeta Situacao { get; set; }
    public int Participantes { get; set; }
    public bool Ativo { get; set; }
}

public sealed class MetaDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly FimEm { get; set; }
    public SituacaoMeta Situacao { get; set; }
    public decimal LimiteAtingimento { get; set; } = 150;
    public bool Ativo { get; set; } = true;
    public DateTime? FechadaEm { get; set; }
    public string? FechadaPor { get; set; }
    public List<MetaItemDto> Itens { get; set; } = new();
    public List<MetaFaixaDto> Faixas { get; set; } = new();
    public List<MetaParticipanteDto> Participantes { get; set; } = new();
    public List<MetaAlvoDto> Alvos { get; set; } = new();
}

public sealed class MetaItemDto
{
    public Guid Id { get; set; }
    public Guid IndicadorId { get; set; }
    public decimal Peso { get; set; }
    public int Ordem { get; set; }
}

public sealed class MetaFaixaDto
{
    public Guid Id { get; set; }
    public decimal InicioPercentual { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal PercentualPremio { get; set; }
}

public sealed class MetaParticipanteDto
{
    public Guid Id { get; set; }
    public NivelParticipante Nivel { get; set; }
    public Guid ReferenciaId { get; set; }

    /// <summary>Somente leitura: nome do participante.</summary>
    public string? Nome { get; set; }
    public decimal? NotaFinal { get; set; }
    public string? Faixa { get; set; }
    public decimal? PercentualPremio { get; set; }
}

public sealed class MetaAlvoDto
{
    public Guid Id { get; set; }
    public Guid ParticipanteId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Alvo { get; set; }
    public decimal? Realizado { get; set; }
    public OrigemRealizado? OrigemRealizado { get; set; }
    public DateTime? RealizadoEm { get; set; }
    public string? RealizadoPor { get; set; }
}

/// <summary>Mudança de situação da meta (publicar, apurar, fechar, reabrir...). Reabrir exige motivo.</summary>
public sealed class AlterarSituacaoMetaRequisicao
{
    public byte[]? Versao { get; set; }
    public SituacaoMeta Situacao { get; set; }
    public string? Motivo { get; set; }
}

public sealed class LancamentoRealizadoDto
{
    public Guid ParticipanteId { get; set; }
    public Guid ItemId { get; set; }

    /// <summary>Nulo = apaga o valor informado (volta a "sem realizado"; fica na auditoria).</summary>
    public decimal? Valor { get; set; }
}

public sealed class LancarRealizadoRequisicao
{
    public byte[]? Versao { get; set; }
    public List<LancamentoRealizadoDto> Lancamentos { get; set; } = new();
    public string? Motivo { get; set; }
}

/// <summary>Importação do realizado de um item por CSV ("participante;valor", uma linha por participante).</summary>
public sealed class ImportarRealizadoRequisicao
{
    public byte[]? Versao { get; set; }
    public Guid ItemId { get; set; }
    public string Conteudo { get; set; } = string.Empty;

    /// <summary>Falso = só confere e mostra o que seria gravado.</summary>
    public bool Confirmar { get; set; }
    public string? Motivo { get; set; }
}

public sealed class LinhaImportacaoRealizadoDto
{
    public int Linha { get; set; }
    public string Participante { get; set; } = string.Empty;
    public decimal? Valor { get; set; }
    public Guid? ParticipanteId { get; set; }
    public string? Erro { get; set; }
}

public sealed class ResultadoImportacaoRealizadoDto
{
    public List<LinhaImportacaoRealizadoDto> Linhas { get; set; } = new();
    public int Validas { get; set; }
    public bool Gravado { get; set; }
}

public sealed class ApuracaoItemDto
{
    public Guid ItemId { get; set; }
    public string Indicador { get; set; } = string.Empty;
    public decimal? Alvo { get; set; }
    public decimal? Realizado { get; set; }
    public OrigemRealizado? Origem { get; set; }
    public decimal? Atingimento { get; set; }
}

public sealed class ApuracaoParticipanteDto
{
    public Guid ParticipanteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public NivelParticipante Nivel { get; set; }
    public decimal Nota { get; set; }
    public string? Faixa { get; set; }
    public decimal? PercentualPremio { get; set; }
    public List<ApuracaoItemDto> Itens { get; set; } = new();
}

/// <summary>Resultado da meta: calculado agora (até fechar) ou o congelado no fechamento.</summary>
public sealed class ApuracaoDto
{
    public Guid MetaId { get; set; }
    public SituacaoMeta Situacao { get; set; }
    public bool Congelada { get; set; }
    public List<ApuracaoParticipanteDto> Participantes { get; set; } = new();
    public List<string> Avisos { get; set; } = new();
}

/// <summary>Opção de participante para escolher (nome e o cadastro de origem).</summary>
public sealed record ParticipanteOpcaoDto(NivelParticipante Nivel, Guid Id, string Nome);

/// <summary>Tudo que a tela de metas precisa para as escolhas, numa chamada só.</summary>
public sealed class MetaOpcoesDto
{
    public List<IndicadorDto> Indicadores { get; set; } = new();
    public List<ParticipanteOpcaoDto> Participantes { get; set; } = new();
}
