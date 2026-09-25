using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>
/// Profissão do cadastro de pessoas (ex.: "Advogado"), com a ocupação da CBO quando conhecida.
/// Nunca é excluída: desativada, some das escolhas novas mas continua em quem já tem e no histórico.
/// O nome é único sem diferenciar maiúsculas nem acentos (collation CI_AI no banco).
/// </summary>
[DisplayName("Profissão")]
public class Profissao : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 80;
    public const int TamanhoMaximoDescricao = 150;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    [DisplayName("Ocupação CBO")]
    public int? OcupacaoCboId { get; set; }

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Profissão '{Nome}' desativada.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Profissão '{Nome}' reativada.");
    }
}
