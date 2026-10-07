using System.Text.Json;
using Lone.Application.CamposPersonalizados;
using Lone.Application.Documentos;
using Lone.Application.Pessoas;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Documentos;
using Lone.Contracts.Pessoas;
using Lone.Domain.Documentos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// P1-8B: as regras de documento no caminho real (serviços → repositório → SQL Server): repetido na mesma pessoa, aviso e
/// bloqueio entre pessoas (com a garantia do índice único), ativação do bloqueio com diagnóstico, campos dirigidos pelo
/// tipo, emissão futura, formato, legado intocado (D6), proteção dos tipos de sistema (D1) e o histórico mascarado (B0-7).
/// Banco temporário; pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class DocumentosRegrasFluxoTests : IClassFixture<AmbienteCadastroPessoas>
{
    private static readonly Guid Rg = TiposDocumentoSistema.Id(TipoDocumento.Rg);

    private readonly AmbienteCadastroPessoas _ambiente;

    public DocumentosRegrasFluxoTests(AmbienteCadastroPessoas ambiente) => _ambiente = ambiente;

    private BancoDeTeste Banco => _ambiente.Banco!;

    private async Task<ResultadoSalvarPessoa> SalvarAsync(PessoaDto dto)
    {
        await using var requisicao = _ambiente.Requisicao();
        return await requisicao.ServiceProvider.GetRequiredService<IPessoaAppService>().SalvarAsync(dto);
    }

    private async Task<PessoaDto> ObterAsync(Guid id)
    {
        await using var requisicao = _ambiente.Requisicao();
        return (await requisicao.ServiceProvider.GetRequiredService<IPessoaAppService>().ObterAsync(id))!;
    }

    private async Task<TipoDocumentoDto> SalvarTipoAsync(TipoDocumentoDto dto)
    {
        await using var requisicao = _ambiente.Requisicao();
        return await requisicao.ServiceProvider.GetRequiredService<ITipoDocumentoAppService>().SalvarAsync(dto);
    }

    private async Task<TipoDocumentoDto> ObterTipoAsync(Guid id)
    {
        await using var requisicao = _ambiente.Requisicao();
        return (await requisicao.ServiceProvider.GetRequiredService<ITipoDocumentoAppService>().ObterAsync(id))!;
    }

    private static string Unico(string prefixo) => $"{prefixo} {Random.Shared.Next(100000, 999999)}";

    private static PessoaDto Fisica(string nome, params DocumentoDto[] documentos)
    {
        var dto = new PessoaDto { Natureza = NaturezaPessoa.Fisica, Nome = nome, DocumentoPrincipal = DocumentosDeTeste.Cpf() };
        dto.Documentos.AddRange(documentos);
        return dto;
    }

    private static DocumentoDto Documento(Guid tipo, string numero, string? uf = null, string? orgao = null, DateOnly? emissao = null) =>
        new() { Id = Guid.NewGuid(), TipoDocumentoId = tipo, Numero = numero, Uf = uf, OrgaoEmissor = orgao, EmitidoEm = emissao };

    private static async Task<ValidacaoException> RecusadaAsync(Func<Task> acao) => await Assert.ThrowsAsync<ValidacaoException>(acao);

    private static string NumeroUnico() => Random.Shared.Next(10_000_000, 99_999_999).ToString();

    // ---------------------------------------------------------------- mesma pessoa (D2/D6)

    [FatoSqlServer]
    public async Task Mesmo_numero_do_mesmo_tipo_na_mesma_pessoa_e_recusado_mesmo_com_mascara_diferente()
    {
        var n = NumeroUnico();
        var repetido = Documento(Rg, $"{n[..2]}.{n[2..5]}.{n[5..]}-X");
        var erro = await RecusadaAsync(() => SalvarAsync(Fisica("Repetida", Documento(Rg, n + "x"), repetido)));

        Assert.Contains(erro.Itens, i => i.Campo == CamposFichaPessoa.DocumentoNumero && i.Item == repetido.Id && i.Mensagem.Contains("desta pessoa"));
    }

    [FatoSqlServer]
    public async Task Legado_repetido_intocado_nao_trava_a_ficha_e_reativar_o_repetido_e_recusado()
    {
        var n = NumeroUnico();
        var salva = (await SalvarAsync(Fisica("Legado repetido", Documento(Rg, n)))).Pessoa;
        var inativoId = Guid.NewGuid();
        await using (var db = Banco.Contexto())
        {
            db.PessoaDocumentos.AddRange(
                new PessoaDocumento { Id = Guid.NewGuid(), PessoaId = salva.Id, TipoDocumentoId = Rg, Tipo = TipoDocumento.Rg, Numero = n, NumeroNormalizado = n, Ativo = true },
                new PessoaDocumento { Id = inativoId, PessoaId = salva.Id, TipoDocumentoId = Rg, Tipo = TipoDocumento.Rg, Numero = n, NumeroNormalizado = n, Ativo = false });
            await db.SaveChangesAsync(); // cadastro antigo: o mesmo RG duas vezes ativo, e outra inativa
        }

        var aberta = await ObterAsync(salva.Id);
        aberta.Nome += " (alterada)";
        aberta.Documentos.Single(d => d.Id == salva.Documentos[0].Id).Observacoes = "só observação: não conta como alterado";
        var gravada = (await SalvarAsync(aberta)).Pessoa;
        Assert.EndsWith("(alterada)", gravada.Nome);

        gravada.Documentos.Single(d => d.Id == inativoId).Ativo = true;
        var erro = await RecusadaAsync(() => SalvarAsync(gravada));
        Assert.Contains(erro.Itens, i => i.Item == inativoId && i.Campo == CamposFichaPessoa.DocumentoNumero);
    }

    // ---------------------------------------------------------------- entre pessoas

    [FatoSqlServer]
    public async Task Aviso_entre_pessoas_deixa_gravar_e_nao_mostra_o_numero_inteiro()
    {
        var n = NumeroUnico();
        var primeira = (await SalvarAsync(Fisica("Primeira do aviso", Documento(Rg, $"{n[..2]}.{n[2..5]}.{n[5..]}")))).Pessoa;

        var resultado = await SalvarAsync(Fisica("Segunda do aviso", Documento(Rg, n)));

        Assert.True(resultado.Pessoa.Codigo > 0); // gravou
        var aviso = Assert.Single(resultado.Avisos, a => a.StartsWith("Possível duplicidade"));
        Assert.Contains($"terminado em {n[^3..]}", aviso);
        Assert.Contains(primeira.Nome, aviso);
        Assert.DoesNotContain(n, aviso);
        Assert.DoesNotContain(n[..5], aviso);
    }

    [FatoSqlServer]
    public async Task Tipo_que_bloqueia_recusa_o_mesmo_numero_em_outra_pessoa()
    {
        var tipo = await SalvarTipoAsync(new TipoDocumentoDto { Nome = Unico("Registro único"), Unicidade = UnicidadeDocumento.PorTipo });
        var n = "AB-" + NumeroUnico();
        await SalvarAsync(Fisica(Unico("Dona"), Documento(tipo.Id, n)));

        var outra = Documento(tipo.Id, n.Replace("-", " ").ToLowerInvariant());
        var erro = await RecusadaAsync(() => SalvarAsync(Fisica(Unico("Outra"), outra)));
        var item = Assert.Single(erro.Itens, i => i.Item == outra.Id);
        Assert.Equal(CamposFichaPessoa.DocumentoNumero, item.Campo);
        Assert.Contains("não permite repetir", item.Mensagem);
        Assert.DoesNotContain(n, item.Mensagem);

        Assert.True((await SalvarAsync(Fisica(Unico("Outra"), Documento(tipo.Id, n + "9")))).Pessoa.Codigo > 0);
    }

    [FatoSqlServer]
    public async Task Tipo_que_bloqueia_por_UF_aceita_o_numero_em_outra_UF()
    {
        var tipo = await SalvarTipoAsync(new TipoDocumentoDto
        {
            Nome = Unico("Registro estadual"), UsoUf = UsoCampoDocumento.Obrigatorio, Unicidade = UnicidadeDocumento.PorTipoEUf
        });
        var n = NumeroUnico();
        await SalvarAsync(Fisica(Unico("SP"), Documento(tipo.Id, n, uf: "SP")));
        Assert.True((await SalvarAsync(Fisica(Unico("RJ"), Documento(tipo.Id, n, uf: "RJ")))).Pessoa.Codigo > 0);

        var erro = await RecusadaAsync(() => SalvarAsync(Fisica(Unico("SP2"), Documento(tipo.Id, n, uf: "SP"))));
        Assert.Contains(erro.Itens, i => i.Mensagem.Contains("na mesma UF"));
        var semUf = await RecusadaAsync(() => SalvarAsync(Fisica(Unico("Sem UF"), Documento(tipo.Id, n))));
        Assert.Contains(semUf.Itens, i => i.Campo == CamposFichaPessoa.DocumentoUf);
    }

    [FatoSqlServer]
    public async Task Gravacao_simultanea_do_mesmo_numero_e_barrada_pelo_indice_e_vira_erro_no_documento()
    {
        // A conferência do serviço passou para os dois; o índice único barra o segundo (simulado gravando direto).
        var tipo = await SalvarTipoAsync(new TipoDocumentoDto { Nome = Unico("Bloqueio simultâneo"), Unicidade = UnicidadeDocumento.PorTipo });
        var n = NumeroUnico();
        Pessoa ComDocumento(out Guid documento)
        {
            var p = new Pessoa { Id = Lone.Domain.Comum.IdSequencial.Novo(), Nome = Unico("Corrida"), Natureza = NaturezaPessoa.Fisica, DocumentoPrincipal = DocumentosDeTeste.Cpf() };
            documento = Guid.NewGuid();
            p.Documentos.Add(new PessoaDocumento { Id = documento, PessoaId = p.Id, TipoDocumentoId = tipo.Id, Tipo = TipoDocumento.Outro, Numero = n, Ativo = true });
            return p;
        }

        async Task GravarDiretoAsync(Pessoa p)
        {
            await using var requisicao = _ambiente.Requisicao();
            await requisicao.ServiceProvider.GetRequiredService<IPessoaRepositorio>().SalvarAsync(p, nova: true, OrigemAlteracao.Usuario, CancellationToken.None);
        }

        await GravarDiretoAsync(ComDocumento(out _));
        var segunda = ComDocumento(out var documentoDaSegunda);
        var erro = await RecusadaAsync(() => GravarDiretoAsync(segunda));

        var item = Assert.Single(erro.Itens);
        Assert.Equal((ConflitosDocumentoPessoa.NumeroDocumentoJaCadastrado, CamposFichaPessoa.DocumentoNumero, (Guid?)documentoDaSegunda),
                     (item.Mensagem, item.Campo, item.Item));
        Assert.DoesNotContain("IX_", item.Mensagem);
    }

    [FatoSqlServer]
    public async Task Ligar_o_bloqueio_com_repetidos_e_recusado_sem_mudar_nada_e_sem_repetidos_cria_as_chaves()
    {
        var tipo = await SalvarTipoAsync(new TipoDocumentoDto { Nome = Unico("Licença"), Unicidade = UnicidadeDocumento.Aviso });
        var n = NumeroUnico();
        var a = (await SalvarAsync(Fisica(Unico("A"), Documento(tipo.Id, n)))).Pessoa;
        var b = (await SalvarAsync(Fisica(Unico("B"), Documento(tipo.Id, n)))).Pessoa; // aviso: gravou

        var aberto = await ObterTipoAsync(tipo.Id);
        aberto.Unicidade = UnicidadeDocumento.PorTipo;
        var erro = await RecusadaAsync(() => SalvarTipoAsync(aberto));
        Assert.Contains(erro.Erros, e => e.StartsWith("Não é possível bloquear número repetido neste tipo: há 1 número(s) repetido(s)"));
        Assert.Equal(UnicidadeDocumento.Aviso, (await ObterTipoAsync(tipo.Id)).Unicidade);
        await using (var db = Banco.Contexto())
            Assert.False(await db.PessoaDocumentos.AnyAsync(d => d.TipoDocumentoId == tipo.Id && d.ChaveUnicidade != null));

        // Corrigido pelo usuário (o documento de B removido), o bloqueio liga e as chaves aparecem nos documentos do tipo.
        var fichaB = await ObterAsync(b.Id);
        fichaB.Documentos[0].Ativo = false;
        await SalvarAsync(fichaB);
        aberto = await ObterTipoAsync(tipo.Id);
        aberto.Unicidade = UnicidadeDocumento.PorTipo;
        Assert.Equal(UnicidadeDocumento.PorTipo, (await SalvarTipoAsync(aberto)).Unicidade);
        await using (var db = Banco.Contexto())
        {
            var chaves = await db.PessoaDocumentos.AsNoTracking().Where(d => d.TipoDocumentoId == tipo.Id).ToDictionaryAsync(d => d.PessoaId, d => d.ChaveUnicidade);
            Assert.Equal($"{tipo.Id:N}|{n}", chaves[a.Id]);
            Assert.Equal($"{tipo.Id:N}|{n}", chaves[b.Id]); // inativo também tem chave; o índice só olha os ativos
        }

        // Voltar a só avisar limpa as chaves.
        aberto = await ObterTipoAsync(tipo.Id);
        aberto.Unicidade = UnicidadeDocumento.Aviso;
        await SalvarTipoAsync(aberto);
        await using (var db = Banco.Contexto())
            Assert.False(await db.PessoaDocumentos.AnyAsync(d => d.TipoDocumentoId == tipo.Id && d.ChaveUnicidade != null));
    }

    [FatoSqlServer]
    public async Task Numero_sem_comparavel_nao_entra_na_comparacao()
    {
        var tipo = await SalvarTipoAsync(new TipoDocumentoDto { Nome = Unico("Só símbolos"), Unicidade = UnicidadeDocumento.PorTipo });
        await SalvarAsync(Fisica(Unico("Símbolos 1"), Documento(tipo.Id, "ЖД-##")));
        var segunda = await SalvarAsync(Fisica(Unico("Símbolos 2"), Documento(tipo.Id, "ЖД-##")));
        Assert.True(segunda.Pessoa.Codigo > 0);
        Assert.DoesNotContain(segunda.Avisos, a => a.StartsWith("Possível duplicidade"));
    }

    // ---------------------------------------------------------------- campos, emissão, formato

    [FatoSqlServer]
    public async Task Orgao_UF_e_emissao_obrigatorios_pelo_tipo_e_oculto_nao_apaga_o_gravado()
    {
        var tipo = await SalvarTipoAsync(new TipoDocumentoDto
        {
            Nome = Unico("Carteira profissional"), UsoOrgaoEmissor = UsoCampoDocumento.Obrigatorio, UsoUf = UsoCampoDocumento.Obrigatorio,
            UsoEmissao = UsoCampoDocumento.Obrigatorio
        });
        var incompleto = Documento(tipo.Id, NumeroUnico());
        var erro = await RecusadaAsync(() => SalvarAsync(Fisica(Unico("Incompleta"), incompleto)));
        Assert.Contains(erro.Itens, i => (i.Campo, i.Item) == (CamposFichaPessoa.DocumentoOrgaoEmissor, incompleto.Id));
        Assert.Contains(erro.Itens, i => (i.Campo, i.Item) == (CamposFichaPessoa.DocumentoUf, incompleto.Id));
        Assert.Contains(erro.Itens, i => (i.Campo, i.Item) == (CamposFichaPessoa.DocumentoEmitidoEm, incompleto.Id));

        var completa = (await SalvarAsync(Fisica(Unico("Completa"),
            Documento(tipo.Id, NumeroUnico(), uf: "MG", orgao: "CRM", emissao: new DateOnly(2020, 1, 2))))).Pessoa;

        // O tipo passa a "Não usar" os três: o que estava gravado continua (nada é apagado) e nada é exigido.
        var aberto = await ObterTipoAsync(tipo.Id);
        aberto.UsoOrgaoEmissor = aberto.UsoUf = aberto.UsoEmissao = UsoCampoDocumento.Oculto;
        await SalvarTipoAsync(aberto);
        var ficha = await ObterAsync(completa.Id);
        ficha.Nome += " (alterada)";
        var gravada = (await SalvarAsync(ficha)).Pessoa;
        Assert.Equal(("CRM", "MG", (DateOnly?)new DateOnly(2020, 1, 2)), (gravada.Documentos[0].OrgaoEmissor, gravada.Documentos[0].Uf, gravada.Documentos[0].EmitidoEm));
    }

    [FatoSqlServer]
    public async Task Emissao_no_futuro_e_recusada()
    {
        var futuro = Documento(Rg, NumeroUnico(), emissao: DateOnly.FromDateTime(DateTime.Today).AddDays(2));
        var erro = await RecusadaAsync(() => SalvarAsync(Fisica(Unico("Futuro"), futuro)));
        Assert.Contains(erro.Itens, i => (i.Campo, i.Item) == (CamposFichaPessoa.DocumentoEmitidoEm, futuro.Id) && i.Mensagem.Contains("futuro"));
    }

    [FatoSqlServer]
    public async Task Formato_e_tamanho_valem_para_novo_e_alterado_e_o_legado_invalido_intocado_continua()
    {
        var tipo = await SalvarTipoAsync(new TipoDocumentoDto
        {
            Nome = Unico("Matrícula"), FormatoNumero = FormatoNumeroDocumento.SomenteDigitos, TamanhoMinimoNumero = 6, TamanhoMaximoNumero = 8
        });
        var letras = Documento(tipo.Id, "12A456");
        var curto = Documento(tipo.Id, "12.345");
        var erro = await RecusadaAsync(() => SalvarAsync(Fisica(Unico("Formato"), letras, curto)));
        Assert.Contains(erro.Itens, i => i.Item == letras.Id && i.Mensagem.Contains("só números"));
        Assert.Contains(erro.Itens, i => i.Item == curto.Id && i.Mensagem.Contains("pelo menos 6"));

        var salva = (await SalvarAsync(Fisica(Unico("Legado formato"), Documento(tipo.Id, "123.456")))).Pessoa;
        await using (var db = Banco.Contexto())
            await db.PessoaDocumentos.Where(d => d.PessoaId == salva.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Numero, "LEGADO-X").SetProperty(d => d.NumeroNormalizado, "LEGADOX")); // gravado antes da regra
        var ficha = await ObterAsync(salva.Id);
        ficha.Nome += " (alterada)";
        Assert.EndsWith("(alterada)", (await SalvarAsync(ficha)).Pessoa.Nome);

        ficha = await ObterAsync(salva.Id);
        ficha.Documentos[0].Numero = "LEGADO-Y"; // mexeu: passa pela regra
        await RecusadaAsync(() => SalvarAsync(ficha));
    }

    // ---------------------------------------------------------------- D1

    [FatoSqlServer]
    public async Task Tipo_de_sistema_nao_muda_de_nome_nem_de_alcance_e_o_do_usuario_continua_renomeavel()
    {
        var rg = await ObterTipoAsync(Rg);
        rg.Nome = "Registro Geral";
        var erro = await RecusadaAsync(() => SalvarTipoAsync(rg));
        Assert.Contains(erro.Erros, e => e.Contains("não pode ser alterado"));

        rg = await ObterTipoAsync(Rg);
        rg.AplicaPessoaJuridica = true;
        await RecusadaAsync(() => SalvarTipoAsync(rg));

        rg = await ObterTipoAsync(Rg);
        rg.UsoUf = UsoCampoDocumento.Obrigatorio; // as outras regras do tipo de sistema continuam ajustáveis
        Assert.Equal(UsoCampoDocumento.Obrigatorio, (await SalvarTipoAsync(rg)).UsoUf);
        rg = await ObterTipoAsync(Rg);
        rg.UsoUf = UsoCampoDocumento.Opcional;
        Assert.Equal(("RG", UsoCampoDocumento.Opcional), ((await SalvarTipoAsync(rg)).Nome, rg.UsoUf));

        var meu = await SalvarTipoAsync(new TipoDocumentoDto { Nome = Unico("Meu tipo") });
        meu.Nome = Unico("Meu tipo renomeado");
        Assert.Equal(meu.Nome, (await SalvarTipoAsync(meu)).Nome);
    }

    // ---------------------------------------------------------------- B0-7

    [FatoSqlServer]
    public async Task Historico_de_anexo_e_de_campo_do_documento_nao_revela_o_numero_inteiro()
    {
        var campo = await ComServicoAsync<ICampoPersonalizadoAppService, CampoPersonalizadoDto>(s => s.SalvarAsync(new CampoPersonalizadoDto
        {
            Entidade = EntidadePersonalizavel.Documento, TipoDocumentoId = Rg, Nome = Unico("Via"), Tipo = TipoCampoPersonalizado.Texto
        }));
        var documento = Documento(Rg, "ZQ-778.899");
        documento.ValoresPersonalizados.Add(new ValorPersonalizadoDto { CampoId = campo.Id, Texto = "segunda" });
        var salva = (await SalvarAsync(Fisica(Unico("Histórico"), documento))).Pessoa;
        await ComServicoAsync<IAnexoAppService, AnexoDto>(s => s.EnviarAsync(salva.Id, documento.Id,
            new EnviarAnexoRequisicao { NomeArquivo = "rg.pdf", Conteudo = [.. "%PDF-1.4\n"u8.ToArray(), 1, 2, 3] }));

        var historico = await ComServicoAsync<IPessoaAppService, List<Lone.Contracts.Auditoria.RegistroHistorico>>(s => s.ListarHistoricoAsync(salva.Id));
        var texto = JsonSerializer.Serialize(historico);

        Assert.Contains("899", texto);                 // o final aparece (como no próprio campo Número)
        Assert.DoesNotContain("778.899", texto);
        Assert.DoesNotContain("ZQ-778", texto);
        Assert.DoesNotContain("ZQ778899", texto);
        Assert.Equal("RG •••••••899", Lone.Infrastructure.Persistencia.Consultas.AuditoriaConsultas.DocumentoMascarado("RG", "ZQ-778.899"));
    }

    private async Task<TResultado> ComServicoAsync<TServico, TResultado>(Func<TServico, Task<TResultado>> acao) where TServico : notnull
    {
        await using var requisicao = _ambiente.Requisicao();
        return await acao(requisicao.ServiceProvider.GetRequiredService<TServico>());
    }
}
