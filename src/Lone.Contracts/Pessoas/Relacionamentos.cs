using Lone.Contracts.GruposEmpresariais;
using Lone.Domain.Enums;

namespace Lone.Contracts.Pessoas;

/// <summary>
/// Relacionamento entre pessoas visto de uma delas (ex.: na ficha de João, "Sócio de ABC"; na ficha da ABC, "Tem como
/// sócio João"). Gravado por operações próprias, fora do "Salvar" da ficha.
/// </summary>
public sealed class PessoaRelacionamentoDto
{
    public Guid Id { get; set; }
    public Guid TipoRelacionamentoId { get; set; }

    /// <summary>Texto do tipo do ponto de vista da ficha aberta ("Sócio de" ou "Tem como sócio").</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Verdadeiro quando a ficha aberta é o destino do vínculo (o texto vem do nome inverso).</summary>
    public bool Inverso { get; set; }

    public Guid OutraPessoaId { get; set; }
    public string OutraPessoaNome { get; set; } = string.Empty;
    public NaturezaPessoa OutraPessoaNatureza { get; set; }
    public DateOnly? InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public string? Observacoes { get; set; }

    /// <summary>Falso = desativado (lançado por engano); continua no histórico.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: ativo e dentro do período hoje.</summary>
    public bool Vigente { get; set; }

    /// <summary>Somente leitura: vínculo societário (sócio, administrador), que exige a permissão de estrutura empresarial.</summary>
    public bool Societario { get; set; }
}

/// <summary>
/// Inclui um relacionamento a partir da ficha aberta. <see cref="Inverso"/> = a ficha aberta é o destino (ex.: na ficha
/// da ABC, "Tem como sócio" João grava João → "Sócio de" → ABC).
/// </summary>
public sealed class IncluirRelacionamentoRequisicao
{
    public Guid TipoRelacionamentoId { get; set; }
    public bool Inverso { get; set; }
    public Guid OutraPessoaId { get; set; }
    public DateOnly? InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public string? Observacoes { get; set; }
}

/// <summary>Encerra um relacionamento (preenche o fim; o vínculo continua gravado).</summary>
public sealed class EncerrarRelacionamentoRequisicao
{
    public DateOnly? FimEm { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>Desativa um relacionamento lançado por engano (continua no histórico).</summary>
public sealed class DesativarRelacionamentoRequisicao
{
    public string? Motivo { get; set; }
}

/// <summary>Tipo de relacionamento (os de sistema e os que forem criados), com os dois sentidos.</summary>
public sealed class TipoRelacionamentoDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string NomeInverso { get; set; } = string.Empty;
    public bool Societario { get; set; }
}

/// <summary>Opções da estrutura empresarial na ficha: tipos de relacionamento e grupos empresariais (com os desativados).</summary>
public sealed class EstruturaEmpresarialOpcoesDto
{
    public List<TipoRelacionamentoDto> TiposRelacionamento { get; set; } = new();
    public List<GrupoEmpresarialDto> GruposEmpresariais { get; set; } = new();
}
