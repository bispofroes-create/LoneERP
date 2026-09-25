namespace Lone.Contracts.Profissoes;

/// <summary>Profissão do cadastro, como trafega entre o aplicativo e a API.</summary>
public sealed class ProfissaoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    /// <summary>Código CBO de 6 dígitos (nulo = sem ocupação CBO).</summary>
    public int? OcupacaoCboId { get; set; }

    /// <summary>Somente leitura: "2410-05 · Advogado".</summary>
    public string? OcupacaoCboTexto { get; set; }

    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: quantos cadastros têm esta profissão (só para quem gerencia profissões).</summary>
    public int QuantidadePessoas { get; set; }
}

/// <summary>Mesclar: as pessoas da profissão de origem (a da URL) passam para a de destino, e a origem é desativada.</summary>
public sealed class MesclarProfissaoRequisicao
{
    public Guid DestinoId { get; set; }

    /// <summary>Versão da profissão de origem que o usuário via (se mudou, dá conflito).</summary>
    public byte[]? Versao { get; set; }
}

public sealed class ResultadoMesclarProfissao
{
    public ProfissaoDto Origem { get; set; } = new();
    public ProfissaoDto Destino { get; set; } = new();
    public int CadastrosAlterados { get; set; }
}

/// <summary>Ocupação da CBO 2002 (Id = código de 6 dígitos).</summary>
public sealed class OcupacaoCboDto
{
    public int Codigo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string CodigoFormatado => Codigo.ToString("0000-00", System.Globalization.CultureInfo.InvariantCulture);
    public string Texto => $"{CodigoFormatado} · {Titulo}";
    public override string ToString() => Texto;
}

public sealed class SituacaoCbo
{
    public int Quantidade { get; set; }
    public DateTime? AtualizadaEm { get; set; }
}

/// <summary>Arquivo oficial "CBO2002 - Ocupacao.csv" (bytes; vai como base64 no JSON).</summary>
public sealed class ImportarCboRequisicao
{
    public string? NomeArquivo { get; set; }
    public byte[] Arquivo { get; set; } = [];
}

public sealed class ResultadoImportacaoCbo
{
    public int Lidas { get; set; }
    public int Incluidas { get; set; }
    public int Alteradas { get; set; }
    public int Desativadas { get; set; }
    public int LinhasIgnoradas { get; set; }
}
