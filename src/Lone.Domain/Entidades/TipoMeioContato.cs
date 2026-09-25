using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Classificação de telefone ou e-mail (ex.: "Comercial", "Residencial", "Pessoal"). A categoria diz onde ele é
/// oferecido (telefones ou e-mails) e não muda. Nunca é excluído: desativado, some das escolhas novas mas continua
/// nos contatos que já o têm. Nome único por categoria, sem diferenciar maiúsculas nem acentos.
/// </summary>
[DisplayName("Tipo de telefone/e-mail")]
public class TipoMeioContato : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 40;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Categoria")]
    public CategoriaMeioContato Categoria { get; set; }

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Tipo de {(Categoria == CategoriaMeioContato.Email ? "e-mail" : "telefone")} '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Tipo de {(Categoria == CategoriaMeioContato.Email ? "e-mail" : "telefone")} '{Nome}' reativado.");
    }
}
