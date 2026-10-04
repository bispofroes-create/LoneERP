using Lone.Domain.Entidades;

namespace Lone.Contracts.Comum;

/// <summary>Prazo pronto do campo Prazo ("30 dias", "6 meses", "1 ano"). Todos leem; quem altera parâmetros mantém.</summary>
public sealed class PrazoPeriodoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public int Quantidade { get; set; }
    public UnidadePrazo Unidade { get; set; } = UnidadePrazo.Dias;
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: "30 dias", "1 ano".</summary>
    public string Nome { get; set; } = string.Empty;
}
