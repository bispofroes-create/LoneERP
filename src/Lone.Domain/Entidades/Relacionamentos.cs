using System.ComponentModel;

namespace Lone.Domain.Entidades;

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
    public Guid PessoaDestinoId { get; set; }

    [DisplayName("Tipo")]
    public Guid TipoRelacionamentoId { get; set; }
    public TipoRelacionamento? TipoRelacionamento { get; set; }

    [DisplayName("Início")]
    public DateOnly? InicioEm { get; set; }

    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }
}

/// <summary>Ids fixos dos tipos de relacionamento criados pelo sistema (iguais em todo banco do Lone).</summary>
public static class TiposRelacionamentoSistema
{
    public static readonly Guid SocioDe = new("5a0e6f10-0000-0000-0000-000000000001");
    public static readonly Guid ResponsavelPor = new("5a0e6f10-0000-0000-0000-000000000002");
    public static readonly Guid RepresentanteDe = new("5a0e6f10-0000-0000-0000-000000000003");
    public static readonly Guid ContatoDe = new("5a0e6f10-0000-0000-0000-000000000004");
    public static readonly Guid DependenteDe = new("5a0e6f10-0000-0000-0000-000000000005");
    public static readonly Guid FuncionarioDe = new("5a0e6f10-0000-0000-0000-000000000006");
}
