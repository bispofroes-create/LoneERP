using Lone.Cliente.ViewModels.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>R1 da revisão de Pessoas: idade sempre calculada da data e os cenários de vários endereços no formulário.</summary>
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

    // ---------------------------------------------------------------- Vários endereços

    [Fact]
    public void Um_dois_e_tres_enderecos_com_finalidades_diferentes()
    {
        var f = PessoaFormulario.NovaPessoa();
        var primeiro = Assert.Single(f.Enderecos);
        primeiro.Logradouro = "Rua A";
        var segundo = new EnderecoFormulario { Logradouro = "Rua B", Cobranca = true, Principal = false };
        var terceiro = new EnderecoFormulario { Logradouro = "Rua C", Entrega = true, Principal = false };
        f.AdicionarEndereco(segundo);
        f.AdicionarEndereco(terceiro);

        var dto = f.ParaDto();

        Assert.Equal(3, dto.Enderecos.Count);
        Assert.True(dto.Enderecos[0].Finalidades.HasFlag(FinalidadeEndereco.Principal));
        Assert.True(dto.Enderecos[1].Finalidades.HasFlag(FinalidadeEndereco.Cobranca));
        Assert.False(dto.Enderecos[1].Finalidades.HasFlag(FinalidadeEndereco.Principal));
        Assert.True(dto.Enderecos[2].Finalidades.HasFlag(FinalidadeEndereco.Entrega));
    }

    [Fact]
    public void Editar_e_remover_o_segundo_endereco_e_trocar_o_principal()
    {
        var f = PessoaFormulario.NovaPessoa();
        var primeiro = f.Enderecos[0];
        var segundo = new EnderecoFormulario { Logradouro = "Rua B" };
        var terceiro = new EnderecoFormulario { Logradouro = "Rua C" };
        f.AdicionarEndereco(segundo);
        f.AdicionarEndereco(terceiro);

        segundo.Numero = "100";                 // editar o 2º
        terceiro.Principal = true;              // trocar o principal
        Assert.False(primeiro.Principal);

        segundo.RemoverCommand.Execute(null);   // remover o 2º (novo: sai da lista)

        Assert.Equal(new[] { primeiro, terceiro }, f.Enderecos);
        Assert.True(terceiro.Principal);
        Assert.Single(f.ParaDto().Enderecos, e => e.Finalidades.HasFlag(FinalidadeEndereco.Principal));
    }
}
