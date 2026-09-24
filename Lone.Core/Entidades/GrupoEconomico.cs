using System.ComponentModel;

namespace Lone.Core.Entidades;

/// <summary>Conjunto de empresas relacionadas (ex.: "Grupo ABC"). Cada CNPJ continua sendo uma pessoa.</summary>
[DisplayName("Grupo econômico")]
public class GrupoEconomico : AgregadoRaiz
{
    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }
}
