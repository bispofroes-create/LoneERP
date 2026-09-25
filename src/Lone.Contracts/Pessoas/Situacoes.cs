using Lone.Domain.Enums;

namespace Lone.Contracts.Pessoas;

/// <summary>Bloqueio novo (ação própria, fora do "Salvar" da ficha).</summary>
public sealed class BloquearRequisicao
{
    /// <summary>Empresa do grupo; nulo = todas.</summary>
    public Guid? EmpresaId { get; set; }
    public EscopoBloqueio Escopo { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

public sealed class LiberarBloqueioRequisicao
{
    public string Motivo { get; set; } = string.Empty;
}

public sealed class InteracaoDto
{
    public Guid Id { get; set; }
    public DateTime DataHora { get; set; }
    public TipoInteracao Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
}

public sealed class RegistrarInteracaoRequisicao
{
    public TipoInteracao Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Quando aconteceu (nulo = agora). Não pode ser no futuro.</summary>
    public DateTime? DataHora { get; set; }
}

/// <summary>Relacionamento da pessoa (somente leitura na ficha): última interação, situação e as mais recentes.</summary>
public sealed class RelacionamentoDto
{
    public DateTime? UltimaInteracaoEm { get; set; }
    public SituacaoRelacionamento Situacao { get; set; }
    public int DiasEmRisco { get; set; }
    public int DiasInativo { get; set; }
    public List<InteracaoDto> Interacoes { get; set; } = new();
}

public sealed class ParametrosRelacionamentoDto
{
    public byte[]? Versao { get; set; }
    public int DiasEmRisco { get; set; }
    public int DiasInativo { get; set; }
}
