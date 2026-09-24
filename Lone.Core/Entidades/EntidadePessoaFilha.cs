using Lone.Core.Auditoria;

namespace Lone.Core.Entidades;

/// <summary>Base para registros que pertencem a uma pessoa (endereços, contatos...).</summary>
public abstract class EntidadePessoaFilha : EntidadeBase, IParteDeAgregado
{
    public int PessoaId { get; set; }

    // Explícito: informa a auditoria sem virar coluna no banco.
    string IParteDeAgregado.RaizEntidade => nameof(Pessoa);
    int IParteDeAgregado.RaizId => PessoaId;
}
