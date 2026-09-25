using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>
/// Classificação livre de endereço (ex.: "Sede", "Depósito", "Filial Centro"). Não substitui as finalidades
/// (principal, fiscal, cobrança, entrega...), que têm regra no sistema. Nunca é excluído: desativado, some das
/// escolhas novas mas continua nos endereços que já o têm. Nome único sem diferenciar maiúsculas nem acentos.
/// </summary>
[DisplayName("Tipo de endereço")]
public class TipoEndereco : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 40;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Tipo de endereço '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Tipo de endereço '{Nome}' reativado.");
    }
}
