using System.Net;
using Lone.Cliente.Api;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class CampoPersonalizadoFormularioTests
{
    private static CampoPersonalizadoDto Campo(TipoCampoPersonalizado tipo, bool obrigatorio = false, params OpcaoCampoDto[] opcoes) =>
        new() { Id = Guid.NewGuid(), Nome = "Campo", Tipo = tipo, Obrigatorio = obrigatorio, Opcoes = [.. opcoes] };

    [Fact]
    public void Inteiro_e_data_vao_e_voltam_no_formato_brasileiro()
    {
        var filhos = Campo(TipoCampoPersonalizado.Inteiro);
        var f = CampoPersonalizadoFormulario.Criar(filhos, new ValorPersonalizadoDto { CampoId = filhos.Id, Numero = 2 });
        Assert.Equal("2", f.Texto);
        f.Texto = "3";
        Assert.Equal(3m, f.ParaDto()!.Numero);

        var data = Campo(TipoCampoPersonalizado.Data);
        var d = CampoPersonalizadoFormulario.Criar(data, null);
        d.Texto = "05/01/2026";
        Assert.Equal(new DateTime(2026, 1, 5), d.ParaDto()!.Data);
        Assert.Equal(TipoMascara.Data, d.Mascara);
    }

    [Fact]
    public void Sim_nao_e_lista_usam_escolha_e_vazio_nao_vai_na_lista()
    {
        var bebe = Campo(TipoCampoPersonalizado.SimNao);
        var f = CampoPersonalizadoFormulario.Criar(bebe, null);
        Assert.Equal(VisualCampo.Escolha, f.Visual);
        Assert.Null(f.ParaDto());

        f.Escolha = f.Opcoes.Single(o => o.Texto == "Não");
        Assert.False(f.ParaDto()!.Logico);

        var casado = new OpcaoCampoDto { Id = Guid.NewGuid(), Texto = "Casado", Ativa = true };
        var viuvo = new OpcaoCampoDto { Id = Guid.NewGuid(), Texto = "Viúvo", Ativa = false };
        var civil = Campo(TipoCampoPersonalizado.Lista, false, casado, viuvo);
        Assert.Equal(new[] { "—", "Casado" }, CampoPersonalizadoFormulario.Criar(civil, null).Opcoes.Select(o => o.Texto));

        // A opção desativada só aparece para quem já tinha ela gravada.
        var gravado = CampoPersonalizadoFormulario.Criar(civil, new ValorPersonalizadoDto { CampoId = civil.Id, OpcaoId = viuvo.Id });
        Assert.Equal("Viúvo (desativada)", gravado.Escolha.Texto);
    }

    [Fact]
    public void Obrigatorio_vazio_e_formato_errado_sao_avisados_no_aparelho()
    {
        var obrigatorio = CampoPersonalizadoFormulario.Criar(Campo(TipoCampoPersonalizado.Texto, obrigatorio: true), null);
        Assert.Equal("Campo *", obrigatorio.Rotulo);
        Assert.Contains("Informe", obrigatorio.Validar());

        var numero = CampoPersonalizadoFormulario.Criar(Campo(TipoCampoPersonalizado.Decimal), null);
        numero.Texto = "abc";
        Assert.Contains("número inválido", numero.Validar());
    }
}

public class AlteracoesPendentesTests
{
    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente)> AbrirAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Etiquetas.EtiquetaDto>())
            .Responder(HttpStatusCode.OK, new List<CampoPersonalizadoDto>
            {
                new() { Id = Guid.NewGuid(), Nome = "Time que torce", Tipo = TipoCampoPersonalizado.Texto, Ativo = true }
            })
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Profissoes.ProfissaoDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Papeis.PapelCadastroDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Contatos.TipoMeioContatoDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Enderecos.TipoEnderecoDto>())
            .Responder(HttpStatusCode.OK, Finalidades.Cadastro)
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Documentos.TipoDocumentoDto>())
            .Responder(HttpStatusCode.OK, new List<CampoPersonalizadoDto>())
            .Responder(HttpStatusCode.OK, new List<PessoaResumo>());
        var tela = PessoasViewModelTests.NovaTela(ambiente);
        await tela.CarregarCommand.ExecuteAsync(null);
        return (tela, ambiente);
    }

    [Fact]
    public async Task Ficha_nova_sem_alteracao_descarta_voltando_a_lista_sem_perguntar()
    {
        var (tela, ambiente) = await AbrirAsync();
        await tela.NovoCommand.ExecuteAsync(null);
        Assert.False(tela.TemAlteracoes);

        await tela.DescartarCommand.ExecuteAsync(null);

        Assert.False(tela.Editando);
        Assert.Empty(ambiente.Dialogos.Perguntas);
    }

    [Fact]
    public async Task Com_alteracao_pergunta_e_continuar_editando_mantem_tudo()
    {
        var (tela, ambiente) = await AbrirAsync();
        await tela.NovoCommand.ExecuteAsync(null);
        tela.Formulario!.Nome = "Carlos";
        Assert.True(tela.TemAlteracoes);

        ambiente.Dialogos.RespostaConfirmacao = false; // "Continuar editando"
        await tela.FecharFichaCommand.ExecuteAsync(null);
        await tela.DescartarCommand.ExecuteAsync(null);

        Assert.True(tela.Editando);
        Assert.Equal("Carlos", tela.Formulario!.Nome);
        Assert.Equal(2, ambiente.Dialogos.Perguntas.Count);
        Assert.All(ambiente.Dialogos.Perguntas, p => Assert.Equal("Existem alterações não salvas. Deseja realmente descartá-las?", p));
    }

    [Fact]
    public async Task Descartar_alteracoes_de_ficha_nova_cancela_a_inclusao()
    {
        var (tela, ambiente) = await AbrirAsync();
        await tela.NovoCommand.ExecuteAsync(null);
        tela.Formulario!.InformacoesAdicionais[0].Texto = "Atlético";

        await tela.DescartarCommand.ExecuteAsync(null); // resposta padrão: "Descartar alterações"

        Assert.False(tela.Editando);
        Assert.DoesNotContain(ambiente.Servidor.Recebidas, r => r.Metodo == HttpMethod.Put); // nada foi gravado nem excluído
    }

    [Fact]
    public async Task Descartar_ficha_existente_volta_aos_dados_gravados()
    {
        var (tela, ambiente) = await AbrirAsync();
        var gravada = PessoaFormulario.NovaPessoa().ParaDto();
        gravada.Nome = "Ana";
        gravada.Codigo = 3;
        ambiente.Servidor.Responder(HttpStatusCode.OK, gravada);
        tela.Selecionado = new PessoaResumo { Id = gravada.Id, Nome = "Ana" };
        await Task.Delay(50);
        Assert.True(tela.Editando);

        tela.Formulario!.Nome = "Ana Alterada";
        ambiente.Servidor.Responder(HttpStatusCode.OK, gravada); // releitura

        await tela.DescartarCommand.ExecuteAsync(null);

        Assert.True(tela.Editando);
        Assert.Equal("Ana", tela.Formulario!.Nome);
        Assert.False(tela.TemAlteracoes);
        Assert.Equal(TipoMensagem.Informacao, tela.TipoMensagem);
    }

    [Fact]
    public async Task Desativar_pede_o_motivo_e_usa_a_acao_propria_da_API()
    {
        var (tela, ambiente) = await AbrirAsync();
        var gravada = PessoaFormulario.NovaPessoa().ParaDto();
        gravada.Nome = "João da Silva";
        gravada.Codigo = 9;
        gravada.Versao = [1, 2, 3];
        ambiente.Servidor.Responder(HttpStatusCode.OK, gravada);
        tela.Selecionado = new PessoaResumo { Id = gravada.Id, Nome = "João da Silva" };
        await Task.Delay(50);
        Assert.True(tela.PodeDesativar);

        var inativa = PessoaFormulario.NovaPessoa().ParaDto();
        inativa.Id = gravada.Id;
        inativa.Nome = gravada.Nome;
        inativa.Codigo = 9;
        inativa.Situacao = SituacaoPessoa.Inativo;
        inativa.SituacaoMotivo = "Mudou de cidade";
        ambiente.Dialogos.RespostaPergunta = "Mudou de cidade";
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, inativa)
            .Responder(HttpStatusCode.OK, new List<PessoaResumo>());

        await tela.DesativarCommand.ExecuteAsync(null);

        var post = Assert.Single(ambiente.Servidor.Recebidas, r => r.Metodo == HttpMethod.Post);
        Assert.Equal("/" + Rotas.Pessoas.Desativar(gravada.Id), post.Caminho);
        Assert.Contains("Mudou de cidade", post.Corpo);
        Assert.True(tela.Formulario!.EstaInativo);
        Assert.True(tela.PodeReativar);
        Assert.False(tela.PodeDesativar);
        Assert.Equal(TipoMensagem.Sucesso, tela.TipoMensagem);
    }

    [Fact]
    public async Task Cancelar_o_motivo_nao_desativa()
    {
        var (tela, ambiente) = await AbrirAsync();
        var gravada = PessoaFormulario.NovaPessoa().ParaDto();
        gravada.Nome = "Ana";
        gravada.Codigo = 1;
        ambiente.Servidor.Responder(HttpStatusCode.OK, gravada);
        tela.Selecionado = new PessoaResumo { Id = gravada.Id, Nome = "Ana" };
        await Task.Delay(50);
        var chamadas = ambiente.Servidor.Recebidas.Count;

        ambiente.Dialogos.RespostaPergunta = null; // Cancelar
        await tela.DesativarCommand.ExecuteAsync(null);

        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count);
    }
}
