namespace Lone.Aplicacao.Integracoes;

/// <summary>Dados de uma empresa devolvidos pela consulta de CNPJ, já normalizados.</summary>
public class DadosCnpj
{
    public string Cnpj { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }
    public string? SituacaoCadastral { get; set; }
    public bool EhMatriz { get; set; }

    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? CodigoMunicipioIbge { get; set; }

    public string? Telefone { get; set; }
    public string? Email { get; set; }

    /// <summary>Nome do serviço que respondeu (ex.: "BrasilAPI").</summary>
    public string Fonte { get; set; } = string.Empty;
}
