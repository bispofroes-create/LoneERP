using System.ComponentModel;
using Lone.Core.Auditoria;

namespace Lone.Core.Entidades;

/// <summary>Uma permissão concedida a um perfil (código do catálogo, ex.: "PESSOAS.EDITAR").</summary>
[DisplayName("Permissão")]
public class PerfilPermissao : IParteDeAgregado
{
    public int PerfilId { get; set; }

    [DisplayName("Permissão")]
    public string Codigo { get; set; } = string.Empty;

    string IParteDeAgregado.RaizEntidade => nameof(Perfil);
    int IParteDeAgregado.RaizId => PerfilId;
}
