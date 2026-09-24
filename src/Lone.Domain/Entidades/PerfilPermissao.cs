using System.ComponentModel;
using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>Uma permissão concedida a um perfil (código do catálogo, ex.: "PESSOAS.EDITAR").</summary>
[DisplayName("Permissão")]
public class PerfilPermissao : IParteDeAgregado
{
    public Guid PerfilId { get; set; }

    [DisplayName("Permissão")]
    public string Codigo { get; set; } = string.Empty;

    string IParteDeAgregado.RaizEntidade => nameof(Perfil);
    Guid IParteDeAgregado.RaizId => PerfilId;
}
