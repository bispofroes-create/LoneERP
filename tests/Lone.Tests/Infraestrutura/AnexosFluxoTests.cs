using System.Security.Cryptography;
using Lone.Application.Documentos;
using Lone.Application.Pessoas;
using Lone.Contracts.Documentos;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Documentos;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// P1-8A (rede de testes do B0): anexos de documentos no caminho real — aplicação, pasta (temporária, nunca a do sistema),
/// hash, download e permissões. Nada aqui muda o comportamento dos anexos. Pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class AnexosFluxoTests : IClassFixture<AmbienteCadastroPessoas>
{
    private readonly AmbienteCadastroPessoas _ambiente;

    public AnexosFluxoTests(AmbienteCadastroPessoas ambiente) => _ambiente = ambiente;

    private static readonly byte[] Pdf = [.. "%PDF-1.4\n% teste de anexo\n"u8.ToArray(), .. Enumerable.Range(0, 200).Select(i => (byte)i)];

    private async Task<T> ComAsync<T>(Func<IServiceProvider, Task<T>> acao, params string[] negadas)
    {
        await using var requisicao = _ambiente.Requisicao();
        requisicao.ServiceProvider.GetRequiredService<UsuarioDoTeste>().Negadas.UnionWith(negadas);
        return await acao(requisicao.ServiceProvider);
    }

    private Task<AnexoDto> EnviarAsync(Guid pessoa, Guid documento, byte[] conteudo, string nome = "rg-frente.pdf", params string[] negadas) =>
        ComAsync(sp => sp.GetRequiredService<IAnexoAppService>().EnviarAsync(pessoa, documento,
            new EnviarAnexoRequisicao { NomeArquivo = nome, Conteudo = conteudo }), negadas);

    private Task<AnexoConteudoDto> BaixarAsync(Guid anexo, params string[] negadas) =>
        ComAsync(sp => sp.GetRequiredService<IAnexoAppService>().BaixarAsync(anexo), negadas);

    private async Task<PessoaDto> PessoaComDocumentoAsync(bool ativo = true)
    {
        var dto = new PessoaDto { Natureza = NaturezaPessoa.Fisica, Nome = "Anexos Teste", DocumentoPrincipal = DocumentosDeTeste.Cpf() };
        dto.Documentos.Add(new DocumentoDto { Id = Guid.NewGuid(), TipoDocumentoId = TiposDocumentoSistema.Id(TipoDocumento.Rg), Numero = "9.999.999" });
        var salva = await ComAsync(async sp => (await sp.GetRequiredService<IPessoaAppService>().SalvarAsync(dto)).Pessoa);
        if (ativo) return salva;
        salva.Documentos[0].Ativo = false;
        return await ComAsync(async sp => (await sp.GetRequiredService<IPessoaAppService>().SalvarAsync(salva)).Pessoa);
    }

    [FatoSqlServer]
    public async Task Envio_grava_o_arquivo_na_pasta_com_hash_e_o_download_devolve_o_mesmo_conteudo()
    {
        var pessoa = await PessoaComDocumentoAsync();
        var documento = pessoa.Documentos[0].Id;

        var anexo = await EnviarAsync(pessoa.Id, documento, Pdf);

        Assert.Equal(("rg-frente.pdf", "application/pdf", (long)Pdf.Length, true), (anexo.NomeArquivo, anexo.TipoConteudo, anexo.Tamanho, anexo.Ativo));
        await using (var db = _ambiente.Banco!.Contexto())
        {
            var registro = await db.AnexosDocumento.AsNoTracking().SingleAsync(a => a.Id == anexo.Id);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Pdf)), registro.Hash, ignoreCase: true);
            Assert.True(File.Exists(Path.Combine(_ambiente.PastaAnexos, registro.Caminho)));
        }
        Assert.Equal(Pdf, (await BaixarAsync(anexo.Id)).Conteudo);

        var dto = await ComAsync(async sp => (await sp.GetRequiredService<IPessoaAppService>().ObterAsync(pessoa.Id))!);
        Assert.Equal(anexo.Id, Assert.Single(dto.Documentos[0].Anexos).Id);
    }

    [FatoSqlServer]
    public async Task Arquivo_alterado_na_pasta_e_recusado_no_download()
    {
        var pessoa = await PessoaComDocumentoAsync();
        var anexo = await EnviarAsync(pessoa.Id, pessoa.Documentos[0].Id, Pdf);
        await using (var db = _ambiente.Banco!.Contexto())
        {
            var caminho = (await db.AnexosDocumento.AsNoTracking().SingleAsync(a => a.Id == anexo.Id)).Caminho;
            await File.WriteAllBytesAsync(Path.Combine(_ambiente.PastaAnexos, caminho), [.. Pdf, 0x20]);
        }

        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => BaixarAsync(anexo.Id));
        Assert.Contains(erro.Erros, e => e.Contains("danificado"));
    }

    [FatoSqlServer]
    public async Task Documento_inativo_ou_que_nao_e_da_pessoa_nao_recebe_anexo()
    {
        var inativo = await PessoaComDocumentoAsync(ativo: false);
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => EnviarAsync(inativo.Id, inativo.Documentos[0].Id, Pdf));
        Assert.Contains(erro.Erros, e => e.Contains("reative-o"));

        var outra = await PessoaComDocumentoAsync();
        await Assert.ThrowsAsync<ValidacaoException>(() => EnviarAsync(outra.Id, inativo.Documentos[0].Id, Pdf));
        await Assert.ThrowsAsync<ValidacaoException>(() => EnviarAsync(outra.Id, Guid.NewGuid(), Pdf));
    }

    [FatoSqlServer]
    public async Task Remover_anexo_so_desativa_e_o_arquivo_continua()
    {
        var pessoa = await PessoaComDocumentoAsync();
        var anexo = await EnviarAsync(pessoa.Id, pessoa.Documentos[0].Id, Pdf);

        var removido = await ComAsync(sp => sp.GetRequiredService<IAnexoAppService>().DesativarAsync(anexo.Id));
        Assert.False(removido.Ativo);
        Assert.Equal(Pdf, (await BaixarAsync(anexo.Id)).Conteudo); // continua no histórico, íntegro

        var reativado = await ComAsync(sp => sp.GetRequiredService<IAnexoAppService>().ReativarAsync(anexo.Id));
        Assert.True(reativado.Ativo);
        await using var db = _ambiente.Banco!.Contexto();
        Assert.Equal(1, await db.AnexosDocumento.CountAsync(a => a.PessoaDocumentoId == pessoa.Documentos[0].Id));
    }

    [FatoSqlServer]
    public async Task Anexo_removido_continua_na_leitura_da_ficha_com_o_mesmo_arquivo_e_hash()
    {
        // Correções de UX pós-P1-8 (item 3): a ficha mostra o anexo removido em "Mostrar inativos" porque a leitura da
        // pessoa continua trazendo ele, intacto. Remover não apaga, não troca o arquivo e não reativa nada.
        var pessoa = await PessoaComDocumentoAsync();
        var anexo = await EnviarAsync(pessoa.Id, pessoa.Documentos[0].Id, Pdf);
        string hashAntes;
        await using (var antes = _ambiente.Banco!.Contexto())
            hashAntes = (await antes.AnexosDocumento.SingleAsync(a => a.Id == anexo.Id)).Hash;

        await ComAsync(sp => sp.GetRequiredService<IAnexoAppService>().DesativarAsync(anexo.Id));
        var lida = await ComAsync(async sp => (await sp.GetRequiredService<IPessoaAppService>().ObterAsync(pessoa.Id))!);

        var documento = Assert.Single(lida.Documentos);
        Assert.True(documento.Ativo);
        var devolvido = Assert.Single(documento.Anexos);
        Assert.Equal(anexo.Id, devolvido.Id);
        Assert.False(devolvido.Ativo);
        Assert.Equal(Pdf.Length, devolvido.Tamanho);
        await using var depois = _ambiente.Banco!.Contexto();
        var gravado = await depois.AnexosDocumento.SingleAsync(a => a.Id == anexo.Id);
        Assert.Equal(hashAntes, gravado.Hash);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Pdf)), gravado.Hash);
        Assert.Equal(Pdf, (await BaixarAsync(anexo.Id)).Conteudo);
    }

    [FatoSqlServer]
    public async Task Permissoes_dos_anexos()
    {
        var pessoa = await PessoaComDocumentoAsync();
        await Assert.ThrowsAsync<AcessoNegadoException>(() => EnviarAsync(pessoa.Id, pessoa.Documentos[0].Id, Pdf, negadas: [Permissoes.Pessoas.Editar]));

        var anexo = await EnviarAsync(pessoa.Id, pessoa.Documentos[0].Id, Pdf);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => BaixarAsync(anexo.Id, Permissoes.Pessoas.Visualizar));
        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            ComAsync(sp => sp.GetRequiredService<IAnexoAppService>().DesativarAsync(anexo.Id), Permissoes.Pessoas.Editar));
    }
}
