using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;

namespace Lone.Application.Pessoas;

/// <summary>Distribui pessoas por faixa de idade (as faixas mais usadas em relatórios de clientes).</summary>
public static class FaixasEtarias
{
    public const string SemData = "Sem data de nascimento";

    private static readonly (int Ate, string Nome)[] Faixas =
    [
        (17, "Até 17 anos"),
        (24, "18 a 24 anos"),
        (34, "25 a 34 anos"),
        (44, "35 a 44 anos"),
        (59, "45 a 59 anos"),
        (int.MaxValue, "60 anos ou mais")
    ];

    /// <summary>Todas as faixas, inclusive as vazias, na ordem; por último, quem não informou a data.</summary>
    public static List<QuantidadePorFaixaEtaria> Contar(IEnumerable<DateOnly?> nascimentos, DateOnly hoje)
    {
        var contagem = Faixas.ToDictionary(f => f.Nome, _ => 0);
        var semData = 0;

        foreach (var nascimento in nascimentos)
        {
            if (nascimento is not { } data || data > hoje)
            {
                semData++;
                continue;
            }
            var idade = Idade.Em(data, hoje);
            contagem[Faixas.First(f => idade <= f.Ate).Nome]++;
        }

        var resultado = Faixas.Select(f => new QuantidadePorFaixaEtaria(f.Nome, contagem[f.Nome])).ToList();
        resultado.Add(new QuantidadePorFaixaEtaria(SemData, semData));
        return resultado;
    }
}
