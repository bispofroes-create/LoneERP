using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Uma linha da auditoria do ERP: quem mudou o quê, quando, de onde veio, valores antes/depois.
/// Serve para qualquer módulo: a "raiz" agrupa tudo que pertence ao mesmo agregado.
/// </summary>
public class RegistroAuditoria
{
    public long Id { get; set; }
    public DateTime DataHora { get; set; }
    public string Usuario { get; set; } = string.Empty;

    /// <summary>Agrupa as linhas geradas por uma mesma gravação.</summary>
    public Guid OperacaoId { get; set; }
    public OrigemAlteracao Origem { get; set; }

    /// <summary>Entidade alterada (Pessoa, PessoaEndereco, Cliente...).</summary>
    public string Entidade { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;

    /// <summary>Agregado a que a entidade pertence (ex.: Pessoa 125), para montar o histórico dele.</summary>
    public string RaizEntidade { get; set; } = string.Empty;
    public Guid? RaizId { get; set; }

    public AcaoAuditoria Acao { get; set; }
    public string? Campo { get; set; }
    public string? ValorAnterior { get; set; }
    public string? ValorNovo { get; set; }

    /// <summary>Texto do evento de negócio (só quando Acao = Evento).</summary>
    public string? Descricao { get; set; }
}
