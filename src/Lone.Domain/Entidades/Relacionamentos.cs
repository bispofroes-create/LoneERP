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

/// <summary>
/// Vínculo: esta pessoa (origem) → outra pessoa (destino), de um tipo, num período (ex.: João → "Sócio de" → ABC).
/// Visto do destino, aparece pelo nome inverso ("ABC tem como sócio João"). Gravado por operações próprias (fora do
/// Salvar da ficha) e nunca apagado: encerrar preenche o fim; lançado por engano fica inativo (histórico).
/// Uma pessoa pode ter vários vínculos com empresas diferentes: nada aqui limita a uma empresa por pessoa.
/// </summary>
[DisplayName("Relacionamento")]
public class PessoaRelacionamento : EntidadePessoaFilha
{
    public const int TamanhoMaximoObservacoes = 250;

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

    /// <summary>Falso = lançado por engano (desativado). Continua gravado para o histórico.</summary>
    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>Ativo e dentro do período na data informada (sem início = desde sempre; sem fim = em aberto).</summary>
    public bool Vigente(DateOnly data) =>
        Ativo && (InicioEm is null || InicioEm <= data) && (FimEm is null || FimEm >= data);
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
    public static readonly Guid AdministradorDe = new("5a0e6f10-0000-0000-0000-000000000007");
    public static readonly Guid ParceiroDe = new("5a0e6f10-0000-0000-0000-000000000008");

    /// <summary>Vínculos societários: exigem a permissão de estrutura empresarial e têm uma empresa como destino.</summary>
    public static IReadOnlySet<Guid> Societarios { get; } = new HashSet<Guid> { SocioDe, AdministradorDe };
}
