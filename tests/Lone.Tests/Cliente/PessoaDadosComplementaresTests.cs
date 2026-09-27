using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Etiquetas;
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

    /// <summary>
    /// Fase 3: a ficha não envia consentimentos no Salvar (conceder/revogar são ações próprias). Substitui os testes
    /// antigos "Consentimento_so_vai_para_a_API_quando_existe_ou_foi_marcado" e
    /// "Retirar_autorizacao_gravada_manda_o_registro_desmarcado" (comportamento retirado de propósito).
    /// </summary>
    [Fact]
    public void Salvar_da_ficha_nao_leva_consentimento()
    {
        var f = PessoaFormulario.NovaPessoa();
        var json = System.Text.Json.JsonSerializer.Serialize(f.ParaDto());
        Assert.DoesNotContain("Consentimento", json);
        Assert.DoesNotContain(typeof(PessoaDto).GetProperties(), p => p.Name.Contains("Consentimento"));
    }

    [Fact]
    public void Etiquetas_marcadas_vao_pelo_id_e_origem_fora_da_lista_e_mantida()
    {
        var vip = new EtiquetaDto { Id = Guid.NewGuid(), Nome = "VIP", Ativo = true };
        var atacado = new EtiquetaDto { Id = Guid.NewGuid(), Nome = "Atacado", Ativo = true };
        var f = PessoaFormulario.De(
            new PessoaDto { Id = Guid.NewGuid(), Nome = "Ana", OrigemCadastro = "Rádio", EtiquetaIds = [vip.Id] },
            etiquetas: [vip, atacado]);
        Assert.Contains("Rádio", f.Origens);
        Assert.Equal("VIP", f.Etiquetas.Resumo);

        f.Etiquetas.AdicionarCommand.Execute(f.Etiquetas.Disponiveis.Single(e => e.Id == atacado.Id));
        var dto = f.ParaDto();

        Assert.Equal(new[] { vip.Id, atacado.Id }, dto.EtiquetaIds);
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
