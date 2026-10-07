using System.Net;
using Lone.Cliente.Mensagens;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Documentos;
using Lone.Contracts.Comum;
using Lone.Contracts.Pessoas;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Cliente;

/// <summary>
/// Correções de UX pós-P1-8, item 3: o anexo removido (inativo, que continua guardado) é consultado em "Mostrar inativos"
/// na aba Documentos, mesmo quando todos os documentos estão ativos. Nada é reativado ou apagado sozinho.
/// </summary>
public class AnexosInativosTests
{
    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente)> AbrirAsync(bool documentoAtivo, params AnexoDto[] anexos)
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Etiquetas.EtiquetaDto>())
            .Responder(HttpStatusCode.OK, new List<CampoPersonalizadoDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Profissoes.ProfissaoDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Profissoes.OcupacaoCboDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Papeis.PapelCadastroDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Contatos.TipoMeioContatoDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Enderecos.TipoEnderecoDto>())
            .Responder(HttpStatusCode.OK, Finalidades.Cadastro)
            .Responder(HttpStatusCode.OK, new List<TipoDocumentoDto>())
            .Responder(HttpStatusCode.OK, new List<CampoPersonalizadoDto>())
            .Responder(HttpStatusCode.OK, new CatalogoFiltrosPessoasDto())
            .Responder(HttpStatusCode.OK, new PaginaListaPessoas());
        var tela = PessoasViewModelTests.NovaTela(ambiente);
        await tela.CarregarCommand.ExecuteAsync(null);

        var gravada = PessoaFormulario.NovaPessoa().ParaDto();
        gravada.Nome = "Ana";
        var documentoId = Guid.NewGuid();
        gravada.Documentos =
        [
            new DocumentoDto
            {
                Id = documentoId, Numero = "MG123", Ativo = documentoAtivo,
                Anexos = [.. anexos.Select(a => { a.PessoaDocumentoId = documentoId; return a; })]
            }
        ];
        ambiente.Servidor.Responder(HttpStatusCode.OK, gravada);
        tela.Selecionado = new PessoaResumo { Id = gravada.Id, Nome = "Ana" };
        await Task.Delay(50);
        return (tela, ambiente);
    }

    private static AnexoDto Anexo(bool ativo = true) => new()
    {
        Id = Guid.NewGuid(), NomeArquivo = "rg.pdf", TipoConteudo = "application/pdf", Tamanho = 2048,
        EnviadoEm = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc), EnviadoPor = "maria", Ativo = ativo
    };

    [Fact]
    public async Task Documento_ativo_com_anexo_removido_oferece_mostrar_inativos_e_o_anexo_aparece()
    {
        var (tela, _) = await AbrirAsync(documentoAtivo: true, Anexo(), Anexo(ativo: false));
        var f = tela.Formulario!;
        var documento = Assert.Single(f.Documentos);
        var removido = documento.Anexos.Single(a => !a.Ativo);

        Assert.True(documento.Ativo);
        Assert.True(f.TemDocumentosInativos);     // a caixa "Mostrar inativos" aparece
        Assert.False(removido.Visivel);

        f.MostrarDocumentosInativos = true;

        Assert.True(removido.Visivel);
        Assert.True(removido.Inativo);             // aparece com "Reativar", sem reativar sozinho
        Assert.True(documento.Visivel);
    }

    [Fact]
    public async Task Sem_nada_inativo_a_caixa_nao_aparece_e_o_anexo_ativo_segue_normal()
    {
        var (tela, _) = await AbrirAsync(documentoAtivo: true, Anexo());
        var f = tela.Formulario!;

        Assert.False(f.TemDocumentosInativos);
        var anexo = Assert.Single(f.Documentos[0].Anexos);
        Assert.True(anexo.Visivel);
        Assert.True(anexo.Ativo);
    }

    [Fact]
    public async Task Documento_inativo_continua_oferecendo_mostrar_inativos()
    {
        var (tela, _) = await AbrirAsync(documentoAtivo: false);
        var f = tela.Formulario!;

        Assert.True(f.TemDocumentosInativos);
        f.MostrarDocumentosInativos = true;
        Assert.True(f.Documentos[0].Visivel);
    }

    [Fact]
    public async Task Remover_anexo_mostra_a_caixa_na_hora_sem_apagar_o_anexo_e_a_mensagem_aponta_para_ela()
    {
        var gravado = Anexo();
        var (tela, ambiente) = await AbrirAsync(documentoAtivo: true, gravado);
        var f = tela.Formulario!;
        var anexo = f.Documentos[0].Anexos[0];
        tela.Mensagens = new ServicoMensagens(new FakeTimeProvider());
        var avisos = new List<string?>();
        f.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);
        Assert.False(f.TemDocumentosInativos);

        var desativado = Anexo(ativo: false);
        desativado.Id = gravado.Id;
        ambiente.Servidor.Responder(HttpStatusCode.OK, desativado);
        await anexo.RemoverCommand.ExecuteAsync(null);

        Assert.EndsWith($"/anexos/{gravado.Id}/desativar", ambiente.Servidor.Recebidas[^1].Caminho); // desativa, não exclui
        Assert.Same(anexo, Assert.Single(f.Documentos[0].Anexos));                                    // continua na ficha
        Assert.False(anexo.Ativo);
        Assert.Contains(nameof(PessoaFormulario.TemDocumentosInativos), avisos);                      // a tela atualiza a caixa
        Assert.True(f.TemDocumentosInativos);
        var toast = Assert.Single(tela.Mensagens.Visiveis);
        Assert.Equal(TipoMensagem.Sucesso, toast.Tipo);
        Assert.Contains("\"Mostrar inativos\"", toast.Texto);                                         // e é lá que ele está
        Assert.False(tela.TemAlteracoes); // anexo não depende do "Salvar" da ficha
    }

    [Fact]
    public async Task Anexo_removido_abre_pelo_mesmo_caminho_e_reativar_so_quando_pedido()
    {
        var removido = Anexo(ativo: false);
        var (tela, ambiente) = await AbrirAsync(documentoAtivo: true, removido);
        var f = tela.Formulario!;
        f.MostrarDocumentosInativos = true;
        var anexo = f.Documentos[0].Anexos[0];

        ambiente.Servidor.Responder(HttpStatusCode.OK, new AnexoConteudoDto { NomeArquivo = "rg.pdf", TipoConteudo = "application/pdf", Conteudo = [7, 8] });
        await anexo.AbrirCommand.ExecuteAsync(null);

        Assert.Equal("/" + Rotas.Anexos.Conteudo(removido.Id), ambiente.Servidor.Recebidas[^1].Caminho); // o download de sempre (a API confere permissão e hash)
        Assert.Equal("rg.pdf", Assert.Single(ambiente.Arquivos.Abertos).Nome);
        Assert.False(anexo.Ativo);                                                           // abrir não reativa

        var reativado = Anexo();
        reativado.Id = removido.Id;
        ambiente.Servidor.Responder(HttpStatusCode.OK, reativado);
        await anexo.ReativarCommand.ExecuteAsync(null);

        Assert.True(anexo.Ativo);
        Assert.False(f.TemDocumentosInativos); // nada mais inativo: a caixa sai
    }
}
