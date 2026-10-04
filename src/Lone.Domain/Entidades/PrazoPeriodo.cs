using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>Unidade de um prazo pronto: dias corridos (contando o início), meses ou anos pelo calendário.</summary>
public enum UnidadePrazo
{
    Dias = 1,
    Meses = 2,
    Anos = 3
}

/// <summary>
/// Prazo pronto do campo Prazo (entre Início e Fim) em todas as telas com período: "30 dias", "6 meses", "1 ano"...
/// Cadastro do sistema inteiro (menu do usuário › Administração; pedido do usuário, 03/10/2026). Nunca é excluído:
/// desativado, some da lista de prazos prontos.
/// </summary>
[DisplayName("Prazo de período")]
public class PrazoPeriodo : AgregadoRaiz
{
    [DisplayName("Quantidade")]
    public int Quantidade { get; set; }

    [DisplayName("Unidade")]
    public UnidadePrazo Unidade { get; set; } = UnidadePrazo.Dias;

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>"30 dias", "1 mês", "6 meses", "1 ano", "2 anos".</summary>
    public string Nome => RegrasPrazoPeriodo.Nome(Quantidade, Unidade);

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Prazo de período '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Prazo de período '{Nome}' reativado.");
    }
}

/// <summary>Regras dos prazos de período: nome, limites, ordem e os prazos que a migração cria.</summary>
public static class RegrasPrazoPeriodo
{
    public const int MaximoDias = 3650;
    public const int MaximoMeses = 120;
    public const int MaximoAnos = 10;

    /// <summary>Os prazos de antes do cadastro (7, 15, 30, 60, 90, 180 dias e 1 ano), criados pela migração.</summary>
    public static IReadOnlyList<(Guid Id, int Quantidade, UnidadePrazo Unidade)> Iniciais { get; } =
    [
        (new Guid("5a2e0d10-0000-0000-0000-000000000001"), 7, UnidadePrazo.Dias),
        (new Guid("5a2e0d10-0000-0000-0000-000000000002"), 15, UnidadePrazo.Dias),
        (new Guid("5a2e0d10-0000-0000-0000-000000000003"), 30, UnidadePrazo.Dias),
        (new Guid("5a2e0d10-0000-0000-0000-000000000004"), 60, UnidadePrazo.Dias),
        (new Guid("5a2e0d10-0000-0000-0000-000000000005"), 90, UnidadePrazo.Dias),
        (new Guid("5a2e0d10-0000-0000-0000-000000000006"), 180, UnidadePrazo.Dias),
        (new Guid("5a2e0d10-0000-0000-0000-000000000007"), 1, UnidadePrazo.Anos)
    ];

    public static string Nome(int quantidade, UnidadePrazo unidade) => unidade switch
    {
        UnidadePrazo.Meses => quantidade == 1 ? "1 mês" : $"{quantidade} meses",
        UnidadePrazo.Anos => quantidade == 1 ? "1 ano" : $"{quantidade} anos",
        _ => quantidade == 1 ? "1 dia" : $"{quantidade} dias"
    };

    /// <summary>Ordem na lista: pelo tamanho aproximado (30 dias antes de 2 meses; 12 meses junto de 1 ano).</summary>
    public static int DiasAproximados(int quantidade, UnidadePrazo unidade) => unidade switch
    {
        UnidadePrazo.Meses => quantidade * 30,
        UnidadePrazo.Anos => quantidade * 365,
        _ => quantidade
    };

    public static int Maximo(UnidadePrazo unidade) => unidade switch
    {
        UnidadePrazo.Meses => MaximoMeses,
        UnidadePrazo.Anos => MaximoAnos,
        _ => MaximoDias
    };

    /// <summary>Unidade válida, quantidade de 1 até o máximo da unidade e sem repetir (ativo ou desativado).</summary>
    public static List<string> Validar(PrazoPeriodo dados, IEnumerable<PrazoPeriodo> todos)
    {
        var erros = new List<string>();
        if (!Enum.IsDefined(dados.Unidade))
        {
            erros.Add("Escolha a unidade: dias, meses ou anos.");
            return erros;
        }
        var maximo = Maximo(dados.Unidade);
        if (dados.Quantidade < 1 || dados.Quantidade > maximo)
            erros.Add($"Quantidade: de 1 a {maximo} {(dados.Unidade == UnidadePrazo.Dias ? "dias" : dados.Unidade == UnidadePrazo.Meses ? "meses" : "anos")}.");
        else if (todos.Any(t => t.Id != dados.Id && t.Quantidade == dados.Quantidade && t.Unidade == dados.Unidade))
            erros.Add($"Já existe o prazo \"{dados.Nome}\" (ativo ou desativado).");
        return erros;
    }
}
