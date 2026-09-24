namespace Lone.Contracts.Municipios;

/// <summary>Município da tabela do IBGE (Id = código IBGE).</summary>
public sealed class MunicipioDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;

    public string NomeComUf => $"{Nome} - {Uf}";
    public override string ToString() => NomeComUf;
}

/// <summary>Situação da tabela de municípios (carga do IBGE e textos antigos ainda sem município).</summary>
public sealed class SituacaoMunicipios
{
    public int Quantidade { get; set; }
    public DateTime? AtualizadaEm { get; set; }
    public int PendenciasAbertas { get; set; }
}

/// <summary>Resultado de uma atualização da tabela pelo IBGE.</summary>
public sealed class ResultadoAtualizacaoMunicipios
{
    public int Incluidos { get; set; }
    public int Alterados { get; set; }
    public int Desativados { get; set; }
    public int PendenciasResolvidas { get; set; }
    public int PendenciasAbertas { get; set; }
}
