using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Papel que uma pessoa pode ter no ERP (Cliente, Fornecedor, Representante...). Os oito papéis de sistema
/// (<see cref="PapelSistema"/> preenchido) têm regras no código e nascem com a base; os demais são criados pelo
/// usuário e servem para classificar e filtrar. Nunca é excluído: desativado, some das escolhas novas mas continua
/// em quem já tem e no histórico. Nome único sem diferenciar maiúsculas nem acentos; código único e imutável.
/// </summary>
[DisplayName("Papel")]
public class Papel : AgregadoRaiz
{
    public const int TamanhoMaximoCodigo = 30;
    public const int TamanhoMaximoNome = 60;
    public const int TamanhoMaximoDescricao = 150;

    /// <summary>Código estável (ex.: "CLIENTE"). Não muda depois de criado: integrações e relatórios podem usá-lo.</summary>
    [DisplayName("Código")]
    public string Codigo { get; set; } = string.Empty;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>Papel de sistema ligado ao enum usado pelas regras (nulo = papel criado pelo usuário).</summary>
    [DisplayName("Papel de sistema")]
    public TipoPapel? PapelSistema { get; set; }

    public void Desativar()
    {
        if (!Ativo) return;
        if (PapelSistema is { } sistema && global::Lone.Domain.Papeis.PapeisSistema.TemRegra(sistema))
            throw new Validacao.ValidacaoException([$"O papel \"{Nome}\" é usado pelas regras do sistema e não pode ser desativado."]);
        Ativo = false;
        RegistrarEvento($"Papel '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Papel '{Nome}' reativado.");
    }
}
