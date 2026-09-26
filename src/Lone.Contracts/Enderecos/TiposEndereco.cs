namespace Lone.Contracts.Enderecos;

/// <summary>Tipo (classificação) de endereço do cadastro de tipos.</summary>
public sealed class TipoEnderecoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: quantos endereços ativos usam este tipo (só para quem gerencia os tipos).</summary>
    public int QuantidadeUsos { get; set; }
}

/// <summary>Finalidade de endereço do cadastro (Comercial, Residencial, Fiscal...). "Principal" não é finalidade.</summary>
public sealed class FinalidadeEnderecoDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;
}

/// <summary>Pessoa com endereços possivelmente duplicados (rotina de identificação; nada é alterado).</summary>
public sealed class EnderecosDuplicadosDto
{
    public Guid PessoaId { get; set; }
    public int Codigo { get; set; }
    public string Nome { get; set; } = string.Empty;
    public List<string> Enderecos { get; set; } = new();
}

/// <summary>Página da rotina de endereços duplicados (paginação por Id da pessoa).</summary>
public sealed class PaginaEnderecosDuplicados
{
    public List<EnderecosDuplicadosDto> Itens { get; set; } = new();
    public Guid? ProximoId { get; set; }
}

/// <summary>
/// Intenção de consolidar um endereço duplicado (origem) em outro (destino) da mesma pessoa. O servidor carrega o
/// estado gravado, confere tudo e decide as finalidades resultantes; Versao = a que o usuário tinha aberta.
/// </summary>
public sealed class ConsolidarEnderecosRequisicao
{
    public byte[]? Versao { get; set; }
    public Guid OrigemId { get; set; }
    public Guid DestinoId { get; set; }
}
