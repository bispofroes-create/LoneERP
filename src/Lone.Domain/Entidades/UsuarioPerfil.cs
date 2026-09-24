using System.ComponentModel;
using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>Perfil atribuído a um usuário, numa empresa do grupo (EmpresaId nulo = em todas as empresas).</summary>
[DisplayName("Perfil do usuário")]
public class UsuarioPerfil : IParteDeAgregado
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }

    [DisplayName("Perfil")]
    public Guid PerfilId { get; set; }
    public Perfil? Perfil { get; set; }

    /// <summary>Pessoa com papel EmpresaDoGrupo. Nulo = o perfil vale em todas as empresas.</summary>
    [DisplayName("Empresa")]
    public Guid? EmpresaId { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Usuario);
    Guid IParteDeAgregado.RaizId => UsuarioId;
}
