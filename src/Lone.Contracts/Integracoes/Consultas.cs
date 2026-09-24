using Lone.Contracts.Pessoas;

namespace Lone.Contracts.Integracoes;

public sealed class DadosCep
{
    public string Cep { get; set; } = string.Empty;
    public string? Logradouro { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? CodigoMunicipioIbge { get; set; }
}

/// <summary>Dados de uma empresa devolvidos pela consulta de CNPJ, já normalizados.</summary>
public sealed class DadosCnpj
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

    // ---- Dados da empresa ----
    public DateOnly? DataAbertura { get; set; }
    public string? Porte { get; set; }
    public decimal? CapitalSocial { get; set; }
    public bool? OpcaoSimples { get; set; }
    public bool? OpcaoMei { get; set; }
    public string? CnaePrincipal { get; set; }
    public string? NaturezaJuridica { get; set; }
    public List<string> CnaesSecundarios { get; set; } = new();
    public List<SocioDto> Socios { get; set; } = new();

    /// <summary>Inscrições estaduais encontradas (fonte pública complementar; pode vir vazia).</summary>
    public List<InscricaoEstadualEncontrada> InscricoesEstaduais { get; set; } = new();

    /// <summary>Nome do serviço que respondeu (ex.: "BrasilAPI").</summary>
    public string Fonte { get; set; } = string.Empty;
}

/// <summary>Uma inscrição estadual do CNPJ num estado.</summary>
public sealed record InscricaoEstadualEncontrada(string Uf, string Numero, bool Ativa);
