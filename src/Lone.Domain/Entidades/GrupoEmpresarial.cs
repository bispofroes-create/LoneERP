using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>
/// Grupo empresarial: conjunto de pessoas jurídicas independentes (CNPJs com raízes diferentes) que o usuário quer ver e
/// administrar juntas (ex.: "Grupo João" com ABC Comércio e XYZ Transportes). Não substitui a pessoa jurídica nem vira
/// matriz/filial: cada empresa continua com seus estabelecimentos. É opcional e sempre explícito (nada é deduzido de
/// sócios em comum). É o único grupo de empresas do Lone (decisão MC-4): o antigo <see cref="GrupoEconomico"/> saiu do código.
/// Nunca é excluído: desativado, some das escolhas novas e continua nas empresas que já o têm.
/// </summary>
[DisplayName("Grupo empresarial")]
public class GrupoEmpresarial : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 100;
    public const int TamanhoMaximoDescricao = 250;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Grupo empresarial '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Grupo empresarial '{Nome}' reativado.");
    }
}
