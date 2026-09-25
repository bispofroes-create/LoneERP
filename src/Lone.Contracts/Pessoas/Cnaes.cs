namespace Lone.Contracts.Pessoas;

/// <summary>Subclasse CNAE (tabela oficial do IBGE).</summary>
public sealed record CnaeDto(int Codigo, string CodigoFormatado, string Descricao, bool Ativo);

/// <summary>Quantidade de CNAEs carregados e quando foi a última carga.</summary>
public sealed class SituacaoCnaes
{
    public int Quantidade { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}

public sealed class ResultadoAtualizacaoCnaes
{
    public int Incluidos { get; set; }
    public int Alterados { get; set; }
    public int Desativados { get; set; }
}
