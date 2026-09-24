using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>Conjunto de permissões atribuído a usuários (ex.: "Vendedor", "Financeiro").</summary>
[DisplayName("Perfil")]
public class Perfil : AgregadoRaiz
{
    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    /// <summary>Administrador tem todas as permissões, inclusive as criadas no futuro.</summary>
    [DisplayName("Administrador")]
    public bool Administrador { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public List<PerfilPermissao> Permissoes { get; set; } = new();
}
