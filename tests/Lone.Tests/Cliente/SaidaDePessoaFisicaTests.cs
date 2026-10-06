using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;

namespace Lone.Tests.Cliente;

/// <summary>
/// Bloco G (P1-2) na ficha: trocar uma pessoa física para pessoa jurídica ou estrangeiro com dados que só existem nela
/// pergunta antes; confirmada, a gravação leva a confirmação específica (de/para/campos); recusada, nada muda.
/// </summary>
public class SaidaDePessoaFisicaTests
{
    private static PessoaFormulario FisicaGravada(Action<PessoaDto>? ajustar = null)
    {
        var dto = new PessoaDto
        {
            Id = Guid.NewGuid(), Natureza = NaturezaPessoa.Fisica, Nome = "Ana", DocumentoPrincipal = "52998224725", NomeSocial = "Aninha",
            Estabelecimentos = [new EstabelecimentoDto { Id = Guid.NewGuid(), Principal = true }]
        };
        ajustar?.Invoke(dto);
        return PessoaFormulario.De(dto);
    }

    private static Opcao<NaturezaPessoa> Natureza(NaturezaPessoa n) => Opcao.De(OpcoesPessoa.Naturezas, n);

    [Fact]
    public async Task Recusar_mantem_pessoa_fisica_e_nao_manda_confirmacao()
    {
        var f = FisicaGravada();
        string? mensagem = null;
        f.Confirmar = (_, texto, _, _) => { mensagem = texto; return Task.FromResult(false); };

        f.NaturezaNaTela = Natureza(NaturezaPessoa.Juridica);
        await Task.Delay(50);

        Assert.True(f.EhFisica);
        Assert.Contains("o CPF; o nome social", mensagem);
        Assert.Contains("ficam no histórico", mensagem); // cadastro gravado
        Assert.Null(f.ParaDto().ConfirmacaoTrocaNatureza);
    }

    [Fact]
    public async Task Confirmar_troca_e_a_gravacao_leva_a_confirmacao_especifica()
    {
        var f = FisicaGravada();
        f.Confirmar = (_, _, _, _) => Task.FromResult(true);

        f.NaturezaNaTela = Natureza(NaturezaPessoa.Juridica);
        await Task.Delay(50);

        Assert.True(f.EhJuridica);
        var confirmacao = f.ParaDto().ConfirmacaoTrocaNatureza!;
        Assert.Equal((NaturezaPessoa.Fisica, NaturezaPessoa.Juridica), (confirmacao.De, confirmacao.Para));
        Assert.Equal(new[] { CamposFichaPessoa.Documento, CamposFichaPessoa.NomeSocial }, confirmacao.Campos);
    }

    [Fact]
    public void Pessoa_fisica_sem_dados_a_perder_troca_direto()
    {
        var f = FisicaGravada(d => { d.DocumentoPrincipal = null; d.NomeSocial = null; });
        var perguntas = 0;
        f.Confirmar = (_, _, _, _) => { perguntas++; return Task.FromResult(true); };

        f.NaturezaNaTela = Natureza(NaturezaPessoa.Juridica);

        Assert.True(f.EhJuridica);
        Assert.Equal(0, perguntas);
        Assert.Null(f.ParaDto().ConfirmacaoTrocaNatureza);
    }

    [Fact]
    public async Task Ir_para_estrangeiro_depois_de_confirmar_juridica_pergunta_de_novo_pela_inscricao_estadual()
    {
        var f = FisicaGravada(d => d.Estabelecimentos[0].InscricaoEstadual = "110042490114");
        var perguntas = 0;
        f.Confirmar = (_, _, _, _) => { perguntas++; return Task.FromResult(true); };

        f.NaturezaNaTela = Natureza(NaturezaPessoa.Juridica);
        await Task.Delay(50);
        f.NaturezaNaTela = Natureza(NaturezaPessoa.Estrangeiro);
        await Task.Delay(50);

        Assert.Equal(2, perguntas);
        var confirmacao = f.ParaDto().ConfirmacaoTrocaNatureza!;
        Assert.Equal(NaturezaPessoa.Estrangeiro, confirmacao.Para);
        Assert.Contains(CamposFichaPessoa.InscricaoEstadual, confirmacao.Campos);
    }

    [Fact]
    public async Task Voltar_para_pessoa_fisica_nao_manda_confirmacao()
    {
        var f = FisicaGravada();
        f.Confirmar = (_, _, _, _) => Task.FromResult(true);
        f.NaturezaNaTela = Natureza(NaturezaPessoa.Juridica);
        await Task.Delay(50);

        f.NaturezaNaTela = Natureza(NaturezaPessoa.Fisica);

        Assert.True(f.EhFisica);
        Assert.Null(f.ParaDto().ConfirmacaoTrocaNatureza);
    }

    [Fact]
    public async Task Ficha_nova_com_dados_digitados_tambem_avisa()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.NomeSocial = "Aninha";
        string? mensagem = null;
        f.Confirmar = (_, texto, _, _) => { mensagem = texto; return Task.FromResult(true); };

        f.NaturezaNaTela = Natureza(NaturezaPessoa.Estrangeiro);
        await Task.Delay(50);

        Assert.True(f.EhEstrangeiro);
        Assert.Contains("o nome social", mensagem);
        Assert.DoesNotContain("histórico", mensagem); // nada gravado ainda
    }
}
