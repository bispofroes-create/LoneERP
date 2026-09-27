using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Aba Fiscal por natureza: a PF vê só o que usa; o estrangeiro, só o resumo; dados de empresa antigos não somem.</summary>
public class FiscalPessoaFisicaFormularioTests
{
    private static PessoaDto PessoaFisicaGravada(EstabelecimentoDto estabelecimento) => new()
    {
        Id = Guid.NewGuid(),
        Codigo = 7,
        Natureza = NaturezaPessoa.Fisica,
        Nome = "João da Silva",
        Estabelecimentos = [estabelecimento]
    };

    [Fact]
    public void Pessoa_fisica_nova_ve_so_o_resumo_e_produtor_rural_mostra_a_ie()
    {
        var f = PessoaFormulario.NovaPessoa();
        var e = f.Principal;

        Assert.True(e.DaPessoaFisica);
        Assert.True(e.MostrarResumoFiscal);
        Assert.False(e.MostrarDadosIE);
        Assert.False(e.MostrarInscricaoMunicipal);
        Assert.True(e.PodeAdicionarInscricaoMunicipal);
        Assert.DoesNotContain(e.IndicadoresIE, o => o.Valor == IndicadorIE.NaoInformado);

        e.ProdutorRural = true;
        Assert.True(e.MostrarDadosIE);
        Assert.Equal(IndicadorIE.Contribuinte, e.IndicadorIE.Valor);

        e.InscricaoEstadual = "0012345670001";
        e.ProdutorRural = false;
        Assert.True(e.MostrarResumoFiscal);
        Assert.Equal(IndicadorIE.NaoContribuinte, e.IndicadorIE.Valor);
        Assert.Equal("0012345670001", e.InscricaoEstadual); // não apaga o que foi digitado
    }

    [Fact]
    public void Inscricao_municipal_aparece_quando_pedida_ou_gravada()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Principal.AdicionarInscricaoMunicipalCommand.Execute(null);
        Assert.True(f.Principal.MostrarInscricaoMunicipal);
        Assert.False(f.Principal.PodeAdicionarInscricaoMunicipal);

        var gravada = PessoaFormulario.De(PessoaFisicaGravada(new EstabelecimentoDto
        {
            Id = Guid.NewGuid(), Principal = true, IndicadorIE = IndicadorIE.NaoContribuinte, InscricaoMunicipal = "12345"
        }));
        Assert.True(gravada.Principal.MostrarInscricaoMunicipal);
    }

    [Fact]
    public void Carregar_pessoa_fisica_nao_muda_os_dados_gravados()
    {
        // Produtor rural gravado com "não informado": carregar não troca o indicador sozinho.
        var f = PessoaFormulario.De(PessoaFisicaGravada(new EstabelecimentoDto
        {
            Id = Guid.NewGuid(), Principal = true, ProdutorRural = true, IndicadorIE = IndicadorIE.NaoInformado
        }));
        Assert.Equal(IndicadorIE.NaoInformado, f.Principal.IndicadorIE.Valor);
        Assert.True(f.Principal.MostrarDadosIE);

        // IE gravada sem produtor rural: continua à vista.
        var comIe = PessoaFormulario.De(PessoaFisicaGravada(new EstabelecimentoDto
        {
            Id = Guid.NewGuid(), Principal = true, IndicadorIE = IndicadorIE.Contribuinte, InscricaoEstadual = "0012345670001"
        }));
        Assert.True(comIe.Principal.MostrarDadosIE);
        Assert.Equal(IndicadorIE.Contribuinte, comIe.Principal.IndicadorIE.Valor);
    }

    [Fact]
    public void Dados_de_empresa_gravados_na_pessoa_fisica_aparecem_ate_serem_removidos()
    {
        var f = PessoaFormulario.De(PessoaFisicaGravada(new EstabelecimentoDto
        {
            Id = Guid.NewGuid(), Principal = true, IndicadorIE = IndicadorIE.NaoContribuinte,
            RegimeTributario = RegimeTributario.SimplesNacional, CnaePrincipal = "4711302", InscricaoSuframa = "123456789"
        }));
        var e = f.Principal;
        Assert.True(e.TemDadosDeEmpresa);
        Assert.Equal(3, e.DadosDeEmpresa.Count);
        Assert.Equal(RegimeTributario.SimplesNacional, f.ParaDto().Estabelecimentos[0].RegimeTributario); // mantido se não remover

        e.RemoverDadosDeEmpresaCommand.Execute(null);
        Assert.False(e.TemDadosDeEmpresa);
        var dto = f.ParaDto().Estabelecimentos[0];
        Assert.Equal(RegimeTributario.NaoInformado, dto.RegimeTributario);
        Assert.Null(dto.CnaePrincipal);
        Assert.Null(dto.InscricaoSuframa);
    }

    [Fact]
    public void Estrangeiro_ve_so_o_resumo_e_pj_ve_tudo()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Estrangeiro);
        Assert.True(f.Principal.MostrarResumoFiscal);
        Assert.Contains("exterior", f.Principal.ResumoFiscal);
        Assert.False(f.Principal.PodeAdicionarInscricaoMunicipal);

        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        Assert.True(f.Principal.MostrarDadosIE);
        Assert.True(f.Principal.MostrarInscricaoMunicipal);
        Assert.False(f.Principal.MostrarResumoFiscal);
        Assert.False(f.Principal.TemDadosDeEmpresa);
        Assert.Contains(f.Principal.IndicadoresIE, o => o.Valor == IndicadorIE.NaoInformado);
    }
}
