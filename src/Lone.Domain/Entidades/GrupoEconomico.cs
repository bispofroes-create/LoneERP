using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>
/// LEGADO (decisão MC-4 do Motor Comercial, 28/09/2026): o grupo de empresas do Lone é o <see cref="GrupoEmpresarial"/>.
/// Esta classe só mantém mapeada a tabela antiga "GruposEconomicos" (vazia), para não gerar migration nem apagar nada;
/// nenhuma tela, API ou regra usa. Não criar usos novos.
/// </summary>
[DisplayName("Grupo econômico")]
public class GrupoEconomico : AgregadoRaiz
{
    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }
}
