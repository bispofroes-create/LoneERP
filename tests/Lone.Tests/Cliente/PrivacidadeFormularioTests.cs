using System.Net;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Aba "Privacidade": só mostra o que a API calculou; conceder/revogar são ações próprias (fora do Salvar).</summary>
public class PrivacidadeFormularioTests
{
    private static readonly Guid Marketing = new("7a9e1c08-0000-0000-0000-000000000001");

    private static PrivacidadeDto Dados() => new()
    {
        Finalidades = [new FinalidadeConsentimentoDto { FinalidadeId = Marketing, Nome = "Marketing", BaseLegal = BaseLegal.Consentimento, Situacao = SituacaoConsentimento.NaoInformado }],
        Consentimentos =
        [
            new ConsentimentoDto { Id = Guid.NewGuid(), FinalidadeId = Marketing, Finalidade = "Marketing", Canal = CanalComunicacao.Email, EmVigor = true,
                                   ConcedidoEm = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc), ConcedidoPor = "Maria", Motivo = "Balcão" },
            new ConsentimentoDto { Id = Guid.NewGuid(), FinalidadeId = Marketing, Finalidade = "Marketing", EmVigor = false,
                                   ConcedidoEm = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc), RevogadoEm = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc),
                                   MotivoRevogacao = "Pediu" },
            new ConsentimentoDto { Id = Guid.NewGuid(), FinalidadeId = Guid.NewGuid(), Finalidade = "Registro anterior", SomenteHistorico = true,
                                   Canal = CanalComunicacao.WhatsApp, EmVigor = true }
        ],
        Canais =
        [
            new CanalPrivacidadeDto
            {
                MeioContatoId = Guid.NewGuid(), Tipo = TipoContato.Email, Valor = "ana@exemplo.com.br", AceitaComunicacoes = true, UsoParaMarketing = false,
                Decisoes = [new DecisaoCanalDto { FinalidadeId = Marketing, Finalidade = "Marketing", Canal = CanalComunicacao.Email,
                                                  Resultado = ResultadoComunicacao.BloqueadoPorFinalidade, Motivo = "Não está marcado \"Uso para marketing\"." }]
            }
        ]
    };

    [Fact]
    public void Mostra_situacoes_periodos_e_canais_sem_confundir_os_conceitos()
    {
        var p = new PrivacidadeFormulario();
        p.Carregar(Dados());

        Assert.True(p.Carregada);
        Assert.Equal("Não informado", Assert.Single(p.Finalidades).Situacao);
        Assert.Single(p.Periodos, x => x.Visivel); // só o em vigor; revogado e registro anterior ficam no histórico
        Assert.True(p.TemHistorico);
        p.MostrarHistorico = true;
        Assert.Equal(3, p.Periodos.Count(x => x.Visivel));

        var anterior = p.Periodos.Single(x => x.Dados.SomenteHistorico);
        Assert.False(anterior.PodeRevogar); // registro anterior não se revoga
        Assert.Contains("não autoriza", anterior.Situacao);
        Assert.True(p.Periodos.Single(x => x.Dados.EmVigor && !x.Dados.SomenteHistorico).PodeRevogar);
        Assert.StartsWith("Revogado", p.Periodos.Single(x => !x.Dados.EmVigor).Situacao);

        var canal = Assert.Single(p.Canais);
        Assert.Contains("Aceita comunicações: Sim", canal.Marcas);
        Assert.Contains("Uso para marketing: Não", canal.Marcas);
        Assert.Contains("não pode", Assert.Single(canal.Decisoes));
    }

    [Fact]
    public void Conceder_exige_finalidade_e_motivo_e_vai_com_canal_opcional()
    {
        var p = new PrivacidadeFormulario();
        p.Carregar(Dados());
        Assert.Equal(Marketing, p.NovaFinalidade.Valor); // única finalidade: já vem escolhida
        Assert.Contains(p.ValidarConcessao(), e => e.Contains("motivo"));

        p.NovoMotivo = "Autorizou no balcão";
        p.NovaVersaoTermo = "v1.0";
        Assert.Empty(p.ValidarConcessao());
        var requisicao = p.ParaConcessao();
        Assert.Null(requisicao.Canal); // "Qualquer canal"
        Assert.Equal("v1.0", requisicao.VersaoTermo);

        p.NovoCanal = PrivacidadeFormulario.OpcoesCanal.Single(o => o.Valor == CanalComunicacao.Email);
        Assert.Equal(CanalComunicacao.Email, p.ParaConcessao().Canal);
    }

    [Fact]
    public async Task Aba_Privacidade_so_com_permissao_e_cadastro_gravado_e_conceder_usa_a_acao_propria()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao()); // administrador: tem PESSOAS.PRIVACIDADE
        var gravada = PessoaFormulario.NovaPessoa().ParaDto();
        gravada.Nome = "Ana";
        gravada.Codigo = 5;
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        ambiente.Servidor.Responder(HttpStatusCode.OK, gravada);
        tela.Selecionado = new PessoaResumo { Id = gravada.Id, Nome = "Ana" };
        await Task.Delay(50);

        var aba = Assert.Single(tela.Secoes, s => s.Secao == SecaoPessoa.Privacidade);
        Assert.Contains(tela.Secoes, s => s.Secao == SecaoPessoa.Relacionamento && s.Texto == "Interações");
        ambiente.Servidor.Responder(HttpStatusCode.OK, Dados());
        tela.SecaoSelecionada = aba;
        await Task.Delay(50);
        Assert.True(tela.NaPrivacidade);
        Assert.True(tela.Formulario!.Privacidade.Carregada);

        var p = tela.Formulario.Privacidade;
        p.NovoMotivo = "Autorizou no balcão";
        ambiente.Servidor.Responder(HttpStatusCode.OK, Dados());
        await p.ConcederCommand.ExecuteAsync(null);

        var post = ambiente.Servidor.Recebidas.Last();
        Assert.Equal(HttpMethod.Post, post.Metodo);
        Assert.Equal("/" + Rotas.Pessoas.Consentimentos(gravada.Id), post.Caminho);
        Assert.Contains("Autorizou no balc", post.Corpo);
        Assert.DoesNotContain(ambiente.Servidor.Recebidas, r => r.Metodo == HttpMethod.Put); // não passa pelo Salvar
        Assert.Equal(string.Empty, p.NovoMotivo); // limpo depois de conceder
    }

    [Fact]
    public void Sem_permissao_ou_cadastro_novo_nao_ha_aba_Privacidade()
    {
        var nova = PessoaFormulario.NovaPessoa();
        nova.PodeVerPrivacidade = true;
        Assert.DoesNotContain(SecaoOpcao.Para(nova), s => s.Secao == SecaoPessoa.Privacidade);

        var gravada = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Codigo = 3, Nome = "Ana" });
        Assert.DoesNotContain(SecaoOpcao.Para(gravada), s => s.Secao == SecaoPessoa.Privacidade);
        gravada.PodeVerPrivacidade = true;
        Assert.Contains(SecaoOpcao.Para(gravada), s => s.Secao == SecaoPessoa.Privacidade);
    }
}
