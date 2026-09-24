using System.ComponentModel;

namespace Lone.Core.Entidades;

/// <summary>Tipo de vínculo entre pessoas, cadastrável (ex.: "Sócio de" / "Tem como sócio").</summary>
[DisplayName("Tipo de relacionamento")]
public class TipoRelacionamento : EntidadeBase
{
    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Como o vínculo aparece do outro lado.</summary>
    [DisplayName("Nome inverso")]
    public string NomeInverso { get; set; } = string.Empty;

    /// <summary>Criado pelo sistema (não pode ser excluído).</summary>
    public bool Sistema { get; set; }
}

/// <summary>Vínculo: esta pessoa (origem) → outra pessoa (destino), de um tipo, num período.</summary>
[DisplayName("Relacionamento")]
public class PessoaRelacionamento : EntidadePessoaFilha
{
    [DisplayName("Pessoa relacionada")]
    public int PessoaDestinoId { get; set; }

    [DisplayName("Tipo")]
    public int TipoRelacionamentoId { get; set; }
    public TipoRelacionamento? TipoRelacionamento { get; set; }

    [DisplayName("Início")]
    public DateOnly? InicioEm { get; set; }

    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }
}
