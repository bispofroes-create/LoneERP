using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Idade calculada, CPF/CNPJ conferidos no campo e vários endereços (revisão do cadastro de pessoas).</summary>
public class IdentificacaoFormularioTests
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

    [Fact]
    public void Data_invalida_mostra_erro_junto_do_campo_e_bloqueia_o_envio()
    {
        var f = Pf(new DateOnly(2026, 9, 25));
        f.Nome = "Ana";
        f.DataNascimento = "31/02/1990";

        Assert.Equal("Data inválida (dd/mm/aaaa).", f.ErroDataNascimento);
        Assert.Contains(f.ValidarLocalmente(), e => e.Contains("nascimento"));
    }

    // ---------------------------------------------------------------- CPF / CNPJ

    [Fact]
    public void CPF_invalido_completo_acusa_na_hora_e_impede_o_envio()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Nome = "Ana";
        f.Documento = "529.982.247-24";

        Assert.Equal("CPF inválido: confira os dígitos.", f.ErroDocumento);
        Assert.False(f.DocumentoValido);
        Assert.Contains("CPF inválido: confira os dígitos.", f.ValidarLocalmente());
    }

    [Fact]
    public void CPF_incompleto_so_acusa_ao_sair_do_campo()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Documento = "529.982";
        Assert.Equal(string.Empty, f.ErroDocumento); // ainda digitando

        f.ConferirDocumentoCommand.Execute(null);      // foco saiu

        Assert.Equal("CPF incompleto: são 11 dígitos.", f.ErroDocumento);
    }

    [Fact]
    public void CPF_valido_fica_marcado_como_valido_e_sem_erro()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Documento = "529.982.247-25";
        f.ConferirDocumentoCommand.Execute(null);

        Assert.Equal(string.Empty, f.ErroDocumento);
        Assert.True(f.DocumentoValido);
        Assert.DoesNotContain(f.ValidarLocalmente(), e => e.Contains("CPF"));
    }

    [Fact]
    public void CNPJ_invalido_acusa_no_estabelecimento_e_valido_passa()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        f.Nome = "Empresa";
        f.Principal.Cnpj = "11.222.333/0001-80";

        Assert.Equal("CNPJ inválido: confira os dígitos.", f.Principal.ErroCnpj);
        Assert.Contains(f.ValidarLocalmente(), e => e.Contains("CNPJ inválido"));

        f.Principal.Cnpj = "11.222.333/0001-81";
        Assert.Equal(string.Empty, f.Principal.ErroCnpj);
        Assert.True(f.Principal.CnpjValido);
    }

    [Fact]
    public void Pessoa_fisica_nao_confere_CNPJ_e_juridica_nao_confere_CPF()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Principal.Cnpj = "123";
        Assert.Equal(string.Empty, f.Principal.ErroCnpj);

        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        f.Documento = "111";
        Assert.Equal(string.Empty, f.ErroDocumento);
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

    [Fact]
    public void Seletor_de_endereco_fiscal_usa_opcoes_estaveis_e_acompanha_os_enderecos()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        var filial = f.AdicionarEstabelecimento();
        var antes = filial.OpcoesEnderecoFiscal;

        var novo = new EnderecoFormulario { Logradouro = "Rua do Depósito", Principal = false };
        f.AdicionarEndereco(novo);
        f.AdicionarEndereco(new EnderecoFormulario { Logradouro = "Rua 3", Principal = false });

        Assert.NotSame(antes, filial.OpcoesEnderecoFiscal);       // array novo, nunca a coleção viva
        Assert.Equal(4, filial.OpcoesEnderecoFiscal.Length);      // "principal da pessoa" + 3 endereços
        filial.EnderecoFiscalOpcao = filial.OpcoesEnderecoFiscal.First(o => o.Valor == novo.Id);
        Assert.Same(novo, filial.EnderecoFiscal);
        Assert.Equal(novo.Id, f.ParaDto().Estabelecimentos[1].EnderecoFiscalId);
    }
}
