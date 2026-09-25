using Lone.Cliente.ViewModels.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>R1 da revisão de Pessoas: idade sempre calculada da data de nascimento (endereços: EnderecoFinalidadesTests).</summary>
public class RevisaoPessoasR1Tests
{
    private static PessoaFormulario Pf(DateOnly hoje)
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Hoje = hoje;
        return f;
    }

    // ---------------------------------------------------------------- Idade

    [Theory]
    [InlineData("13/05/1983", 2026, 9, 25, "43 anos")]   // aniversário já passou no ano
    [InlineData("13/10/1983", 2026, 9, 25, "42 anos")]   // aniversário ainda não chegou
    [InlineData("25/09/1983", 2026, 9, 25, "43 anos")]   // aniversário hoje
    [InlineData("20/09/2026", 2026, 9, 25, "Menos de 1 ano")] // recém-nascido
    [InlineData("25/09/2025", 2026, 9, 25, "1 ano")]
    [InlineData("26/09/2026", 2026, 9, 25, "Data no futuro")]
    public void Idade_calculada_na_data_de_hoje(string nascimento, int ano, int mes, int dia, string esperado)
    {
        var f = Pf(new DateOnly(ano, mes, dia));
        f.DataNascimento = nascimento;
        Assert.Equal(esperado, f.Idade);
    }

    [Theory]
    [InlineData("")]
    [InlineData("31/02/1990")] // data que não existe
    [InlineData("13/05/19")]   // ainda digitando: não mostra idade de 2019
    [InlineData("abc")]
    public void Sem_data_completa_e_valida_a_idade_fica_vazia(string nascimento)
    {
        var f = Pf(new DateOnly(2026, 9, 25));
        f.DataNascimento = nascimento;
        Assert.Equal(string.Empty, f.Idade);
    }

    [Fact]
    public void Mudar_e_apagar_a_data_atualiza_a_idade_na_hora()
    {
        var f = Pf(new DateOnly(2026, 9, 25));
        var avisos = new List<string?>();
        f.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        f.DataNascimento = "13/05/1983";
        Assert.Equal("43 anos", f.Idade);
        f.DataNascimento = "13/05/2000";
        Assert.Equal("26 anos", f.Idade);
        f.DataNascimento = string.Empty;

        Assert.Equal(string.Empty, f.Idade);
        Assert.Equal(3, avisos.Count(p => p == nameof(PessoaFormulario.Idade))); // a tela é avisada a cada mudança
    }
}
