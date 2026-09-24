using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>De onde veio um texto de município que precisa ser ligado à tabela do IBGE.</summary>
public enum OrigemPendenciaMunicipio : byte
{
    Naturalidade = 0,
    Endereco = 1
}

/// <summary>
/// Texto de município gravado antes da tabela do IBGE (ex.: "Curvelo - MG"). A conciliação tenta ligar cada um ao
/// município certo; o que não der fica aqui, com o texto original, até alguém corrigir no cadastro.
/// Nada é apagado: a pendência resolvida guarda quando e como foi resolvida.
/// </summary>
[NaoAuditar]
public class PendenciaMunicipio
{
    public Guid Id { get; set; }
    public Guid PessoaId { get; set; }
    public OrigemPendenciaMunicipio Origem { get; set; }

    /// <summary>Id da pessoa (naturalidade) ou do endereço.</summary>
    public Guid RegistroId { get; set; }

    public string TextoOriginal { get; set; } = string.Empty;
    public string? UfOriginal { get; set; }

    /// <summary>Código IBGE que já estava gravado no endereço (vindo do CEP), quando havia.</summary>
    public string? CodigoIbgeOriginal { get; set; }

    /// <summary>Por que a conciliação automática não resolveu (ex.: "existe em 3 estados").</summary>
    public string? Observacao { get; set; }

    public DateTime CriadaEm { get; set; }
    public DateTime? ResolvidaEm { get; set; }

    /// <summary>"Automática" (conciliação) ou o usuário que escolheu o município no cadastro.</summary>
    public string? ResolvidaPor { get; set; }

    public int? MunicipioId { get; set; }

    public bool Resolvida => ResolvidaEm is not null;
}
