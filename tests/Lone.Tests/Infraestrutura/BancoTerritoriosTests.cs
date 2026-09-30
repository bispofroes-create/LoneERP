using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Domain.Territorios;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Banco temporário com o modelo atual e os gatilhos dos territórios (sobreposição dos responsáveis, árvore e posições), com
/// o mesmo texto e na mesma ordem que as migrações executam (um banco por classe de teste). Pulado sem LONE_TESTES_SQLSERVER.
/// Padrão do SQL Server: READ_COMMITTED_SNAPSHOT desligado (leitura com travas).
/// </summary>
public class BancoTerritorios : IAsyncLifetime
{
    public BancoDeTeste? Banco { get; private set; }

    /// <summary>Liga READ_COMMITTED_SNAPSHOT e ALLOW_SNAPSHOT_ISOLATION no banco temporário (o cenário mais exigente).</summary>
    protected virtual bool ComVersoesDeLinha => false;

    public async Task InitializeAsync()
    {
        if (FatoSqlServerAttribute.Conexao is null) return;
        Banco = await BancoDeTeste.CriarAsync(comProtecoes: false);
        await CriarProtecoesAsync(Banco);
        if (ComVersoesDeLinha) await LigarVersoesDeLinhaAsync(Banco);
    }

    /// <summary>
    /// As proteções dos territórios na ordem das migrações (2: responsáveis; 3: árvore e posições; 4: reforço; 2b-1b: motor,
    /// FKs de Exclusivo e gatilhos 50073–50076).
    /// </summary>
    public static async Task CriarProtecoesAsync(BancoDeTeste banco, bool comReforcoConcorrencia = true, bool comMotor = true)
    {
        await using var db = banco.Contexto();
        await db.Database.ExecuteSqlRawAsync(SqlMigracaoTerritorios.CriarProtecao);
        await db.Database.ExecuteSqlRawAsync(SqlMigracaoTerritorios.CriarProtecaoArvore);
        await db.Database.ExecuteSqlRawAsync(SqlMigracaoTerritorios.CriarProtecaoPosicoes);
        if (comReforcoConcorrencia) await db.Database.ExecuteSqlRawAsync(SqlMigracaoTerritorios.ReforcarProtecaoConcorrencia);
        if (!comMotor) return;
        await db.Database.ExecuteSqlRawAsync(SqlMigracaoTerritorios.PreencherMotores);
        await db.Database.ExecuteSqlRawAsync(SqlMigracaoTerritorios.CriarChavesExclusivo);
        await db.Database.ExecuteSqlRawAsync(SqlMigracaoTerritorios.CriarProtecaoRegras);
        await db.Database.ExecuteSqlRawAsync(SqlMigracaoTerritorios.CriarProtecaoExcecoes);
        await db.Database.ExecuteSqlRawAsync(SqlMigracaoTerritorios.CriarProtecaoAtribuicoes);
        await db.Database.ExecuteSqlRawAsync(SqlMigracaoTerritorios.CriarProtecaoItens);
    }

    /// <summary>READ_COMMITTED_SNAPSHOT e SNAPSHOT ligados só no banco temporário (nunca no banco do sistema).</summary>
    public static async Task LigarVersoesDeLinhaAsync(BancoDeTeste banco)
    {
        var nome = new SqlConnectionStringBuilder(banco.Conexao).InitialCatalog;
        await using (var sql = new SqlConnection(new SqlConnectionStringBuilder(banco.Conexao) { InitialCatalog = "master", Pooling = false }.ConnectionString))
        {
            await sql.OpenAsync();
            await using var comando = new SqlCommand($"""
                ALTER DATABASE [{nome}] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
                ALTER DATABASE [{nome}] SET ALLOW_SNAPSHOT_ISOLATION ON;
                """, sql);
            await comando.ExecuteNonQueryAsync();
        }
        SqlConnection.ClearAllPools(); // as conexões antigas do pool foram derrubadas pelo ROLLBACK IMMEDIATE
    }

    public async Task DisposeAsync()
    {
        if (Banco is not null)
        {
            SqlConnection.ClearAllPools();
            await Banco.DisposeAsync();
        }
    }
}

/// <summary>O mesmo banco temporário com READ_COMMITTED_SNAPSHOT e ALLOW_SNAPSHOT_ISOLATION ligados.</summary>
public sealed class BancoTerritoriosComVersoesDeLinha : BancoTerritorios
{
    protected override bool ComVersoesDeLinha => true;
}

/// <summary>
/// Integridade dos territórios garantida PELO BANCO (Fase 2b-1a) com os repositórios de verdade: serialização pela trava da
/// árvore (A→B e B→A simultâneos não formam ciclo; árvore velha é recusada; o cadastro do mapa não conflita com a árvore),
/// gatilhos de sobreposição dos responsáveis, de ciclo/níveis e de posições (também contra gravação feita direto no banco,
/// com várias linhas no mesmo comando e sem falso positivo), dois usuários incluindo responsáveis conflitantes ao mesmo tempo
/// (só um grava; o outro não deixa nada), conferência final da posição aberta e histórico preservado.
/// Pulados (nunca aprovados) sem LONE_TESTES_SQLSERVER. Todos rodam duas vezes: <see cref="BancoTerritoriosTests"/> (padrão do
/// SQL Server) e <see cref="BancoTerritoriosRcsiTests"/> (READ_COMMITTED_SNAPSHOT e SNAPSHOT ligados).
/// </summary>
public abstract partial class BancoTerritoriosTestesBase
{
    private readonly BancoTerritorios _fixture;

    protected BancoTerritoriosTestesBase(BancoTerritorios fixture) => _fixture = fixture;

    private protected LoneDbContext Db() => _fixture.Banco!.Contexto();

    private protected TerritorioRepositorio Repositorio() => new(new Fabrica(_fixture.Banco!), new UsuarioFixo());

    private protected MapaTerritorialRepositorio Mapas() => new(new Fabrica(_fixture.Banco!), new UsuarioFixo());

    private protected async Task<MapaTerritorial> MapaAsync()
    {
        await using var db = Db();
        var mapa = new MapaTerritorial
        {
            Id = IdSequencial.Novo(), Codigo = "M" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), Nome = "Mapa " + Guid.NewGuid().ToString("N")[..8],
            FinalidadeEnderecoReferenciaId = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Comercial)
        };
        db.MapasTerritoriais.Add(mapa);
        db.MapaTerritorialArvores.Add(new MapaTerritorialArvore { MapaId = mapa.Id, AtualizadoEm = DateTime.UtcNow });
        db.MapaTerritorialMotores.Add(new MapaTerritorialMotor { MapaId = mapa.Id, AtualizadoEm = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return mapa;
    }

    private protected static Territorio Novo(Guid mapaId, string nome, Guid? pai = null)
    {
        var t = new Territorio
        {
            Id = IdSequencial.Novo(), MapaId = mapaId, Codigo = RegrasCadastroTerritorial.NormalizarCodigo(nome), Nome = nome,
            TipoId = TiposTerritorioIniciais.Todos[0].Id, PaiId = pai
        };
        t.Posicoes.Add(new TerritorioPosicao { Id = IdSequencial.Novo(), MapaId = mapaId, TerritorioId = t.Id, PaiId = pai, InicioEm = new(2026, 1, 1) });
        return t;
    }

    private protected async Task<byte[]> VersaoDaArvoreAsync(Guid mapaId) => (await Repositorio().ObterVersaoArvoreAsync(mapaId, default))!;


    private protected static async Task<SqlException> Recusado(Func<Task> acao)
    {
        var erro = await Assert.ThrowsAnyAsync<Exception>(acao);
        return erro as SqlException ?? erro.InnerException as SqlException ?? throw new Xunit.Sdk.XunitException("Esperava erro do SQL Server: " + erro);
    }

    [FatoSqlServer]
    public async Task Pai_de_outro_mapa_e_recusado_pela_chave_composta()
    {
        var geografia = await MapaAsync();
        var segmentos = await MapaAsync();
        var mg = Novo(geografia.Id, "MG");
        await Repositorio().SalvarAsync(mg, novo: true, await VersaoDaArvoreAsync(geografia.Id), null, default);

        var erro = await Recusado(() => Repositorio().SalvarAsync(Novo(segmentos.Id, "Açougues", pai: mg.Id), novo: true, null, null, default));
        Assert.Equal(547, erro.Number);
    }

    [FatoSqlServer]
    public async Task Duas_posicoes_abertas_e_responsavel_com_pessoa_e_equipe_sao_recusados()
    {
        var mapa = await MapaAsync();
        var mg = Novo(mapa.Id, "MG");
        await Repositorio().SalvarAsync(mg, novo: true, null, null, default);

        await using (var db = Db())
        {
            var segundaAberta = await Recusado(() => db.Database.ExecuteSqlAsync($"""
                INSERT INTO TerritorioPosicoes (Id, MapaId, TerritorioId, PaiId, InicioEm, FimEm, Ativo, CriadoEm)
                VALUES ({Guid.NewGuid()}, {mapa.Id}, {mg.Id}, NULL, '2026-05-01', NULL, 1, SYSUTCDATETIME())
                """));
            Assert.Equal(2601, segundaAberta.Number);

            var periodoInvertido = await Recusado(() => db.Database.ExecuteSqlAsync($"""
                INSERT INTO TerritorioPosicoes (Id, MapaId, TerritorioId, PaiId, InicioEm, FimEm, Ativo, CriadoEm)
                VALUES ({Guid.NewGuid()}, {mapa.Id}, {mg.Id}, NULL, '2026-05-01', '2026-04-30', 0, SYSUTCDATETIME())
                """));
            Assert.Equal(547, periodoInvertido.Number);

            var encerradoSemData = await Recusado(() => db.Database.ExecuteSqlAsync($"UPDATE Territorios SET Situacao = 1 WHERE Id = {mg.Id}"));
            Assert.Equal(547, encerradoSemData.Number);
        }

        var pessoa = new Pessoa { Id = IdSequencial.Novo(), Nome = "João Teste", Natureza = Lone.Domain.Enums.NaturezaPessoa.Fisica };
        var equipe = new Equipe { Id = IdSequencial.Novo(), Nome = "Equipe " + Guid.NewGuid().ToString("N")[..8] };
        var funcao = new TipoCarteira { Id = IdSequencial.Novo(), Nome = "Função " + Guid.NewGuid().ToString("N")[..8] };
        await using (var db = Db())
        {
            db.Pessoas.Add(pessoa);
            db.Equipes.Add(equipe);
            db.TiposCarteira.Add(funcao);
            await db.SaveChangesAsync();
        }
        mg.Responsaveis.Add(new TerritorioResponsavel
        {
            Id = IdSequencial.Novo(), TerritorioId = mg.Id, PessoaId = pessoa.Id, EquipeId = equipe.Id, TipoCarteiraId = funcao.Id, InicioEm = new(2026, 2, 1)
        });
        var ambos = await Recusado(() => Repositorio().SalvarAsync(mg, novo: false, null, null, default));
        Assert.Equal(547, ambos.Number);
    }

    [FatoSqlServer]
    public async Task Nome_repetido_entre_irmaos_ativos_e_recusado_mas_vale_em_outro_pai()
    {
        var mapa = await MapaAsync();
        var bh = Novo(mapa.Id, "BH");
        var curvelo = Novo(mapa.Id, "Curvelo");
        await Repositorio().SalvarAsync(bh, novo: true, null, null, default);
        await Repositorio().SalvarAsync(curvelo, novo: true, null, null, default);
        await Repositorio().SalvarAsync(Novo(mapa.Id, "Centro", bh.Id), novo: true, null, null, default);
        await Repositorio().SalvarAsync(Renomear(Novo(mapa.Id, "Centro 2", curvelo.Id), "Centro"), novo: true, null, null, default); // outro pai: pode

        var repetido = Renomear(Novo(mapa.Id, "Centro 3", bh.Id), "centro"); // maiúscula não conta (collation)
        var erro = await Assert.ThrowsAsync<Lone.Domain.Validacao.ValidacaoException>(() => Repositorio().SalvarAsync(repetido, novo: true, null, null, default));
        Assert.Contains(erro.Erros, e => e.Contains("mesmo lugar da árvore"));
    }

    private static Territorio Renomear(Territorio t, string nome)
    {
        t.Nome = nome;
        return t;
    }

    [FatoSqlServer]
    public async Task A_abaixo_de_B_e_B_abaixo_de_A_com_a_mesma_versao_lida_nao_formam_ciclo()
    {
        var mapa = await MapaAsync();
        var a = Novo(mapa.Id, "A");
        var b = Novo(mapa.Id, "B");
        await Repositorio().SalvarAsync(a, novo: true, await VersaoDaArvoreAsync(mapa.Id), null, default);
        await Repositorio().SalvarAsync(b, novo: true, await VersaoDaArvoreAsync(mapa.Id), null, default);

        // As duas janelas leram a árvore (e a versão dela) antes de qualquer uma gravar; cada mudança, sozinha, é válida.
        var lida = await VersaoDaArvoreAsync(mapa.Id);
        var aAbaixoDeB = await CopiaAsync(a.Id);
        aAbaixoDeB.PaiId = b.Id;
        aAbaixoDeB.Posicoes.Single().PaiId = b.Id;
        var bAbaixoDeA = await CopiaAsync(b.Id);
        bAbaixoDeA.PaiId = a.Id;
        bAbaixoDeA.Posicoes.Single().PaiId = a.Id;

        var resultados = await Task.WhenAll(
            Tentar(() => Repositorio().SalvarAsync(aAbaixoDeB, novo: false, lida, null, default)),
            Tentar(() => Repositorio().SalvarAsync(bAbaixoDeA, novo: false, lida, null, default)));

        Assert.Equal(1, resultados.Count(r => r is null)); // exatamente uma gravou
        var conflito = Assert.Single(resultados.OfType<Exception>());
        Assert.Equal(RegrasArvoreTerritorial.MensagemArvoreAlterada, Assert.IsType<ConflitoDeEdicaoException>(conflito).Message);

        await using var db = Db();
        var arvore = await db.Territorios.AsNoTracking().Where(t => t.MapaId == mapa.Id).ToDictionaryAsync(t => t.Id, t => t.PaiId);
        Assert.False(arvore[a.Id] == b.Id && arvore[b.Id] == a.Id); // nunca as duas
        Assert.True(arvore[a.Id] is null || arvore[b.Id] is null);
        var posicoes = await db.TerritorioPosicoes.AsNoTracking().Where(p => p.MapaId == mapa.Id).ToDictionaryAsync(p => p.TerritorioId, p => p.PaiId);
        Assert.Equal(arvore[a.Id], posicoes[a.Id]); // a posição acompanhou só a mudança que valeu
        Assert.Equal(arvore[b.Id], posicoes[b.Id]);
    }

    // ------------------------------------------------------------------ Responsáveis: gatilho e concorrência

    private protected sealed record Cadastros(Guid P1, Guid P2, Guid E1, Guid E2, Guid F1, Guid F2);

    /// <summary>Duas pessoas, duas equipes e duas funções (papéis comerciais) novas.</summary>
    private protected async Task<Cadastros> CadastrosAsync()
    {
        string Sufixo() => Guid.NewGuid().ToString("N")[..8];
        var pessoas = new[] { new Pessoa { Id = IdSequencial.Novo(), Nome = "Pessoa " + Sufixo(), Natureza = Lone.Domain.Enums.NaturezaPessoa.Fisica },
                              new Pessoa { Id = IdSequencial.Novo(), Nome = "Pessoa " + Sufixo(), Natureza = Lone.Domain.Enums.NaturezaPessoa.Fisica } };
        var equipes = new[] { new Equipe { Id = IdSequencial.Novo(), Nome = "Equipe " + Sufixo() }, new Equipe { Id = IdSequencial.Novo(), Nome = "Equipe " + Sufixo() } };
        var funcoes = new[] { new TipoCarteira { Id = IdSequencial.Novo(), Nome = "Função " + Sufixo() }, new TipoCarteira { Id = IdSequencial.Novo(), Nome = "Função " + Sufixo() } };
        await using var db = Db();
        db.Pessoas.AddRange(pessoas);
        db.Equipes.AddRange(equipes);
        db.TiposCarteira.AddRange(funcoes);
        await db.SaveChangesAsync();
        return new Cadastros(pessoas[0].Id, pessoas[1].Id, equipes[0].Id, equipes[1].Id, funcoes[0].Id, funcoes[1].Id);
    }

    private async Task InserirResponsavelAsync(Guid territorio, Guid? pessoa, Guid? equipe, Guid funcao, string inicio, string? fim, bool ativo = true)
    {
        await using var db = Db();
        DateOnly? ate = fim is null ? null : DateOnly.Parse(fim, System.Globalization.CultureInfo.InvariantCulture);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO TerritorioResponsaveis (Id, TerritorioId, PessoaId, EquipeId, TipoCarteiraId, InicioEm, FimEm, Observacao, Ativo, CriadoEm)
            VALUES ({Guid.NewGuid()}, {territorio}, {pessoa}, {equipe}, {funcao}, {DateOnly.Parse(inicio, System.Globalization.CultureInfo.InvariantCulture)}, {ate}, NULL, {ativo}, SYSUTCDATETIME())
            """);
    }

    [FatoSqlServer]
    public async Task Gatilho_barra_sobreposicao_feita_direto_no_banco_sem_falso_positivo()
    {
        var mapa = await MapaAsync();
        var t1 = Novo(mapa.Id, "T1");
        var t2 = Novo(mapa.Id, "T2");
        await Repositorio().SalvarAsync(t1, novo: true, null, null, default);
        await Repositorio().SalvarAsync(t2, novo: true, null, null, default);
        var c = await CadastrosAsync();
        await InserirResponsavelAsync(t1.Id, c.P1, null, c.F1, "2026-01-01", "2026-06-30");

        // Mesmo território, função e pessoa: cruzar o período ou encostar no último dia é sobreposição.
        Assert.Equal(50070, (await Recusado(() => InserirResponsavelAsync(t1.Id, c.P1, null, c.F1, "2026-06-01", null))).Number);
        Assert.Equal(50070, (await Recusado(() => InserirResponsavelAsync(t1.Id, c.P1, null, c.F1, "2026-06-30", "2026-12-31"))).Number);

        // Sem falso positivo: dia seguinte, outra função, outro território, outra pessoa, equipe no lugar de pessoa.
        await InserirResponsavelAsync(t1.Id, c.P1, null, c.F1, "2026-07-01", "2026-12-31");
        await InserirResponsavelAsync(t1.Id, c.P1, null, c.F2, "2026-01-01", null);
        await InserirResponsavelAsync(t2.Id, c.P1, null, c.F1, "2026-01-01", null);
        await InserirResponsavelAsync(t1.Id, c.P2, null, c.F1, "2026-01-01", null);
        await InserirResponsavelAsync(t1.Id, null, c.E1, c.F1, "2026-01-01", null);
        await InserirResponsavelAsync(t1.Id, null, c.E2, c.F1, "2026-01-01", null);

        // Período aberto (fim nulo) cruza qualquer início posterior; equipe se compara com a mesma equipe.
        Assert.Equal(50070, (await Recusado(() => InserirResponsavelAsync(t1.Id, c.P2, null, c.F1, "2030-01-01", null))).Number);
        Assert.Equal(50070, (await Recusado(() => InserirResponsavelAsync(t1.Id, null, c.E1, c.F1, "2026-03-01", "2026-03-31"))).Number);

        // Anulado (Ativo = 0) não conta; reativá-lo por UPDATE direto também é barrado.
        await InserirResponsavelAsync(t1.Id, c.P1, null, c.F1, "2026-03-01", "2026-03-31", ativo: false);
        await using var db = Db();
        Assert.Equal(50070, (await Recusado(() => db.Database.ExecuteSqlAsync(
            $"UPDATE TerritorioResponsaveis SET Ativo = 1 WHERE TerritorioId = {t1.Id} AND Ativo = 0"))).Number);
    }

    [FatoSqlServer]
    public async Task Dois_usuarios_incluindo_responsaveis_conflitantes_so_um_grava_e_o_outro_nao_deixa_nada()
    {
        var mapa = await MapaAsync();
        var norte = Novo(mapa.Id, "Norte");
        await Repositorio().SalvarAsync(norte, novo: true, null, null, default);
        var c = await CadastrosAsync();

        // Os dois abriram o território na mesma versão; cada um renomeia e inclui a mesma pessoa na mesma função,
        // em períodos que se cruzam. Sozinha, cada gravação é válida.
        var deA = await CopiaAsync(norte.Id);
        var deB = await CopiaAsync(norte.Id);
        Assert.Equal(deA.Versao, deB.Versao);
        var responsavelA = new TerritorioResponsavel { Id = IdSequencial.Novo(), TerritorioId = norte.Id, PessoaId = c.P1, TipoCarteiraId = c.F1, InicioEm = new(2026, 1, 1) };
        var responsavelB = new TerritorioResponsavel { Id = IdSequencial.Novo(), TerritorioId = norte.Id, PessoaId = c.P1, TipoCarteiraId = c.F1, InicioEm = new(2026, 3, 1) };
        deA.Nome = "Norte de A";
        deA.Responsaveis.Add(responsavelA);
        deB.Nome = "Norte de B";
        deB.Responsaveis.Add(responsavelB);

        var resultados = await Task.WhenAll(
            Tentar(() => Repositorio().SalvarAsync(deA, novo: false, null, null, default)),
            Tentar(() => Repositorio().SalvarAsync(deB, novo: false, null, null, default)));

        Assert.Equal(1, resultados.Count(r => r is null)); // exatamente uma venceu
        var perdedora = Assert.IsType<ConflitoDeEdicaoException>(Assert.Single(resultados.OfType<Exception>()));
        Assert.Equal(ConflitoDeEdicaoException.MensagemPadrao, perdedora.Message); // recusada pela versão, antes de gravar

        await using var db = Db();
        var gravados = await db.TerritorioResponsaveis.AsNoTracking().Where(r => r.TerritorioId == norte.Id).ToListAsync();
        var vencedor = Assert.Single(gravados); // nada da perdedora ficou
        var nome = (await db.Territorios.AsNoTracking().SingleAsync(t => t.Id == norte.Id)).Nome;
        Assert.Equal(vencedor.Id == responsavelA.Id ? "Norte de A" : "Norte de B", nome); // o nome acompanha a vencedora
    }

    [FatoSqlServer]
    public async Task Versao_velha_e_recusada_e_quando_o_gatilho_barra_a_gravacao_inteira_volta()
    {
        var mapa = await MapaAsync();
        var sul = Novo(mapa.Id, "Sul");
        await Repositorio().SalvarAsync(sul, novo: true, null, null, default);
        var c = await CadastrosAsync();
        var aberta = await CopiaAsync(sul.Id);

        // Outra janela grava antes (renomeia): a versão aberta ficou velha.
        var outra = await CopiaAsync(sul.Id);
        outra.Nome = "Sul (renomeado)";
        await Repositorio().SalvarAsync(outra, novo: false, null, null, default);
        aberta.Responsaveis.Add(new TerritorioResponsavel { Id = IdSequencial.Novo(), TerritorioId = sul.Id, PessoaId = c.P1, TipoCarteiraId = c.F1, InicioEm = new(2026, 1, 1) });
        await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => Repositorio().SalvarAsync(aberta, novo: false, null, null, default));

        // Com a versão certa, mas com sobreposição que só o banco vê (gravada por fora): o gatilho barra e NADA fica,
        // nem a mudança de nome que ia junto.
        await InserirResponsavelAsync(sul.Id, c.P1, null, c.F1, "2026-01-01", null);
        var atual = await CopiaAsync(sul.Id);
        var versaoAntes = atual.Versao;
        atual.Nome = "Sul (não deve ficar)";
        atual.Responsaveis.Add(new TerritorioResponsavel { Id = IdSequencial.Novo(), TerritorioId = sul.Id, PessoaId = c.P1, TipoCarteiraId = c.F1, InicioEm = new(2026, 5, 1) });
        var barrada = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => Repositorio().SalvarAsync(atual, novo: false, null, null, default));
        Assert.Equal(TerritorioRepositorio.ResponsavelSobreposto, barrada.Message);

        await using var db = Db();
        var gravado = await db.Territorios.AsNoTracking().Include(t => t.Responsaveis).SingleAsync(t => t.Id == sul.Id);
        Assert.Equal("Sul (renomeado)", gravado.Nome);
        Assert.Equal(versaoAntes, gravado.Versao); // nem a trava do território ficou
        Assert.Single(gravado.Responsaveis);
    }

    [FatoSqlServer]
    public async Task Reorganizar_dois_periodos_da_mesma_pessoa_numa_gravacao_nao_da_falso_positivo()
    {
        var mapa = await MapaAsync();
        var leste = Novo(mapa.Id, "Leste");
        var c = await CadastrosAsync();
        leste.Responsaveis.Add(new TerritorioResponsavel { Id = IdSequencial.Novo(), PessoaId = c.P1, TipoCarteiraId = c.F1, InicioEm = new(2027, 1, 1), FimEm = new(2027, 1, 31) });
        leste.Responsaveis.Add(new TerritorioResponsavel { Id = IdSequencial.Novo(), PessoaId = c.P1, TipoCarteiraId = c.F1, InicioEm = new(2027, 2, 1) });
        await Repositorio().SalvarAsync(leste, novo: true, null, null, default);

        // O primeiro cresce até 15/02 e o segundo passa a começar em 16/02: o estado final não tem sobreposição, mas gravar o
        // primeiro antes do segundo teria. Os dois passos (interseção antes × depois) evitam o falso positivo.
        var copia = await CopiaAsync(leste.Id);
        copia.Responsaveis.Single(r => r.FimEm is not null).FimEm = new DateOnly(2027, 2, 15);
        copia.Responsaveis.Single(r => r.FimEm is null).InicioEm = new DateOnly(2027, 2, 16);
        await Repositorio().SalvarAsync(copia, novo: false, null, null, default);

        await using var db = Db();
        var periodos = await db.TerritorioResponsaveis.AsNoTracking().Where(r => r.TerritorioId == leste.Id).OrderBy(r => r.InicioEm)
            .Select(r => new { r.InicioEm, r.FimEm }).ToListAsync();
        Assert.Equal(new DateOnly(2027, 2, 15), periodos[0].FimEm);
        Assert.Equal(new DateOnly(2027, 2, 16), periodos[1].InicioEm);
    }

    // ------------------------------------------------------------------ Várias linhas no mesmo comando

    [FatoSqlServer]
    public async Task Gatilho_confere_varias_linhas_no_mesmo_insert_e_no_mesmo_update()
    {
        var mapa = await MapaAsync();
        var oeste = Novo(mapa.Id, "Oeste");
        await Repositorio().SalvarAsync(oeste, novo: true, null, null, default);
        var c = await CadastrosAsync();
        await using var db = Db();

        // Duas linhas novas que se cruzam entre si, num INSERT só: recusado, e nenhuma das duas fica.
        var cruzadas = await Recusado(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO TerritorioResponsaveis (Id, TerritorioId, PessoaId, EquipeId, TipoCarteiraId, InicioEm, FimEm, Observacao, Ativo, CriadoEm)
            VALUES ({Guid.NewGuid()}, {oeste.Id}, {c.P1}, NULL, {c.F1}, '2026-01-01', '2026-06-30', NULL, 1, SYSUTCDATETIME()),
                   ({Guid.NewGuid()}, {oeste.Id}, {c.P1}, NULL, {c.F1}, '2026-06-30', NULL, NULL, 1, SYSUTCDATETIME())
            """));
        Assert.Equal(50070, cruzadas.Number);
        Assert.Equal(0, await db.TerritorioResponsaveis.CountAsync(r => r.TerritorioId == oeste.Id));

        // Várias linhas válidas num INSERT só (períodos em sequência, outra função, outra pessoa, equipe): sem falso positivo.
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO TerritorioResponsaveis (Id, TerritorioId, PessoaId, EquipeId, TipoCarteiraId, InicioEm, FimEm, Observacao, Ativo, CriadoEm)
            VALUES ({Guid.NewGuid()}, {oeste.Id}, {c.P1}, NULL, {c.F1}, '2026-01-01', '2026-06-30', NULL, 1, SYSUTCDATETIME()),
                   ({Guid.NewGuid()}, {oeste.Id}, {c.P1}, NULL, {c.F1}, '2026-07-01', NULL, NULL, 1, SYSUTCDATETIME()),
                   ({Guid.NewGuid()}, {oeste.Id}, {c.P1}, NULL, {c.F2}, '2026-01-01', NULL, NULL, 1, SYSUTCDATETIME()),
                   ({Guid.NewGuid()}, {oeste.Id}, {c.P2}, NULL, {c.F1}, '2026-01-01', NULL, NULL, 1, SYSUTCDATETIME()),
                   ({Guid.NewGuid()}, {oeste.Id}, NULL, {c.E1}, {c.F1}, '2026-01-01', NULL, NULL, 1, SYSUTCDATETIME())
            """);
        Assert.Equal(5, await db.TerritorioResponsaveis.CountAsync(r => r.TerritorioId == oeste.Id));

        // Um UPDATE que mexe em várias linhas e faz duas se cruzarem: recusado, e nenhuma linha muda.
        var antes = await db.TerritorioResponsaveis.AsNoTracking().Where(r => r.TerritorioId == oeste.Id)
            .OrderBy(r => r.Id).Select(r => new { r.Id, r.InicioEm, r.FimEm }).ToListAsync();
        var atualizacao = await Recusado(() => db.Database.ExecuteSqlAsync(
            $"UPDATE TerritorioResponsaveis SET InicioEm = DATEADD(day, -1, InicioEm) WHERE TerritorioId = {oeste.Id} AND PessoaId = {c.P1} AND TipoCarteiraId = {c.F1}"));
        Assert.Equal(50070, atualizacao.Number);
        var depois = await db.TerritorioResponsaveis.AsNoTracking().Where(r => r.TerritorioId == oeste.Id)
            .OrderBy(r => r.Id).Select(r => new { r.Id, r.InicioEm, r.FimEm }).ToListAsync();
        Assert.Equal(antes, depois);
    }

    // ------------------------------------------------------------------ Árvore e posições no banco

    [FatoSqlServer]
    public async Task Ciclo_e_mais_de_doze_niveis_feitos_direto_no_banco_sao_recusados()
    {
        var mapa = await MapaAsync();
        var niveis = new List<Territorio>();
        Guid? pai = null;
        for (var i = 1; i <= 12; i++) // 12 níveis: o limite, aceito
        {
            var t = Novo(mapa.Id, $"N{i:00}", pai);
            await Repositorio().SalvarAsync(t, novo: true, null, null, default);
            niveis.Add(t);
            pai = t.Id;
        }
        await using var db = Db();

        // Ciclo: o primeiro abaixo do último (e um território abaixo dele mesmo).
        Assert.Equal(50071, (await Recusado(() => db.Database.ExecuteSqlAsync(
            $"UPDATE Territorios SET PaiId = {niveis[^1].Id} WHERE Id = {niveis[0].Id}"))).Number);
        Assert.Equal(50071, (await Recusado(() => db.Database.ExecuteSqlAsync(
            $"UPDATE Territorios SET PaiId = Id WHERE Id = {niveis[5].Id}"))).Number);

        // 13º nível, incluído direto: recusado. Mover uma subárvore para baixo que passaria de 12: recusado.
        Assert.Equal(50071, (await Recusado(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO Territorios (Id, MapaId, Codigo, Nome, TipoId, PaiId, Situacao, CriadoEm)
            VALUES ({Guid.NewGuid()}, {mapa.Id}, 'N13', 'N13', {TiposTerritorioIniciais.Todos[0].Id}, {niveis[^1].Id}, 0, SYSUTCDATETIME())
            """))).Number);
        var outraRaiz = Novo(mapa.Id, "Outra raiz");
        var filho = Novo(mapa.Id, "Filho da outra", outraRaiz.Id);
        await Repositorio().SalvarAsync(outraRaiz, novo: true, null, null, default);
        await Repositorio().SalvarAsync(filho, novo: true, null, null, default);
        Assert.Equal(50071, (await Recusado(() => db.Database.ExecuteSqlAsync(
            $"UPDATE Territorios SET PaiId = {niveis[10].Id} WHERE Id = {outraRaiz.Id}"))).Number); // 11 + 1 + 1 = 13

        // Sem falso positivo: mover para um lugar válido e renomear (sem mexer no pai) passam.
        await db.Database.ExecuteSqlAsync($"UPDATE Territorios SET PaiId = {niveis[9].Id} WHERE Id = {outraRaiz.Id}"); // 10 + 1 + 1 = 12
        await db.Database.ExecuteSqlAsync($"UPDATE Territorios SET Nome = Nome + ' (renomeado)' WHERE MapaId = {mapa.Id}");
        var arvore = await db.Territorios.AsNoTracking().Where(t => t.MapaId == mapa.Id).ToDictionaryAsync(t => t.Id, t => t.PaiId);
        Assert.Null(arvore[niveis[0].Id]);
        Assert.Equal(niveis[9].Id, arvore[outraRaiz.Id]);
    }

    [FatoSqlServer]
    public async Task Posicoes_do_mesmo_territorio_que_se_cruzam_sao_recusadas_direto_no_banco()
    {
        var mapa = await MapaAsync();
        var centro = Novo(mapa.Id, "Centro");
        await Repositorio().SalvarAsync(centro, novo: true, null, null, default); // posição aberta desde 01/01/2026
        await using var db = Db();
        var aberta = centro.Posicoes.Single().Id;
        await db.Database.ExecuteSqlAsync($"UPDATE TerritorioPosicoes SET FimEm = '2026-06-30' WHERE Id = {aberta}");

        // Encerrada que encosta no último dia da outra: recusada. Anulada no mesmo período: aceita (não conta).
        Assert.Equal(50072, (await Recusado(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO TerritorioPosicoes (Id, MapaId, TerritorioId, PaiId, InicioEm, FimEm, Ativo, CriadoEm)
            VALUES ({Guid.NewGuid()}, {mapa.Id}, {centro.Id}, NULL, '2026-06-30', '2026-08-31', 1, SYSUTCDATETIME())
            """))).Number);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO TerritorioPosicoes (Id, MapaId, TerritorioId, PaiId, InicioEm, FimEm, Ativo, CriadoEm)
            VALUES ({Guid.NewGuid()}, {mapa.Id}, {centro.Id}, NULL, '2026-03-01', '2026-03-31', 0, SYSUTCDATETIME())
            """);

        // Dia seguinte em diante: aceita (histórico em sequência). Reabrir a primeira por UPDATE: recusado.
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO TerritorioPosicoes (Id, MapaId, TerritorioId, PaiId, InicioEm, FimEm, Ativo, CriadoEm)
            VALUES ({Guid.NewGuid()}, {mapa.Id}, {centro.Id}, NULL, '2026-07-01', NULL, 1, SYSUTCDATETIME())
            """);
        Assert.Equal(50072, (await Recusado(() => db.Database.ExecuteSqlAsync(
            $"UPDATE TerritorioPosicoes SET FimEm = '2026-07-15' WHERE Id = {aberta}"))).Number);
        Assert.Equal(3, await db.TerritorioPosicoes.CountAsync(p => p.TerritorioId == centro.Id));
    }

    // ------------------------------------------------------------------ Trava da árvore (D1 = B)

    [FatoSqlServer]
    public async Task Arvore_velha_e_recusada_e_nada_e_gravado()
    {
        var mapa = await MapaAsync();
        var vista = await VersaoDaArvoreAsync(mapa.Id); // a tela carregou a árvore
        await Repositorio().SalvarAsync(Novo(mapa.Id, "Norte"), novo: true, vista, null, default); // outra janela mudou a árvore

        var sul = Novo(mapa.Id, "Sul");
        var conflito = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => Repositorio().SalvarAsync(sul, novo: true, vista, null, default));

        Assert.Equal(RegrasArvoreTerritorial.MensagemArvoreAlterada, conflito.Message);
        await using var db = Db();
        Assert.False(await db.Territorios.AnyAsync(t => t.Id == sul.Id));
        Assert.False(await db.TerritorioPosicoes.AnyAsync(p => p.TerritorioId == sul.Id));
    }

    [FatoSqlServer]
    public async Task Cadastro_do_mapa_e_arvore_nao_conflitam_mas_desativar_o_mapa_invalida_a_arvore_vista()
    {
        var mapa = await MapaAsync();

        // A ficha do mapa foi aberta; enquanto isso, a árvore mudou. Salvar o cadastro continua valendo (sem aviso falso).
        var ficha = await Mapas().ObterAsync(mapa.Id, default);
        await Repositorio().SalvarAsync(Novo(mapa.Id, "Leste"), novo: true, await VersaoDaArvoreAsync(mapa.Id), null, default);
        ficha!.Descricao = "Regiões de venda";
        await Mapas().SalvarAsync(ficha, novo: false, default);

        // E o contrário: a árvore vista antes de salvar o cadastro continua valendo (a descrição não mexe na árvore)...
        var vista = await VersaoDaArvoreAsync(mapa.Id);
        var ficha2 = await Mapas().ObterAsync(mapa.Id, default);
        ficha2!.Nome += " (revisado)";
        await Mapas().SalvarAsync(ficha2, novo: false, default);
        Assert.Equal(vista, await VersaoDaArvoreAsync(mapa.Id));

        // ... mas desativar o mapa troca a versão da árvore: uma mudança conferida com o mapa ainda ativo não grava depois.
        var ficha3 = await Mapas().ObterAsync(mapa.Id, default);
        ficha3!.Ativo = false;
        await Mapas().SalvarAsync(ficha3, novo: false, default);
        var oeste = Novo(mapa.Id, "Oeste");
        await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => Repositorio().SalvarAsync(oeste, novo: true, vista, null, default));

        // Ficha do mapa velha: mensagem própria, nada gravado.
        ficha!.Descricao = "Não deve ficar";
        var velha = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => Mapas().SalvarAsync(ficha, novo: false, default));
        Assert.Equal(RegrasMapaTerritorial.MensagemMapaAlterado, velha.Message);
        Assert.Equal("Regiões de venda", (await Mapas().ObterAsync(mapa.Id, default))!.Descricao);
    }

    [FatoSqlServer]
    public async Task Mapa_novo_nasce_com_a_trava_da_arvore()
    {
        var mapa = new MapaTerritorial
        {
            Id = IdSequencial.Novo(), Codigo = "M" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), Nome = "Mapa " + Guid.NewGuid().ToString("N")[..8],
            FinalidadeEnderecoReferenciaId = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Comercial)
        };
        await Mapas().SalvarAsync(mapa, novo: true, default);

        var vista = await VersaoDaArvoreAsync(mapa.Id);
        Assert.NotNull(vista);
        await Repositorio().SalvarAsync(Novo(mapa.Id, "Primeiro"), novo: true, vista, null, default);
    }

    // ------------------------------------------------------------------ Conferência final e histórico

    [FatoSqlServer]
    public async Task Pai_e_posicao_aberta_que_nao_conferem_nao_gravam()
    {
        var mapa = await MapaAsync();
        var a = Novo(mapa.Id, "A");
        var b = Novo(mapa.Id, "B");
        await Repositorio().SalvarAsync(a, novo: true, null, null, default);
        await Repositorio().SalvarAsync(b, novo: true, null, null, default);

        // Um caminho que muda o pai sem mexer na posição (o domínio nunca faz isso): a gravação inteira volta.
        var errado = await CopiaAsync(b.Id);
        errado.PaiId = a.Id;
        errado.Nome = "B (não deve ficar)";
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => Repositorio().SalvarAsync(errado, novo: false, null, null, default));
        Assert.Equal(TerritorioRepositorio.MensagemPosicaoIncoerente, erro.Message);

        await using var db = Db();
        var gravado = await db.Territorios.AsNoTracking().SingleAsync(t => t.Id == b.Id);
        Assert.Null(gravado.PaiId);
        Assert.Equal("B", gravado.Nome);
    }

    [FatoSqlServer]
    public async Task Historico_de_responsaveis_e_posicoes_continua_depois_de_trocar_e_encerrar()
    {
        var mapa = await MapaAsync();
        var norte = Novo(mapa.Id, "Norte");
        var c = await CadastrosAsync();
        norte.Responsaveis.Add(new TerritorioResponsavel { Id = IdSequencial.Novo(), PessoaId = c.P1, TipoCarteiraId = c.F1, InicioEm = new(2026, 1, 1), FimEm = new(2026, 6, 30) });
        norte.Responsaveis.Add(new TerritorioResponsavel { Id = IdSequencial.Novo(), PessoaId = c.P2, TipoCarteiraId = c.F1, InicioEm = new(2026, 7, 1) });
        await Repositorio().SalvarAsync(norte, novo: true, null, null, default);

        // Encerrar (como o serviço faz): nada é apagado; quem estava termina no último dia.
        var encerrar = await CopiaAsync(norte.Id);
        RegrasArvoreTerritorial.Encerrar(encerrar, new DateOnly(2026, 9, 29));
        await Repositorio().SalvarAsync(encerrar, novo: false, await VersaoDaArvoreAsync(mapa.Id), null, default);

        await using var db = Db();
        var responsaveis = await db.TerritorioResponsaveis.AsNoTracking().Where(r => r.TerritorioId == norte.Id).ToListAsync();
        Assert.Equal(2, responsaveis.Count);
        Assert.All(responsaveis, r => Assert.True(r.Ativo));
        DateOnly Data(int a, int m, int d) => new(a, m, d);
        Assert.Equal(c.P1, responsaveis.Single(r => r.VigenteEm(Data(2026, 3, 15))).PessoaId); // quem era em março
        Assert.Equal(c.P2, responsaveis.Single(r => r.VigenteEm(Data(2026, 9, 29))).PessoaId); // quem era no último dia
        Assert.DoesNotContain(responsaveis, r => r.VigenteEm(Data(2026, 9, 30)));
        var posicao = await db.TerritorioPosicoes.AsNoTracking().SingleAsync(p => p.TerritorioId == norte.Id);
        Assert.Equal(Data(2026, 9, 29), posicao.FimEm);
        Assert.True(await db.Auditoria.AnyAsync(a => a.RaizId == norte.Id)); // e a auditoria registrou
    }

    private protected async Task<Territorio> CopiaAsync(Guid id)
    {
        await using var db = Db();
        return await db.Territorios.AsNoTracking().Include(t => t.Posicoes).Include(t => t.Responsaveis).SingleAsync(t => t.Id == id);
    }

    private static async Task<Exception?> Tentar(Func<Task> acao)
    {
        try
        {
            await acao();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private sealed class Fabrica(BancoDeTeste banco) : IDbContextFactory<LoneDbContext>
    {
        public LoneDbContext CreateDbContext() => banco.Contexto();
    }

    private sealed class UsuarioFixo : IUsuarioAtual
    {
        public Guid? Id => Guid.Empty;
        public string Nome => "Teste";
    }
}
