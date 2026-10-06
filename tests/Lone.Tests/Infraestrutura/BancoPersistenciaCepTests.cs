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
/// F3 no SQL Server: cache postal (CacheCep) e histórico (ConsultasCep) de verdade, com concorrência, e as três colunas de
/// conferência gravadas no endereço pelo repositório. Banco temporário por classe; pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class BancoPersistenciaCepTests : IClassFixture<BancoComAuditoriaProtegida>
{
    private static readonly DateOnly Hoje = new(2026, 10, 5);
    private static readonly DateTime Agora = new(2026, 10, 5, 20, 30, 0, DateTimeKind.Utc);
    private readonly BancoComAuditoriaProtegida _fixture;

    public BancoPersistenciaCepTests(BancoComAuditoriaProtegida fixture) => _fixture = fixture;

    private BancoDeTeste Banco => _fixture.Banco!;
    private CachePostalCepSql Cache() => new(new MotorTerritorialTeste.Fabrica(Banco), NullLogger<CachePostalCepSql>.Instance);
    private HistoricoConsultasCepSql Historico() => new(new MotorTerritorialTeste.Fabrica(Banco), NullLogger<HistoricoConsultasCepSql>.Instance);

    private static RespostaGuardadaCep Resposta(string logradouro = "Avenida Paulista", bool limite = false) => new(
        SituacaoCachePostal.Encontrado, CepFonte.ViaCep,
        [new RegistroCep("01310911", logradouro, "960", "Bela Vista", "São Paulo", "SP", "3550308", "Edifício Paulicéia")],
        limite, Agora, Agora.AddHours(24), Agora.AddDays(30));

    [FatoSqlServer]
    public async Task Cache_guarda_e_le_com_fonte_original_datas_limite_e_unidade()
    {
        var chave = "cep:01310911-" + Guid.NewGuid().ToString("N")[..6];

        await Cache().GuardarAsync(chave, TipoConsultaCep.PorCep, "01310911", Resposta(limite: true));
        var lida = await Cache().ObterAsync(chave);

        Assert.NotNull(lida);
        Assert.Equal((SituacaoCachePostal.Encontrado, CepFonte.ViaCep, Agora, Agora.AddHours(24), (DateTime?)Agora.AddDays(30), true),
            (lida.Situacao, lida.Fonte, lida.ConsultadoEm, lida.ExpiraEm, lida.UtilizavelAte, lida.LimiteAtingido));
        Assert.Equal(Resposta().Registros, lida.Registros); // dados postais, com unidade, sem nada da pessoa
    }

    [FatoSqlServer]
    public async Task Gravar_a_mesma_chave_de_novo_substitui_sem_duplicar()
    {
        var chave = "cep:subst-" + Guid.NewGuid().ToString("N")[..6];

        await Cache().GuardarAsync(chave, TipoConsultaCep.PorCep, "01310911", Resposta());
        await Cache().GuardarAsync(chave, TipoConsultaCep.PorCep, "01310911", Resposta(logradouro: "Av. Paulista (novo)"));

        await using var db = Banco.Contexto();
        Assert.Equal(1, await db.CacheCep.CountAsync(c => c.Chave == chave));
        Assert.Equal("Av. Paulista (novo)", (await Cache().ObterAsync(chave))!.Registros[0].Logradouro);
    }

    [FatoSqlServer]
    public async Task Gravacoes_simultaneas_da_mesma_chave_nao_duplicam_nem_falham()
    {
        var chave = "cep:conc-" + Guid.NewGuid().ToString("N")[..6];
        var cache = Cache();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            Task.Run(() => cache.GuardarAsync(chave, TipoConsultaCep.PorCep, "01310911", Resposta(logradouro: $"Rua {i}")))));

        await using var db = Banco.Contexto();
        Assert.Equal(1, await db.CacheCep.CountAsync(c => c.Chave == chave));
        Assert.StartsWith("Rua ", (await cache.ObterAsync(chave))!.Registros[0].Logradouro);
    }

    [FatoSqlServer]
    public async Task Historico_grava_a_linha_tecnica_minima()
    {
        var chave = "cep:hist-" + Guid.NewGuid().ToString("N")[..6];

        await Historico().RegistrarAsync(new ConsultaCepOcorrida(OperacaoConsultaCep.Conferencia, TipoConsultaCep.PorCep, chave, "01310100",
            CepFonte.ViaCep, Agora, TimeSpan.FromMilliseconds(512), ResultadoConsultaCep.Encontrado, OrigemRespostaCep.Fonte));

        await using var db = Banco.Contexto();
        var linha = await db.ConsultasCep.SingleAsync(c => c.Chave == chave);
        Assert.Equal((OperacaoConsultaCep.Conferencia, "01310100", (CepFonte?)CepFonte.ViaCep, Agora, 512, ResultadoConsultaCep.Encontrado),
            (linha.Operacao, linha.Cep, linha.Fonte, linha.OcorridoEm, linha.DuracaoMs, linha.Resultado));
    }

    [FatoSqlServer]
    public async Task Cache_e_historico_nao_entram_na_auditoria_de_cadastro()
    {
        await using var antes = Banco.Contexto();
        var linhas = await antes.Auditoria.CountAsync();

        await Cache().GuardarAsync("cep:aud-" + Guid.NewGuid().ToString("N")[..6], TipoConsultaCep.PorCep, "01310911", Resposta());
        await Historico().RegistrarAsync(new ConsultaCepOcorrida(OperacaoConsultaCep.ConsultaDireta, TipoConsultaCep.PorCep, "cep:x", "01310911",
            CepFonte.ViaCep, Agora, TimeSpan.Zero, ResultadoConsultaCep.NaoEncontrado, OrigemRespostaCep.Fonte));

        await using var depois = Banco.Contexto();
        Assert.Equal(linhas, await depois.Auditoria.CountAsync());
    }

    [FatoSqlServer]
    public async Task Endereco_grava_e_le_a_situacao_a_fonte_e_a_data_da_conferencia()
    {
        var id = Guid.NewGuid();
        var enderecoId = Guid.NewGuid();
        var p = new Pessoa { Id = id, Nome = "F3 " + id.ToString("N")[..6], Natureza = NaturezaPessoa.Juridica };
        p.Enderecos.Add(new PessoaEndereco
        {
            Id = enderecoId, PessoaId = id, Cep = "35790000", Logradouro = "Rua Barão", Numero = "150", Bairro = "Centro", Cidade = "Curvelo", Uf = "MG"
        });
        var repo = new PessoaRepositorio(new MotorTerritorialTeste.Fabrica(Banco), new MotorTerritorialTeste.UsuarioTeste(),
            new MotorTerritorialTeste.EscopoTudo(Hoje));
        await repo.SalvarAsync(p, nova: true, OrigemAlteracao.Usuario, CancellationToken.None);

        var novo = (await repo.ObterAsync(id, CancellationToken.None))!;
        Assert.Equal((CepSituacao.NaoConferido, (CepFonte?)null, (DateTime?)null),
            (novo.Enderecos[0].CepSituacao, novo.Enderecos[0].CepFonte, novo.Enderecos[0].CepConferidoEm)); // nada presumido

        novo.Enderecos[0].CepSituacao = CepSituacao.Conferido;
        novo.Enderecos[0].CepFonte = CepFonte.ViaCep;
        novo.Enderecos[0].CepConferidoEm = Agora;
        await repo.SalvarAsync(novo, nova: false, OrigemAlteracao.Usuario, CancellationToken.None);

        var lido = (await repo.ObterAsync(id, CancellationToken.None))!.Enderecos.Single();
        Assert.Equal((CepSituacao.Conferido, (CepFonte?)CepFonte.ViaCep, (DateTime?)Agora), (lido.CepSituacao, lido.CepFonte, lido.CepConferidoEm));
    }
}

/// <summary>F3: o banco do cache ou do histórico falhar não muda a consulta (sem SQL Server: a fábrica falha).</summary>
public class FalhaBancoPersistenciaCepTests
{
    private sealed class FabricaQuebrada : IDbContextFactory<LoneDbContext>
    {
        public LoneDbContext CreateDbContext() => throw new InvalidOperationException("banco fora do ar");
    }

    [Fact]
    public async Task Cache_e_historico_com_banco_fora_do_ar_nao_lancam()
    {
        var cache = new CachePostalCepSql(new FabricaQuebrada(), NullLogger<CachePostalCepSql>.Instance);
        var historico = new HistoricoConsultasCepSql(new FabricaQuebrada(), NullLogger<HistoricoConsultasCepSql>.Instance);
        var resposta = new RespostaGuardadaCep(SituacaoCachePostal.Encontrado, CepFonte.ViaCep, [], false, DateTime.UtcNow, DateTime.UtcNow, null);

        Assert.Null(await cache.ObterAsync("cep:35790000"));
        await cache.GuardarAsync("cep:35790000", TipoConsultaCep.PorCep, "35790000", resposta);
        await historico.RegistrarAsync(new ConsultaCepOcorrida(OperacaoConsultaCep.Conferencia, TipoConsultaCep.PorCep, "cep:35790000", "35790000",
            CepFonte.ViaCep, DateTime.UtcNow, TimeSpan.Zero, ResultadoConsultaCep.Encontrado, OrigemRespostaCep.Fonte));
    }

    [Fact]
    public async Task Consulta_valida_continua_valida_quando_o_banco_de_cache_falha()
    {
        var quebrado = new CachePostalCepSql(new FabricaQuebrada(), NullLogger<CachePostalCepSql>.Instance);
        var historico = new HistoricoConsultasCepSql(new FabricaQuebrada(), NullLogger<HistoricoConsultasCepSql>.Instance);
        var relogio = TimeProvider.System;
        var fonte = new FonteFixa();
        var servico = new ServicoConferenciaCep([fonte], new CacheConferenciaCep(relogio), new SugestoesCepEmitidas(relogio),
            new ConferenciasCepEmitidas(relogio), relogio, quebrado, historico);

        var r = await servico.ConferirAsync(new EnderecoConferenciaCep("35790-000", "R. Barao", "150", "Centro", "Curvelo", "MG", "3120904"));

        Assert.Equal(ResultadoDecisaoCep.Conferido, r.Decisao.Resultado); // não vira "fonte indisponível"
    }

    private sealed class FonteFixa : IProvedorCep
    {
        public CepFonte Fonte => CepFonte.ViaCep;
        public bool BuscaPorEndereco => false;
        public Task<ResultadoProvedorCep> ConsultarPorCepAsync(string cep, CancellationToken ct = default) =>
            Task.FromResult(ResultadoProvedorCep.Encontrado(CepFonte.ViaCep,
                new RegistroCep("35790000", "Rua Barão", "até 999/1000", "Centro", "Curvelo", "MG", "3120904")));
        public Task<ResultadoBuscaProvedorCep> BuscarPorEnderecoAsync(BuscaEnderecoCep busca, CancellationToken ct = default) =>
            throw new InvalidOperationException();
    }
}
