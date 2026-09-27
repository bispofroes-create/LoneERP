using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Resumo da pessoa (blocos, gravidade, ir para a aba, lado × cartão) e confirmação ao sair de PJ.</summary>
public class ResumoETrocaNaturezaTests
{
    private static PessoaFormulario NovaJuridica()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        return f;
    }

    [Fact]
    public void Resumo_mostra_pendencias_por_gravidade_e_leva_a_aba()
    {
        var f = NovaJuridica();
        f.Principal.IndicadorIE = Opcao.De(OpcoesPessoa.IndicadoresIE, IndicadorIE.Contribuinte);
        var resumo = new ResumoPessoa();
        SecaoPessoa? destino = null;

        resumo.Atualizar(f, aba => destino = aba);

        var cadastro = Assert.Single(resumo.Blocos, b => b.Titulo == "Cadastro");
        Assert.True(cadastro.Itens[0].EhAlerta); // alertas primeiro
        Assert.Equal("Contribuinte do ICMS sem inscrição estadual", cadastro.Itens[0].Texto);
        Assert.Contains(cadastro.Itens, i => i.Texto == "CNPJ não informado");
        Assert.StartsWith("Resumo: 1 alerta", resumo.TextoCabecalho);

        cadastro.Itens[0].IrCommand!.Execute(null);
        Assert.Equal(SecaoPessoa.Estabelecimentos, destino);
    }

    [Fact]
    public void Documento_vencido_entra_no_resumo()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Ana", DocumentoPrincipal = "52998224725",
            Documentos = [new DocumentoDto
            {
                Id = Guid.NewGuid(), TipoDocumentoId = Lone.Domain.Documentos.TiposDocumentoSistema.Id(TipoDocumento.Cnh),
                Numero = "123", ValidoAte = hoje.AddDays(-3)
            }]
        });
        var resumo = new ResumoPessoa();
        resumo.Atualizar(f, _ => { });

        var documentos = Assert.Single(resumo.Blocos, b => b.Titulo == "Documentos");
        var item = Assert.Single(documentos.Itens);
        Assert.True(item.EhAlerta);
        Assert.Equal("CNH 123: Vencido há 3 dias", item.Texto);
    }

    [Fact]
    public void Resumo_ao_lado_em_tela_larga_e_cartao_em_tela_estreita()
    {
        var resumo = new ResumoPessoa();
        resumo.Atualizar(PessoaFormulario.NovaPessoa(), _ => { });

        resumo.DefinirLargura(1500);
        Assert.True(resumo.MostrarPainelLateral);
        Assert.False(resumo.MostrarCartao);

        resumo.RecolherCommand.Execute(null);
        Assert.False(resumo.MostrarPainelLateral);
        Assert.True(resumo.MostrarCartao);
        Assert.Equal("Mostrar ao lado", resumo.TextoBotao);

        resumo.DefinirLargura(900);
        Assert.True(resumo.MostrarCartao);
        Assert.False(resumo.MostrarCorpoNoCartao);
        resumo.AlternarCommand.Execute(null);
        Assert.True(resumo.MostrarCorpoNoCartao);

        resumo.Atualizar(null, _ => { });
        Assert.False(resumo.MostrarCartao);
    }

    [Fact]
    public async Task Sair_de_pj_com_dados_da_empresa_pergunta_e_limpa_ou_mantem()
    {
        var f = NovaJuridica();
        f.AplicarCnpj(f.Principal, new DadosCnpj
        {
            Cnpj = "60746948000112", RazaoSocial = "BANCO BRADESCO S.A.", Uf = "SP", NaturezaJuridica = "2046",
            CnaePrincipal = "6422100", Porte = "Demais"
        });
        var resposta = false;
        var perguntas = 0;
        f.Confirmar = (_, _, _, _) => { perguntas++; return Task.FromResult(resposta); };

        f.NaturezaNaTela = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Fisica);
        await Task.Delay(50);
        Assert.Equal(1, perguntas);
        Assert.True(f.EhJuridica); // não confirmou: continua PJ, com tudo
        Assert.Equal("BANCO BRADESCO S.A.", f.Nome);

        resposta = true;
        f.NaturezaNaTela = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Fisica);
        await Task.Delay(50);
        Assert.True(f.EhFisica);
        Assert.Equal(string.Empty, f.Nome);
        Assert.Equal(string.Empty, f.Principal.Cnpj);
        Assert.Equal(string.Empty, f.Porte);
        Assert.Equal(string.Empty, f.Principal.NaturezaJuridica);
    }

    [Fact]
    public void Pj_sem_dados_da_empresa_troca_direto()
    {
        var f = NovaJuridica();
        var perguntas = 0;
        f.Confirmar = (_, _, _, _) => { perguntas++; return Task.FromResult(true); };
        f.NaturezaNaTela = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Fisica);
        Assert.True(f.EhFisica);
        Assert.Equal(0, perguntas);
    }
}
