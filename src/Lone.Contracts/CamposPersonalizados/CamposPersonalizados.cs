using Lone.Domain.Enums;

namespace Lone.Contracts.CamposPersonalizados;

/// <summary>Definição de um campo personalizado, como trafega entre o aplicativo e a API.</summary>
public sealed class CampoPersonalizadoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public EntidadePersonalizavel Entidade { get; set; } = EntidadePersonalizavel.Pessoa;
    public string Nome { get; set; } = string.Empty;
    public TipoCampoPersonalizado Tipo { get; set; } = TipoCampoPersonalizado.Texto;
    public bool Obrigatorio { get; set; }
    public bool Ativo { get; set; } = true;
    public int Ordem { get; set; }
    public string? Dica { get; set; }
    public byte? CasasDecimais { get; set; }
    public decimal? Minimo { get; set; }
    public decimal? Maximo { get; set; }
    public List<OpcaoCampoDto> Opcoes { get; set; } = new();

    /// <summary>Somente leitura: já há valores gravados (o tipo não pode mais mudar).</summary>
    public bool TemValores { get; set; }
}

public sealed class OpcaoCampoDto
{
    public Guid Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public bool Ativa { get; set; } = true;
}

/// <summary>Valor de um campo personalizado. Só a propriedade do tipo do campo é usada; as outras vão nulas.</summary>
public sealed class ValorPersonalizadoDto
{
    public Guid CampoId { get; set; }
    public string? Texto { get; set; }
    public decimal? Numero { get; set; }

    /// <summary>Data (e hora, no tipo "data e hora") como digitada, sem fuso.</summary>
    public DateTime? Data { get; set; }
    public bool? Logico { get; set; }
    public Guid? OpcaoId { get; set; }
}

/// <summary>Versão aberta e motivo de uma ação de situação (desativar/reativar um campo ou um cadastro).</summary>
public sealed class AlterarSituacaoRequisicao
{
    public byte[]? Versao { get; set; }
    public string? Motivo { get; set; }
}
