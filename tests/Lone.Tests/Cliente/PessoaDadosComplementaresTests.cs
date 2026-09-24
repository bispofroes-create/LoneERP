using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class PessoaDadosComplementaresTests
{
    private static PessoaFormulario Juridica()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        return f;
    }

    private static DadosCnpj Consulta(params InscricaoEstadualEncontrada[] inscricoes) => new()
    {
        Cnpj = "11222333000181", RazaoSocial = "Empresa Ltda", Uf = "SP", Fonte = "BrasilAPI",
        DataAbertura = new DateOnly(2015, 6, 1), Porte = "Micro Empresa", CapitalSocial = 50000m,
        OpcaoSimples = true, OpcaoMei = false, CnaePrincipal = "4711302", NaturezaJuridica = "2062",
        CnaesSecundarios = ["5611201", "4721102"],
        Socios = [new SocioDto { Nome = "Ana Souza", Qualificacao = "Sócio-Administrador" }],
        InscricoesEstaduais = [.. inscricoes]
    };

    [Fact]
    public void Consulta_de_CNPJ_preenche_dados_da_empresa_regime_CNAEs_e_socios()
    {
        var f = Juridica();

        f.AplicarCnpj(f.Principal, Consulta());

        Assert.Equal("01/06/2015", f.DataAbertura);
        Assert.Equal("Micro Empresa", f.Porte);
        Assert.Equal("50.000", f.CapitalSocial);
        Assert.Equal(RegimeTributario.SimplesNacional, f.Principal.Regime.Valor);
        Assert.Equal("4711302", f.Principal.CnaePrincipal);
        Assert.Equal("5611201, 4721102", f.Principal.CnaesSecundarios);
        Assert.Equal("Ana Souza", Assert.Single(f.Socios).Nome);
        Assert.True(f.TemSocios);
        Assert.False(f.Principal.InscricaoVeioDaConsulta);
    }

    [Fact]
    public void Inscricao_estadual_ativa_da_UF_do_CNPJ_e_usada_e_marca_contribuinte()
    {
        var f = Juridica();

        f.AplicarCnpj(f.Principal, Consulta(
            new InscricaoEstadualEncontrada("RJ", "86925860", true),
            new InscricaoEstadualEncontrada("SP", "111111111111", false),
            new InscricaoEstadualEncontrada("SP", "110042490114", true)));

        Assert.Equal("110042490114", f.Principal.InscricaoEstadual);
        Assert.Equal(IndicadorIE.Contribuinte, f.Principal.IndicadorIE.Valor);
        Assert.True(f.Principal.InscricaoVeioDaConsulta);
    }

    [Fact]
    public void MEI_tem_preferencia_sobre_Simples()
    {
        var f = Juridica();
        var dados = Consulta();
        dados.OpcaoMei = true;

        f.AplicarCnpj(f.Principal, dados);

        Assert.Equal(RegimeTributario.Mei, f.Principal.Regime.Valor);
    }

    [Fact]
    public void Consentimento_so_vai_para_a_API_quando_existe_ou_foi_marcado()
    {
        var f = PessoaFormulario.NovaPessoa();
        var email = f.Consentimentos.First(c => c.Canal == CanalComunicacao.Email);
        email.Concedido = true;
        email.Origem = "Balcão";

        var dto = f.ParaDto();

        var enviado = Assert.Single(dto.Consentimentos);
        Assert.Equal(CanalComunicacao.Email, enviado.Canal);
        Assert.True(enviado.Concedido);
        Assert.Null(enviado.ConcedidoEm); // a data é dada pela API
        Assert.Equal("Será registrado ao salvar", email.Situacao);
    }

    [Fact]
    public void Retirar_autorizacao_gravada_manda_o_registro_desmarcado()
    {
        var gravado = new ConsentimentoDto
        {
            Id = Guid.NewGuid(), Canal = CanalComunicacao.WhatsApp, Concedido = true,
            ConcedidoEm = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc)
        };
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Ana", Consentimentos = [gravado] });
        var whatsapp = f.Consentimentos.First(c => c.Canal == CanalComunicacao.WhatsApp);
        Assert.StartsWith("Autorizado em", whatsapp.Situacao);

        whatsapp.Concedido = false;
        var enviado = Assert.Single(f.ParaDto().Consentimentos);

        Assert.False(enviado.Concedido);
        Assert.Equal(gravado.ConcedidoEm, enviado.ConcedidoEm);
    }

    [Fact]
    public void Etiquetas_e_origem_vao_limpas_e_origem_fora_da_lista_e_mantida()
    {
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Ana", OrigemCadastro = "Rádio", Etiquetas = ["VIP"] });
        Assert.Contains("Rádio", f.Origens);
        Assert.Equal("VIP", f.Etiquetas);

        f.Etiquetas = " VIP , Atacado ,, ";
        var dto = f.ParaDto();

        Assert.Equal(new[] { "VIP", "Atacado" }, dto.Etiquetas);
        Assert.Equal("Rádio", dto.OrigemCadastro);

        f.OrigemCadastro = OpcoesPessoa.Origens[0]; // "Não informada"
        Assert.Null(f.ParaDto().OrigemCadastro);
    }

    [Fact]
    public void Cor_raca_so_aparece_para_funcionario_e_com_permissao()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.PodeVerDadosSensiveis = true;
        Assert.False(f.MostrarCorRaca);

        f.Papeis.First(p => p.Papel == TipoPapel.Funcionario).Ativo = true;
        Assert.True(f.MostrarCorRaca);

        f.PodeVerDadosSensiveis = false;
        Assert.False(f.MostrarCorRaca);
    }
}
