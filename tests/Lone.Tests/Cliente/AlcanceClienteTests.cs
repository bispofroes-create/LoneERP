using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>
/// Fase 2a-2 no aplicativo: a faixa que diz o que a lista mostra, Pessoas fora do menu com alcance "Nenhum" e o documento
/// em uso num cadastro fora do alcance (E5).
/// </summary>
public class AlcanceClienteTests
{
    [Fact]
    public void Faixa_da_lista_diz_o_alcance_e_some_quando_ve_tudo()
    {
        Assert.Equal(string.Empty, PessoasViewModel.AvisoDeAlcance(AlcanceComercial.Tudo, null));
        Assert.Contains("sua carteira", PessoasViewModel.AvisoDeAlcance(AlcanceComercial.MinhaCarteira, Guid.NewGuid()));
        Assert.Contains("sua equipe", PessoasViewModel.AvisoDeAlcance(AlcanceComercial.MinhaEquipe, Guid.NewGuid()));
        Assert.Contains("não está ligado", PessoasViewModel.AvisoDeAlcance(AlcanceComercial.MinhaCarteira, null));
    }

    [Fact]
    public async Task Alcance_nenhum_tira_pessoas_do_menu_mas_nao_as_outras_permissoes()
    {
        var ambiente = new AmbienteCliente();
        var sessao = AmbienteCliente.NovaSessao();
        sessao.Administrador = false;
        sessao.Permissoes = [Permissoes.Pessoas.Visualizar, Permissoes.Comercial.Visualizar];
        sessao.Alcance = AlcanceComercial.Nenhum;
        await ambiente.Sessao.DefinirAsync(sessao);

        Assert.False(ambiente.Sessao.Possui(Permissoes.Pessoas.Visualizar));
        Assert.True(ambiente.Sessao.Possui(Permissoes.Comercial.Visualizar));

        sessao.Alcance = AlcanceComercial.MinhaCarteira;
        Assert.True(ambiente.Sessao.Possui(Permissoes.Pessoas.Visualizar));
    }

    [Fact]
    public void Documento_em_uso_fora_do_alcance_avisa_sem_nome_e_sem_abrir()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        f.Principal.Cnpj = "11.222.333/0001-81";
        f.DefinirDocumentoEmUso(new DocumentoEmUsoResposta { EmUso = true, ForaDoAlcance = true });

        Assert.True(f.TemAvisoDocumentoEmUso);
        Assert.Contains("fora do seu alcance", f.AvisoDocumentoEmUso);
        Assert.Null(f.DocumentoEmUsoId);
        Assert.False(f.PodeAbrirDocumentoEmUso);

        f.DefinirDocumentoEmUso(new DocumentoEmUsoResposta { EmUso = true, Id = Guid.NewGuid(), Codigo = 12, Nome = "ABC Ltda" });
        Assert.True(f.PodeAbrirDocumentoEmUso);
    }

    [Fact]
    public void Escolher_o_tipo_ou_a_pessoa_do_relacionamento_avisa_para_tirar_o_aviso_antigo()
    {
        // Fase 2a-3: o aviso "Escolha o tipo de relacionamento..." some quando o tipo é escolhido (não fica até a próxima ação).
        var r = new RelacionamentosFormulario();
        var avisos = 0;
        r.Acoes.AoMudarEscolha = () => avisos++;
        r.DefinirTipos([new TipoRelacionamentoDto { Id = Guid.NewGuid(), Nome = "Contato de", NomeInverso = "Tem como contato" }]);
        avisos = 0;

        r.NovoTipo = r.Tipos[1];
        Assert.Equal(1, avisos);
        r.PessoaEscolhida = new PessoaResumo { Id = Guid.NewGuid(), Nome = "ABC" };
        Assert.Equal(2, avisos);
    }
}
