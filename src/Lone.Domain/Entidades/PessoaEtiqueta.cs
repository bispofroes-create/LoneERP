using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>Etiqueta livre para segmentar o cadastro (ex.: "Atacado", "VIP", "Região Sul").</summary>
[DisplayName("Etiqueta")]
public class PessoaEtiqueta : EntidadePessoaFilha
{
    public const int TamanhoMaximo = 40;

    [DisplayName("Etiqueta")]
    public string Texto { get; set; } = string.Empty;
}
