using System.ComponentModel;
using Lone.Core.Auditoria;

namespace Lone.Core.Entidades;

/// <summary>
/// Base dos papéis de uma pessoa. A chave é o próprio PessoaId (1 para 1).
/// Desmarcar um papel apenas o inativa, para não perder o histórico.
/// </summary>
public abstract class PapelBase : IParteDeAgregado
{
    public int PessoaId { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Pessoa);
    int IParteDeAgregado.RaizId => PessoaId;
}
