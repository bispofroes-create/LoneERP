using System.Net;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Documentos;
using Lone.Contracts.Pessoas;

namespace Lone.Tests.Cliente;

public class AnexosTelaTests
{
    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente, PessoaDto Gravada)> AbrirComDocumentoAsync(params AnexoDto[] anexos)
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Etiquetas.EtiquetaDto>())
            .Responder(HttpStatusCode.OK, new List<CampoPersonalizadoDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Profissoes.ProfissaoDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Papeis.PapelCadastroDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Contatos.TipoMeioContatoDto>())
            .Responder(HttpStatusCode.OK, new List<Lone.Contracts.Enderecos.TipoEnderecoDto>())
            .Responder(HttpStatusCode.OK, new List<TipoDocumentoDto>())
            .Responder(HttpStatusCode.OK, new List<CampoPersonalizadoDto>())
            .Responder(HttpStatusCode.OK, new List<PessoaResumo>());
        var tela = PessoasViewModelTests.NovaTela(ambiente);
        await tela.CarregarCommand.ExecuteAsync(null);

        var gravada = PessoaFormulario.NovaPessoa().ParaDto();
        gravada.Nome = "Ana";
        var documentoId = Guid.NewGuid();
        gravada.Documentos = [new DocumentoDto { Id = documentoId, Numero = "MG123", Anexos = [.. anexos.Select(a => { a.PessoaDocumentoId = documentoId; return a; })] }];
        ambiente.Servidor.Responder(HttpStatusCode.OK, gravada);
        tela.Selecionado = new PessoaResumo { Id = gravada.Id, Nome = "Ana" };
        await Task.Delay(50);
        return (tela, ambiente, gravada);
    }

    private static AnexoDto Anexo(bool ativo = true) => new()
    {
        Id = Guid.NewGuid(), NomeArquivo = "rg.pdf", TipoConteudo = "application/pdf", Tamanho = 2048,
        EnviadoEm = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc), EnviadoPor = "maria", Ativo = ativo
    };

    [Fact]
    public async Task Anexar_envia_na_hora_e_mostra_na_lista()
    {
        var (tela, ambiente, gravada) = await AbrirComDocumentoAsync();
        var documento = Assert.Single(tela.Formulario!.Documentos);
        Assert.True(documento.PodeAnexar);
        ambiente.Arquivos.Escolhido = new ArquivoEscolhido("rg.pdf", [1, 2, 3]);
        var enviado = Anexo();
        ambiente.Servidor.Responder(HttpStatusCode.OK, enviado);

        await documento.AnexarCommand.ExecuteAsync(null);

        var chamada = ambiente.Servidor.Recebidas[^1];
        Assert.EndsWith($"pessoas/{gravada.Id}/documentos/{documento.Id}/anexos", chamada.Caminho);
        Assert.Equal("rg.pdf", Assert.Single(documento.Anexos).NomeArquivo);
        Assert.False(tela.TemAlteracoes); // o anexo não depende do "Salvar" da ficha
    }

    [Fact]
    public async Task Documento_novo_nao_recebe_anexo()
    {
        var (tela, ambiente, _) = await AbrirComDocumentoAsync();
        var novo = new DocumentoFormulario();
        tela.Formulario!.AdicionarDocumento(novo);
        var chamadas = ambiente.Servidor.Recebidas.Count;

        await novo.AnexarCommand.ExecuteAsync(null);

        Assert.False(novo.PodeAnexar);
        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count);
    }

    [Fact]
    public async Task Abrir_baixa_e_entrega_ao_aparelho_e_remover_so_desativa()
    {
        var gravado = Anexo();
        var (tela, ambiente, _) = await AbrirComDocumentoAsync(gravado, Anexo(ativo: false));
        var documento = tela.Formulario!.Documentos[0];
        Assert.Equal(2, documento.Anexos.Count);
        Assert.False(documento.Anexos[1].Visivel); // inativo só em "Mostrar inativos"
        var anexo = documento.Anexos[0];
        Assert.Equal("25/09/2026 · 2 KB · maria", anexo.Detalhe);

        ambiente.Servidor.Responder(HttpStatusCode.OK, new AnexoConteudoDto { NomeArquivo = "rg.pdf", TipoConteudo = "application/pdf", Conteudo = [7, 8] });
        await anexo.AbrirCommand.ExecuteAsync(null);
        Assert.Equal("rg.pdf", Assert.Single(ambiente.Arquivos.Abertos).Nome);

        var desativado = Anexo(ativo: false);
        desativado.Id = gravado.Id;
        ambiente.Servidor.Responder(HttpStatusCode.OK, desativado);
        await anexo.RemoverCommand.ExecuteAsync(null);
        Assert.False(anexo.Ativo);
        Assert.EndsWith($"/anexos/{gravado.Id}/desativar", ambiente.Servidor.Recebidas[^1].Caminho);
    }
}
