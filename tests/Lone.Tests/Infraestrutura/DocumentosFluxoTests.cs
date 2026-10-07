using Lone.Application.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Documentos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// P1-8A: os documentos da ficha no caminho real (PessoaAppService → regras → repositório → SQL Server). Primeiro o
/// comportamento que já existia (rede de testes pedida no B0), depois o número comparável, a semente no banco e o índice
/// único da chave de unicidade. Pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class DocumentosFluxoTests : IClassFixture<AmbienteCadastroPessoas>
{
    private static readonly Guid Rg = TiposDocumentoSistema.Id(TipoDocumento.Rg);
    private static readonly Guid Outro = TiposDocumentoSistema.Id(TipoDocumento.Outro);

    private readonly AmbienteCadastroPessoas _ambiente;

    public DocumentosFluxoTests(AmbienteCadastroPessoas ambiente) => _ambiente = ambiente;

    private BancoDeTeste Banco => _ambiente.Banco!;

    private async Task<PessoaDto> SalvarAsync(PessoaDto dto)
    {
        await using var requisicao = _ambiente.Requisicao();
        return (await requisicao.ServiceProvider.GetRequiredService<IPessoaAppService>().SalvarAsync(dto)).Pessoa;
    }

    private async Task<PessoaDto> ObterAsync(Guid id)
    {
        await using var requisicao = _ambiente.Requisicao();
        return (await requisicao.ServiceProvider.GetRequiredService<IPessoaAppService>().ObterAsync(id))!;
    }

    private static PessoaDto Fisica(params DocumentoDto[] documentos)
    {
        var dto = new PessoaDto { Natureza = NaturezaPessoa.Fisica, Nome = "Documentos Teste", DocumentoPrincipal = DocumentosDeTeste.Cpf() };
        dto.Documentos.AddRange(documentos);
        return dto;
    }

    private static PessoaDto Juridica()
    {
        var raiz = DocumentosDeTeste.Raiz();
        return new PessoaDto
        {
            Natureza = NaturezaPessoa.Juridica, Nome = "Empresa Documentos " + raiz,
            Estabelecimentos = [new EstabelecimentoDto { Id = Guid.NewGuid(), Principal = true, Cnpj = DocumentosDeTeste.Cnpj(raiz, 1) }]
        };
    }

    private static DocumentoDto Documento(string numero, Guid? tipo = null, string? uf = null) =>
        new() { Id = Guid.NewGuid(), TipoDocumentoId = tipo ?? Rg, Numero = numero, Uf = uf };

    // ---------------------------------------------------------------- comportamento que já existia

    [FatoSqlServer]
    public async Task Documento_removido_fica_inativo_reativado_volta_e_o_que_nao_veio_na_ficha_nao_e_apagado()
    {
        var salva = await SalvarAsync(Fisica(Documento("1.111.111")));
        var id = salva.Documentos[0].Id;

        salva.Documentos[0].Ativo = false;
        var removida = await SalvarAsync(salva);
        Assert.False(Assert.Single(removida.Documentos).Ativo);

        removida.Documentos[0].Ativo = true;
        var reativada = await SalvarAsync(removida);
        Assert.True(Assert.Single(reativada.Documentos).Ativo);

        reativada.Documentos.Clear(); // ficha que não traz o documento: fica como está no banco
        await SalvarAsync(reativada);

        await using var db = Banco.Contexto();
        var gravado = await db.PessoaDocumentos.AsNoTracking().SingleAsync(d => d.PessoaId == salva.Id);
        Assert.Equal((id, true, "1.111.111"), (gravado.Id, gravado.Ativo, gravado.Numero));
    }

    [FatoSqlServer]
    public async Task Historico_do_numero_e_mascarado_e_nao_tem_linha_dos_campos_tecnicos()
    {
        var salva = await SalvarAsync(Fisica(Documento("AB-123.456")));
        salva.Documentos[0].Numero = "CD-789.012";
        await SalvarAsync(salva);

        await using var db = Banco.Contexto();
        var linhas = await db.Auditoria.AsNoTracking().Where(a => a.RaizId == salva.Id).ToListAsync();
        var numero = Assert.Single(linhas, a => a.Acao == AcaoAuditoria.Alteracao && a.Campo == nameof(PessoaDocumento.Numero));
        Assert.EndsWith("456", numero.ValorAnterior);
        Assert.EndsWith("012", numero.ValorNovo);
        foreach (var texto in linhas.SelectMany(a => new[] { a.ValorAnterior, a.ValorNovo, a.Descricao }).OfType<string>())
        {
            Assert.DoesNotContain("123.456", texto);
            Assert.DoesNotContain("789.012", texto);
            Assert.DoesNotContain("AB123456", texto);
            Assert.DoesNotContain("CD789012", texto);
        }
        Assert.DoesNotContain(linhas, a => a.Campo is nameof(PessoaDocumento.NumeroNormalizado) or nameof(PessoaDocumento.ChaveUnicidade));
    }

    [FatoSqlServer]
    public async Task Duas_sessoes_mexendo_nos_documentos_da_mesma_pessoa_dao_conflito_de_edicao()
    {
        var aberta = await SalvarAsync(Fisica(Documento("2.222.222")));
        var outraSessao = await ObterAsync(aberta.Id);
        outraSessao.Documentos[0].Numero = "3.333.333";
        await SalvarAsync(outraSessao);

        aberta.Documentos[0].Observacoes = "gravado depois, com a versão antiga";
        await Assert.ThrowsAnyAsync<ConflitoDeEdicaoException>(() => SalvarAsync(aberta));
        Assert.Equal("3.333.333", (await ObterAsync(aberta.Id)).Documentos[0].Numero);
    }

    [FatoSqlServer]
    public async Task Rg_novo_numa_empresa_e_recusado_pela_regra_do_tipo_gravada_no_banco_e_o_legado_continua()
    {
        var empresa = Juridica();
        empresa.Documentos.Add(Documento("4.444.444"));
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => SalvarAsync(empresa));
        Assert.Contains(erro.Erros, e => e.Contains("não se aplica a pessoa jurídica"));

        empresa.Documentos.Clear();
        var salva = await SalvarAsync(empresa);
        await using (var db = Banco.Contexto())
        {
            db.PessoaDocumentos.Add(new PessoaDocumento
            {
                Id = Guid.NewGuid(), PessoaId = salva.Id, TipoDocumentoId = Rg, Tipo = TipoDocumento.Rg, Numero = "LEGADO-1", Ativo = true
            });
            await db.SaveChangesAsync(); // cadastro antigo: RG numa empresa
        }

        var aberta = await ObterAsync(salva.Id);
        aberta.Nome += " (alterada)";
        var gravada = await SalvarAsync(aberta);
        Assert.Equal("LEGADO-1", Assert.Single(gravada.Documentos).Numero);
    }

    // ---------------------------------------------------------------- P1-8A

    [FatoSqlServer]
    public async Task Documento_guarda_o_numero_digitado_e_o_comparavel_sem_chave_quando_o_tipo_so_avisa()
    {
        var salva = await SalvarAsync(Fisica(Documento(" 12.345.678-x ", uf: "sp")));

        Assert.Equal("12.345.678-X", Assert.Single(salva.Documentos).Numero); // o exibido: aparado e em maiúsculas, como sempre
        await using var db = Banco.Contexto();
        var gravado = await db.PessoaDocumentos.AsNoTracking().SingleAsync(d => d.PessoaId == salva.Id);
        Assert.Equal(("12.345.678-X", "12345678X", (string?)null), (gravado.Numero, gravado.NumeroNormalizado, gravado.ChaveUnicidade));
    }

    [FatoSqlServer]
    public async Task Os_cinco_tipos_de_sistema_no_banco_tem_a_semente_de_paridade()
    {
        await using var db = Banco.Contexto();
        var tipos = await db.TiposDocumento.AsNoTracking().Where(t => t.TipoSistema != null).ToListAsync();
        Assert.Equal(5, tipos.Count);
        foreach (var t in tipos)
        {
            var s = TiposDocumentoSistema.Semente(t.TipoSistema!.Value);
            Assert.Equal((s.Fisica, s.Juridica, s.Estrangeiro, s.Orgao, s.Uf, s.Emissao, s.Formato, s.Unicidade),
                (t.AplicaPessoaFisica, t.AplicaPessoaJuridica, t.AplicaEstrangeiro, t.UsoOrgaoEmissor, t.UsoUf, t.UsoEmissao, t.FormatoNumero, t.Unicidade));
            Assert.Null(t.TamanhoMinimoNumero);
            Assert.Null(t.TamanhoMaximoNumero);
        }
    }

    [FatoSqlServer]
    public async Task Busca_rapida_acha_o_documento_com_ou_sem_pontuacao()
    {
        var sufixo = Random.Shared.Next(100000, 999999).ToString();
        var salva = await SalvarAsync(Fisica(Documento($"77.{sufixo}-9", Outro)));

        await using var db = Banco.Contexto();
        async Task<bool> AchaAsync(string termo) =>
            await PessoaRepositorio.AplicarBusca(db.Pessoas.AsNoTracking(), termo, db).AnyAsync(p => p.Id == salva.Id);

        Assert.True(await AchaAsync($"77.{sufixo}-9")); // como sempre: o digitado
        Assert.True(await AchaAsync($"77{sufixo}9"));    // novo: sem pontuação
        Assert.True(await AchaAsync($"77 {sufixo[..3]}"));  // começo, com outro separador
        Assert.False(await AchaAsync($"78{sufixo}"));
    }

    [FatoSqlServer]
    public async Task Indice_unico_da_chave_barra_dois_ativos_iguais_e_ignora_inativo_e_sem_chave()
    {
        var p1 = await SalvarAsync(Fisica());
        var p2 = await SalvarAsync(Fisica());
        var chave = $"{Outro:N}|IDX{Random.Shared.Next(100000, 999999)}";

        PessoaDocumento Linha(Guid pessoa, bool ativo, string? chaveDoc) => new()
        {
            Id = Guid.NewGuid(), PessoaId = pessoa, TipoDocumentoId = Outro, Tipo = TipoDocumento.Outro, Numero = "X",
            NumeroNormalizado = "X", ChaveUnicidade = chaveDoc, Ativo = ativo
        };

        await using (var db = Banco.Contexto())
        {
            db.PessoaDocumentos.AddRange(Linha(p1.Id, true, chave), Linha(p2.Id, false, chave), Linha(p2.Id, true, null), Linha(p1.Id, true, null));
            await db.SaveChangesAsync(); // inativo com a mesma chave e documentos sem chave não conflitam
        }

        await using (var db = Banco.Contexto())
        {
            db.PessoaDocumentos.Add(Linha(p2.Id, true, chave));
            var erro = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains(((SqlException)erro.InnerException!).Number, new[] { 2601, 2627 });
        }
    }
}
