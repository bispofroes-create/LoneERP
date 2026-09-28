using System.ComponentModel;
using Lone.Domain.Enums;

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

    /// <summary>Até onde quem tem este perfil enxerga em Pessoas e no Comercial (ignorado no administrador, que vê tudo).</summary>
    [DisplayName("Alcance em Pessoas e no Comercial")]
    public AlcanceComercial AlcanceComercial { get; set; }

    public List<PerfilPermissao> Permissoes { get; set; } = new();
}
