using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Integracoes.Cep;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// F6 no SQL Server: seleção dos endereços a reconferir (situação, idade, elegibilidade, ordem, limite), gravação protegida
/// contra edição concorrente (só as três colunas de conferência) e limpeza do histórico ConsultasCep em lotes, sem tocar em
/// CacheCep, endereços ou Auditoria. Cada teste usa uma UF própria para não enxergar os dados dos outros (banco por classe).
/// Pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class BancoReconferenciaCepTests : IClassFixture<BancoComAuditoriaProtegida>
{
    private static readonly DateOnly Hoje = new(2026, 10, 5);
    private static readonly DateTime Agora = new(2026, 10, 5, 20, 30, 0, DateTimeKind.Utc);
    private readonly BancoComAuditoriaProtegida _fixture;

    public BancoReconferenciaCepTests(BancoComAuditoriaProtegida fixture) => _fixture = fixture;

    private BancoDeTeste Banco => _fixture.Banco!;
    private ReconferenciaCepSql Sql() => new(new MotorTerritorialTeste.Fabrica(Banco));

    private PessoaRepositorio Repositorio() =>
        new(new MotorTerritorialTeste.Fabrica(Banco), new MotorTerritorialTeste.UsuarioTeste(), new MotorTerritorialTeste.EscopoTudo(Hoje));

    private static PessoaEndereco End(string uf, CepSituacao situacao, int? diasDesdeConferencia = null, string? cep = "35790000",
                                      bool ativo = true, string pais = PessoaEndereco.CodigoPaisBrasil, string numero = "150") => new()
    {
        Id = Guid.NewGuid(), Cep = cep, Logradouro = "Rua Barão", Numero = numero, Bairro = "Centro", Cidade = "Curvelo", Uf = uf,
        Ativo = ativo, CodigoPais = pais, CepSituacao = situacao,
        CepFonte = diasDesdeConferencia is null ? null : CepFonte.ViaCep,
        CepConferidoEm = diasDesdeConferencia is { } d ? Agora.AddDays(-d) : null
    };

    private async Task<Guid> PessoaAsync(SituacaoPessoa situacao, params PessoaEndereco[] enderecos)
    {
        var id = Guid.NewGuid();
        var p = new Pessoa { Id = id, Nome = "F6 " + id.ToString("N")[..6], Natureza = NaturezaPessoa.Juridica, Situacao = situacao };
        foreach (var e in enderecos)
        {
            e.PessoaId = id;
            p.Enderecos.Add(e);
        }
        await Repositorio().SalvarAsync(p, nova: true, OrigemAlteracao.Usuario, CancellationToken.None);
        return id;
    }

    [FatoSqlServer]
    public async Task Selecao_traz_so_os_elegiveis_conta_os_sem_cep_e_ordena_pelos_mais_antigos()
    {
        const string uf = "AC";
        var nunca = End(uf, CepSituacao.NaoConferido);
        var divergente = End(uf, CepSituacao.Divergente, 10);
        var naoEncontrado = End(uf, CepSituacao.NaoEncontrado, 50);
        var antigo = End(uf, CepSituacao.Conferido, 200);
        var recente = End(uf, CepSituacao.Conferido, 10);                       // conferido há pouco: fica de fora
        var exterior = End(uf, CepSituacao.NaoConferido, pais: "0132");          // fora do Brasil
        var inativo = End(uf, CepSituacao.NaoConferido, ativo: false);
        var semCep = End(uf, CepSituacao.NaoConferido, cep: null);
        await PessoaAsync(SituacaoPessoa.Ativo, nunca, divergente, naoEncontrado, antigo, recente, exterior, inativo, semCep);
        var arquivado = End(uf, CepSituacao.NaoConferido);
        await PessoaAsync(SituacaoPessoa.Arquivado, arquivado);

        var s = await Sql().SelecionarAsync(new CriterioReconferenciaCep(Uf: uf), Agora);

        Assert.Equal([nunca.Id, antigo.Id, naoEncontrado.Id, divergente.Id], s.Enderecos); // nunca conferido primeiro, depois o mais antigo
        Assert.Equal(4, s.Total);
        Assert.Equal(1, s.SemCepValido);
        Assert.False(s.Truncada);
    }

    [FatoSqlServer]
    public async Task Filtros_por_situacao_e_limite_truncam_a_lista_mas_nao_o_total()
    {
        const string uf = "AL";
        var d1 = End(uf, CepSituacao.Divergente, 30);
        var d2 = End(uf, CepSituacao.Divergente, 20);
        await PessoaAsync(SituacaoPessoa.Ativo, d1, d2, End(uf, CepSituacao.NaoConferido), End(uf, CepSituacao.Conferido, 300));

        var s = await Sql().SelecionarAsync(new CriterioReconferenciaCep(false, true, false, false, uf, Limite: 1), Agora);

        Assert.Equal(2, s.Total);
        Assert.Equal([d1.Id], s.Enderecos);
        Assert.True(s.Truncada);
    }

    [FatoSqlServer]
    public async Task Selecao_sem_nada_elegivel_volta_vazia()
    {
        await PessoaAsync(SituacaoPessoa.Ativo, End("AP", CepSituacao.Conferido, 5));

        var s = await Sql().SelecionarAsync(new CriterioReconferenciaCep(Uf: "AP"), Agora);

        Assert.Equal((0, 0, 0), (s.Total, s.Enderecos.Count, s.SemCepValido));
    }

    [FatoSqlServer]
    public async Task Obter_le_os_dados_conferidos_e_marca_o_arquivado_como_nao_processavel()
    {
        var ativo = End("AM", CepSituacao.Divergente, 10);
        var pessoa = await PessoaAsync(SituacaoPessoa.Ativo, ativo);
        var doArquivado = End("AM", CepSituacao.NaoConferido);
        await PessoaAsync(SituacaoPessoa.Arquivado, doArquivado);

        var lidos = (await Sql().ObterAsync([ativo.Id, doArquivado.Id, Guid.NewGuid()])).ToDictionary(e => e.EnderecoId);

        Assert.Equal(2, lidos.Count); // o id inexistente não volta
        var a = lidos[ativo.Id];
        Assert.Equal((pessoa, "35790000", "Rua Barão", "150", "Centro", "Curvelo", "AM", true, true, CepSituacao.Divergente),
            (a.PessoaId, a.Cep, a.Logradouro, a.Numero, a.Bairro, a.Cidade, a.Uf, a.Ativo, a.EhBrasil, a.Situacao));
        Assert.False(lidos[doArquivado.Id].Ativo);
    }

    [FatoSqlServer]
    public async Task Gravar_altera_so_as_tres_colunas_de_conferencia_e_nao_mexe_no_endereco()
    {
        var e = End("BA", CepSituacao.Divergente, 40);
        await PessoaAsync(SituacaoPessoa.Ativo, e);
        var lido = (await Sql().ObterAsync([e.Id])).Single();

        var ok = await Sql().GravarAsync(lido, new ConferenciaCepRealizada(CepSituacao.Conferido, CepFonte.BrasilApi, Agora));

        Assert.True(ok);
        await using var db = Banco.Contexto();
        var depois = await db.PessoaEnderecos.AsNoTracking().SingleAsync(x => x.Id == e.Id);
        Assert.Equal((CepSituacao.Conferido, (CepFonte?)CepFonte.BrasilApi, (DateTime?)Agora),
            (depois.CepSituacao, depois.CepFonte, depois.CepConferidoEm));
        Assert.Equal(("35790000", "Rua Barão", "150", "Centro", "Curvelo", "BA", true),
            (depois.Cep, depois.Logradouro, depois.Numero, depois.Bairro, depois.Cidade, depois.Uf, depois.Ativo));
        Assert.Equal(e.Complemento, depois.Complemento);
    }

    [FatoSqlServer]
    public async Task Endereco_editado_no_meio_nao_recebe_o_resultado_antigo()
    {
        var e = End("CE", CepSituacao.NaoConferido);
        await PessoaAsync(SituacaoPessoa.Ativo, e);
        var lido = (await Sql().ObterAsync([e.Id])).Single();

        // Outra pessoa salva a ficha (número trocado) enquanto a consulta estava em andamento.
        await using (var db = Banco.Contexto())
            await db.PessoaEnderecos.Where(x => x.Id == e.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Numero, "151"));

        var ok = await Sql().GravarAsync(lido, new ConferenciaCepRealizada(CepSituacao.Conferido, CepFonte.ViaCep, Agora));

        Assert.False(ok);
        await using var leitura = Banco.Contexto();
        var depois = await leitura.PessoaEnderecos.AsNoTracking().SingleAsync(x => x.Id == e.Id);
        Assert.Equal((CepSituacao.NaoConferido, (CepFonte?)null, (DateTime?)null, "151"),
            (depois.CepSituacao, depois.CepFonte, depois.CepConferidoEm, depois.Numero));
    }

    [FatoSqlServer]
    public async Task Endereco_desativado_no_meio_tambem_nao_recebe()
    {
        var e = End("ES", CepSituacao.NaoConferido);
        await PessoaAsync(SituacaoPessoa.Ativo, e);
        var lido = (await Sql().ObterAsync([e.Id])).Single();
        await using (var db = Banco.Contexto())
            await db.PessoaEnderecos.Where(x => x.Id == e.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Ativo, false));

        Assert.False(await Sql().GravarAsync(lido, new ConferenciaCepRealizada(CepSituacao.Conferido, CepFonte.ViaCep, Agora)));
    }

    [FatoSqlServer]
    public async Task Limpeza_remove_em_lotes_so_o_historico_antigo_e_preserva_cache_enderecos_e_auditoria()
    {
        var marca = Guid.NewGuid().ToString("N")[..6];
        var historico = new HistoricoConsultasCepSql(new MotorTerritorialTeste.Fabrica(Banco), NullLogger<HistoricoConsultasCepSql>.Instance);
        var antigo = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
            await historico.RegistrarAsync(new ConsultaCepOcorrida(OperacaoConsultaCep.ReconferenciaLote, TipoConsultaCep.PorCep,
                $"cep:velho-{marca}-{i}", "35790000", CepFonte.ViaCep, antigo.AddDays(i), TimeSpan.Zero, ResultadoConsultaCep.Encontrado,
                OrigemRespostaCep.Fonte));
        await historico.RegistrarAsync(new ConsultaCepOcorrida(OperacaoConsultaCep.Conferencia, TipoConsultaCep.PorCep,
            $"cep:novo-{marca}", "35790000", CepFonte.ViaCep, new DateTime(2001, 12, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero,
            ResultadoConsultaCep.Encontrado, OrigemRespostaCep.Fonte));
        var cache = new CachePostalCepSql(new MotorTerritorialTeste.Fabrica(Banco), NullLogger<CachePostalCepSql>.Instance);
        var chaveCache = $"cep:cache-{marca}";
        await cache.GuardarAsync(chaveCache, TipoConsultaCep.PorCep, "35790000", new RespostaGuardadaCep(SituacaoCachePostal.Encontrado,
            CepFonte.ViaCep, [new RegistroCep("35790000", "Rua Barão", null, "Centro", "Curvelo", "MG")], false, antigo, antigo.AddHours(24),
            antigo.AddDays(30)));
        await PessoaAsync(SituacaoPessoa.Ativo, End("GO", CepSituacao.NaoConferido));
        int auditoria, enderecos;
        await using (var db = Banco.Contexto())
        {
            auditoria = await db.Auditoria.CountAsync();
            enderecos = await db.PessoaEnderecos.CountAsync();
        }
        var limite = new DateTime(2001, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var removidos = await Sql().LimparAsync(limite, tamanhoLote: 2); // 5 linhas em lotes de 2: três voltas
        var deNovo = await Sql().LimparAsync(limite, tamanhoLote: 2);    // idempotente

        Assert.Equal(5, removidos);
        Assert.Equal(0, deNovo);
        await using var leitura = Banco.Contexto();
        Assert.False(await leitura.ConsultasCep.AnyAsync(h => h.Chave.StartsWith($"cep:velho-{marca}")));
        Assert.True(await leitura.ConsultasCep.AnyAsync(h => h.Chave == $"cep:novo-{marca}"));
        Assert.True(await leitura.CacheCep.AnyAsync(c => c.Chave == chaveCache)); // cache postal tem a própria validade
        Assert.Equal(auditoria, await leitura.Auditoria.CountAsync());
        Assert.Equal(enderecos, await leitura.PessoaEnderecos.CountAsync());
    }

    [FatoSqlServer]
    public async Task Limpeza_cancelada_antes_de_comecar_nao_remove_nada()
    {
        var marca = Guid.NewGuid().ToString("N")[..6];
        var historico = new HistoricoConsultasCepSql(new MotorTerritorialTeste.Fabrica(Banco), NullLogger<HistoricoConsultasCepSql>.Instance);
        await historico.RegistrarAsync(new ConsultaCepOcorrida(OperacaoConsultaCep.Conferencia, TipoConsultaCep.PorCep, $"cep:canc-{marca}",
            "35790000", CepFonte.ViaCep, new DateTime(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero, ResultadoConsultaCep.Encontrado,
            OrigemRespostaCep.Fonte));
        using var cancelado = new CancellationTokenSource();
        cancelado.Cancel();

        var removidos = await Sql().LimparAsync(new DateTime(2003, 1, 1, 0, 0, 0, DateTimeKind.Utc), 100, cancelado.Token);

        Assert.Equal(0, removidos);
        await using var db = Banco.Contexto();
        Assert.True(await db.ConsultasCep.AnyAsync(h => h.Chave == $"cep:canc-{marca}"));
    }

    // ---- G, ajuste 2.1: a trava contra resposta velha com os campos opcionais nulos (semântica nula do EF/SQL Server) ----

    private static PessoaEndereco SemOpcionais(string uf) => new()
    {
        Id = Guid.NewGuid(), Cep = "35790000", Logradouro = "Rua Barão", Numero = null, Bairro = null, Cidade = "Curvelo", Uf = uf,
        MunicipioId = null, CodigoMunicipioIbge = null, CepSituacao = CepSituacao.NaoConferido
    };

    [FatoSqlServer]
    public async Task Trava_funciona_com_numero_bairro_municipio_e_ibge_nulos()
    {
        var e = SemOpcionais("MA");
        await PessoaAsync(SituacaoPessoa.Ativo, e);
        var lido = (await Sql().ObterAsync([e.Id])).Single();
        Assert.Equal((null, null, null, null), (lido.Numero, lido.Bairro, lido.MunicipioId, lido.CodigoMunicipioIbge));

        var ok = await Sql().GravarAsync(lido, new ConferenciaCepRealizada(CepSituacao.Divergente, CepFonte.ViaCep, Agora));

        Assert.True(ok); // nulo lido = nulo gravado: a condição "IS NULL" casa
        await using var db = Banco.Contexto();
        var depois = await db.PessoaEnderecos.AsNoTracking().SingleAsync(x => x.Id == e.Id);
        Assert.Equal((CepSituacao.Divergente, (string?)null, (string?)null), (depois.CepSituacao, depois.Numero, depois.Bairro));
    }

    // Um teste por campo (sem teoria: o pulado sem SQL Server tem de aparecer como pulado, nunca como aprovado).
    [FatoSqlServer] public Task Numero_preenchido_durante_a_reconferencia_bloqueia() => NuloPreenchidoBloqueiaAsync("Numero");
    [FatoSqlServer] public Task Bairro_preenchido_durante_a_reconferencia_bloqueia() => NuloPreenchidoBloqueiaAsync("Bairro");
    [FatoSqlServer] public Task Municipio_preenchido_durante_a_reconferencia_bloqueia() => NuloPreenchidoBloqueiaAsync("MunicipioId");
    [FatoSqlServer] public Task Ibge_preenchido_durante_a_reconferencia_bloqueia() => NuloPreenchidoBloqueiaAsync("CodigoMunicipioIbge");

    private async Task NuloPreenchidoBloqueiaAsync(string campo)
    {
        var e = SemOpcionais("PA");
        await PessoaAsync(SituacaoPessoa.Ativo, e);
        var lido = (await Sql().ObterAsync([e.Id])).Single();
        await using (var db = Banco.Contexto())
        {
            if (!await db.Municipios.AnyAsync(m => m.Id == 3120904)) // chave estrangeira do município (banco de teste vazio)
            {
                db.Municipios.Add(new Municipio { Id = 3120904, Nome = "Curvelo", NomeBusca = "CURVELO", Uf = "MG", CodigoUf = 31 });
                await db.SaveChangesAsync();
            }
            var q = db.PessoaEnderecos.Where(x => x.Id == e.Id);
            _ = campo switch
            {
                "Numero" => await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.Numero, "150")),
                "Bairro" => await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.Bairro, "Centro")),
                "MunicipioId" => await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.MunicipioId, 3120904)),
                _ => await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.CodigoMunicipioIbge, "3120904"))
            };
        }

        Assert.False(await Sql().GravarAsync(lido, new ConferenciaCepRealizada(CepSituacao.Conferido, CepFonte.ViaCep, Agora)));
        await using var leitura = Banco.Contexto();
        Assert.Equal(CepSituacao.NaoConferido, (await leitura.PessoaEnderecos.AsNoTracking().SingleAsync(x => x.Id == e.Id)).CepSituacao);
    }

    [FatoSqlServer]
    public async Task Valor_apagado_para_nulo_durante_a_reconferencia_bloqueia_a_gravacao()
    {
        var e = End("PB", CepSituacao.NaoConferido); // com número e bairro
        await PessoaAsync(SituacaoPessoa.Ativo, e);
        var lido = (await Sql().ObterAsync([e.Id])).Single();
        await using (var db = Banco.Contexto())
            await db.PessoaEnderecos.Where(x => x.Id == e.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Numero, (string?)null).SetProperty(x => x.Bairro, (string?)null));

        Assert.False(await Sql().GravarAsync(lido, new ConferenciaCepRealizada(CepSituacao.Conferido, CepFonte.ViaCep, Agora)));
    }

    // ---- G, ajuste 2.2: um evento de auditoria por execução, sem lista de pessoas ----

    [FatoSqlServer]
    public async Task Evento_da_execucao_vai_para_a_auditoria_uma_vez_so_sem_pessoas()
    {
        var execucao = Guid.NewGuid();
        var descricao = EventoReconferenciaCep.Descricao(false, 3, 2, 1, 0, 0, 0, 0, 75, true, true, false, false, "MG", null);
        int antes, enderecos;
        await using (var db = Banco.Contexto())
        {
            antes = await db.Auditoria.CountAsync();
            enderecos = await db.PessoaEnderecos.CountAsync();
        }

        var primeira = await Sql().RegistrarAsync(new ExecucaoReconferenciaCep(execucao, "maria", Agora, descricao));
        var repetida = await Sql().RegistrarAsync(new ExecucaoReconferenciaCep(execucao, "maria", Agora, descricao));

        Assert.True(primeira);
        Assert.False(repetida); // repetir a conclusão não duplica
        await using var leitura = Banco.Contexto();
        Assert.Equal(antes + 1, await leitura.Auditoria.CountAsync());
        var linha = await leitura.Auditoria.SingleAsync(a => a.RaizEntidade == EventoReconferenciaCep.Entidade && a.RaizId == execucao);
        Assert.Equal((AcaoAuditoria.Evento, "maria", Agora, execucao.ToString("N")), (linha.Acao, linha.Usuario, linha.DataHora, linha.RegistroId));
        Assert.Equal(descricao, linha.Descricao);
        Assert.Null(linha.Campo);
        Assert.Equal(enderecos, await leitura.PessoaEnderecos.CountAsync());
    }

    [FatoSqlServer]
    public async Task Indice_de_situacao_e_data_existe_no_modelo_criado()
    {
        await using var db = Banco.Contexto();
        var n = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.indexes WHERE object_id = OBJECT_ID('PessoaEnderecos') AND name = 'IX_PessoaEnderecos_CepSituacao_CepConferidoEm'")
            .ToListAsync();
        Assert.Equal(1, n.Single());
    }
}
