namespace Lone.Aplicacao.Integracoes;

public class DadosCep
{
    public string Cep { get; set; } = string.Empty;
    public string? Logradouro { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? CodigoMunicipioIbge { get; set; }
}
