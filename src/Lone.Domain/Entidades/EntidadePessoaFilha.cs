using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>Base para registros que pertencem a uma pessoa (endereços, contatos...).</summary>
public abstract class EntidadePessoaFilha : EntidadeBase, IParteDeAgregado
{
    public Guid PessoaId { get; set; }

    // Explícito: informa a auditoria sem virar coluna no banco.
    string IParteDeAgregado.RaizEntidade => nameof(Pessoa);
    Guid IParteDeAgregado.RaizId => PessoaId;
}
