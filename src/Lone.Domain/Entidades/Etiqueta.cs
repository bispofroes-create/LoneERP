using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>
/// Etiqueta reutilizável do cadastro de pessoas (ex.: "Cliente VIP", "Não enviar marketing").
/// Nunca é excluída: desativada, some das escolhas novas mas continua em quem já tem e no histórico.
/// O nome é único sem diferenciar maiúsculas nem acentos (garantido pelo banco, com collation CI_AI).
/// </summary>
[DisplayName("Etiqueta")]
public class Etiqueta : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 40;
    public const int TamanhoMaximoDescricao = 150;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Etiqueta '{Nome}' desativada.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Etiqueta '{Nome}' reativada.");
    }
}
