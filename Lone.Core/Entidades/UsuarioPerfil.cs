using System.ComponentModel;
using Lone.Core.Auditoria;

namespace Lone.Core.Entidades;

/// <summary>Perfil atribuído a um usuário, numa empresa do grupo (EmpresaId nulo = em todas as empresas).</summary>
[DisplayName("Perfil do usuário")]
public class UsuarioPerfil : IParteDeAgregado
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }

    [DisplayName("Perfil")]
    public int PerfilId { get; set; }
    public Perfil? Perfil { get; set; }

    /// <summary>Pessoa com papel EmpresaDoGrupo. Nulo = o perfil vale em todas as empresas.</summary>
    [DisplayName("Empresa")]
    public int? EmpresaId { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Usuario);
    int IParteDeAgregado.RaizId => UsuarioId;
}
