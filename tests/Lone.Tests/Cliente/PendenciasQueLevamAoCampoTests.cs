using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Domain.Pessoas;

namespace Lone.Tests.Cliente;

/// <summary>
/// Lone Contextual, Fase 2 (parte 1): o "Resolver" do resumo da pessoa. Cada pendência leva ao campo (ou ao "Adicionar")
/// onde se resolve, pelo mesmo caminho do erro da Fase 1, sem marcar nada como erro.
/// </summary>
public class PendenciasQueLevamAoCampoTests
{
    private static ItemResumo Item(ResumoPessoa resumo, string texto) =>
        resumo.Blocos.SelectMany(b => b.Itens).Single(i => i.Texto == texto);

    [Fact]
    public void Cada_pendencia_do_resumo_diz_o_campo_onde_se_resolve()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Enderecos.Clear();
        var destinos = new List<DestinoFicha>();
        var resumo = new ResumoPessoa();

        resumo.Atualizar(f, destinos.Add);
        Item(resumo, "CPF não informado").IrCommand!.Execute(null);
        Item(resumo, "Nenhum endereço").IrCommand!.Execute(null);
        Item(resumo, "Nenhum telefone ou e-mail").IrCommand!.Execute(null);

        Assert.Equal(new DestinoFicha(SecaoPessoa.Geral, CamposFichaPessoa.Documento), destinos[0]);
        Assert.Equal(new DestinoFicha(SecaoPessoa.Enderecos, CamposFichaPessoa.AdicionarEndereco), destinos[1]); // o "Adicionar", não um cartão aberto
        Assert.Equal(new DestinoFicha(SecaoPessoa.Contatos, CamposFichaPessoa.AdicionarTelefone), destinos[2]);
    }

    [Fact]
    public void Destino_de_pendencia_vai_para_a_aba_certa_pelo_mesmo_mapa_do_erro()
    {
        Assert.Equal(SecaoPessoa.Enderecos, AbaDoCampo.De(CamposFichaPessoa.AdicionarEndereco));
        Assert.Equal(SecaoPessoa.Contatos, AbaDoCampo.De(CamposFichaPessoa.AdicionarTelefone));
        Assert.Equal(SecaoPessoa.Estabelecimentos, AbaDoCampo.De(CamposFichaPessoa.Regime, Guid.NewGuid()));
    }

    [Fact]
    public async Task Na_ficha_resolver_troca_a_aba_e_pede_o_foco_sem_marcar_erro()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        await tela.NovoCommand.ExecuteAsync(null);
        var focos = new List<DestinoCampo>();
        tela.Validacao.FocoPedido += focos.Add;
        tela.SecaoSelecionada = tela.Secoes.First(s => s.Secao == SecaoPessoa.Enderecos);

        Item(tela.Resumo, "CPF não informado").IrCommand!.Execute(null);

        Assert.Equal(SecaoPessoa.Geral, tela.SecaoSelecionada!.Secao);
        Assert.Equal(new DestinoCampo(CamposFichaPessoa.Documento), Assert.Single(focos));
        Assert.False(tela.Validacao.Visivel); // pendência não é erro: nada no resumo de erros
        Assert.Null(tela.Validacao.ErroDe(CamposFichaPessoa.Documento, null));

        Item(tela.Resumo, "Nenhum telefone ou e-mail").IrCommand!.Execute(null);
        Assert.Equal(SecaoPessoa.Contatos, tela.SecaoSelecionada!.Secao);
        Assert.Equal(CamposFichaPessoa.AdicionarTelefone, focos[^1].Campo);
    }

    [Fact]
    public void Levar_sem_aba_para_o_campo_nao_pede_foco()
    {
        var resumo = new ResumoValidacao { AntesDeIr = _ => false };
        var focos = 0;
        resumo.FocoPedido += _ => focos++;

        Assert.False(resumo.Levar(new DestinoCampo(CamposFichaPessoa.Documento)));
        Assert.Equal(0, focos);
    }
}
