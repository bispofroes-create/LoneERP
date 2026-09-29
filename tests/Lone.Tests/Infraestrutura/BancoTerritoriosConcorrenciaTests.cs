using System.Data;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Auditoria final da Fase 2b-1a (29/09/2026): as invariantes dos territórios provadas no SQL Server de verdade, escrevendo
/// DIRETO no banco (sem domínio nem aplicação) e com DUAS TRANSAÇÕES SIMULTÂNEAS de verdade (duas conexões): a primeira
/// grava e ainda não confirmou; a segunda tenta a gravação conflitante. O banco precisa fazer a segunda esperar e recusá-la
/// depois que a primeira confirma — nunca duas verdades incompatíveis gravadas. Também: várias linhas no mesmo comando na
/// árvore e nas posições, e a versão da árvore (D1) mudando só com mudança de estrutura.
/// </summary>
public abstract partial class BancoTerritoriosTestesBase
{
    // ------------------------------------------------------------------ Apoio: SQL direto e duas transações

    private protected async Task<SqlConnection> AbrirConexaoAsync()
    {
        var conexao = new SqlConnection(_fixture.Banco!.Conexao);
        await conexao.OpenAsync();
        return conexao;
    }

    private protected static async Task ExecutarAsync(SqlConnection conexao, SqlTransaction? transacao, string sql)
    {
        await using var comando = new SqlCommand(sql, conexao, transacao) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync();
    }

    private protected async Task ExecutarNoBancoAsync(string sql)
    {
        await using var conexao = await AbrirConexaoAsync();
        await ExecutarAsync(conexao, null, sql);
    }

    private protected static int? NumeroDoErro(Exception? erro) => (erro as SqlException ?? erro?.InnerException as SqlException)?.Number;

    private static string Data(string? data) => data is null ? "NULL" : $"'{data}'";

    /// <summary>INSERT direto de um responsável (pessoa) — sem passar pelo domínio.</summary>
    private protected static string InsertResponsavel(Guid territorio, Guid pessoa, Guid funcao, string inicio, string? fim) => $"""
        INSERT INTO TerritorioResponsaveis (Id, TerritorioId, PessoaId, EquipeId, TipoCarteiraId, InicioEm, FimEm, Observacao, Ativo, CriadoEm)
        VALUES ('{Guid.NewGuid()}', '{territorio}', '{pessoa}', NULL, '{funcao}', {Data(inicio)}, {Data(fim)}, NULL, 1, SYSUTCDATETIME())
        """;

    private static string InsertPosicao(Guid mapa, Guid territorio, string inicio, string? fim) => $"""
        INSERT INTO TerritorioPosicoes (Id, MapaId, TerritorioId, PaiId, InicioEm, FimEm, Ativo, CriadoEm)
        VALUES ('{Guid.NewGuid()}', '{mapa}', '{territorio}', NULL, {Data(inicio)}, {Data(fim)}, 1, SYSUTCDATETIME())
        """;

    /// <summary>
    /// Transação A grava <paramref name="sqlA"/> e fica aberta; a transação B tenta <paramref name="sqlB"/>. Espera até 2 s
    /// para ver se B ficou presa atrás de A, então A confirma e B termina (confirma, se o banco deixar). Devolve se B
    /// esperou A e o erro de B (nulo = B confirmou).
    /// </summary>
    private protected async Task<(bool Esperou, Exception? Erro)> DisputaAsync(string sqlA, string sqlB,
                                                                              IsolationLevel isolamentoB = IsolationLevel.ReadCommitted)
    {
        await using var a = await AbrirConexaoAsync();
        await using var b = await AbrirConexaoAsync();
        var ta = (SqlTransaction)await a.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await ExecutarAsync(a, ta, sqlA);

        var tb = (SqlTransaction)await b.BeginTransactionAsync(isolamentoB);
        var gravacaoB = ExecutarAsync(b, tb, sqlB);
        var esperou = await Task.WhenAny(gravacaoB, Task.Delay(TimeSpan.FromSeconds(2))) != gravacaoB;
        await ta.CommitAsync();

        Exception? erro = null;
        try
        {
            await gravacaoB;
            await tb.CommitAsync();
        }
        catch (Exception ex)
        {
            erro = ex;
            try { tb.Rollback(); } catch (Exception) { /* o gatilho já desfez a transação */ }
        }
        return (esperou, erro);
    }

    private protected async Task<int> ResponsaveisAtivosAsync(Guid territorio, Guid pessoa)
    {
        await using var db = Db();
        return await db.TerritorioResponsaveis.AsNoTracking().CountAsync(r => r.TerritorioId == territorio && r.PessoaId == pessoa && r.Ativo);
    }

    private protected async Task<(Territorio Territorio, Cadastros Cadastros)> TerritorioComCadastrosAsync(string nome)
    {
        var mapa = await MapaAsync();
        var territorio = Novo(mapa.Id, nome);
        await Repositorio().SalvarAsync(territorio, novo: true, null, default);
        return (territorio, await CadastrosAsync());
    }

    // ------------------------------------------------------------------ 50070: duas transações simultâneas

    [FatoSqlServer]
    public async Task Duas_transacoes_diretas_incluindo_o_mesmo_responsavel_so_uma_confirma()
    {
        var (t, c) = await TerritorioComCadastrosAsync("Disputa INSERT");

        var (esperou, erro) = await DisputaAsync(
            InsertResponsavel(t.Id, c.P1, c.F1, "2026-01-01", null),
            InsertResponsavel(t.Id, c.P1, c.F1, "2026-03-01", "2026-12-31"));

        Assert.True(esperou); // B ficou presa atrás de A (a leitura do gatilho respeita a trava)...
        Assert.Equal(50070, NumeroDoErro(erro)); // ... e, quando A confirmou, B viu A e foi barrada
        Assert.Equal(1, await ResponsaveisAtivosAsync(t.Id, c.P1)); // uma verdade só
    }

    [FatoSqlServer]
    public async Task Update_de_uma_transacao_e_insert_de_outra_que_se_cruzam_so_um_confirma()
    {
        var (t, c) = await TerritorioComCadastrosAsync("Disputa UPDATE");
        await ExecutarNoBancoAsync(InsertResponsavel(t.Id, c.P1, c.F1, "2026-01-01", "2026-01-31"));

        // A estende o período (fica em aberto); B inclui a mesma pessoa na mesma função a partir de junho.
        var (esperou, erro) = await DisputaAsync(
            $"UPDATE TerritorioResponsaveis SET FimEm = NULL WHERE TerritorioId = '{t.Id}' AND PessoaId = '{c.P1}'",
            InsertResponsavel(t.Id, c.P1, c.F1, "2026-06-01", null));

        Assert.True(esperou);
        Assert.Equal(50070, NumeroDoErro(erro));
        Assert.Equal(1, await ResponsaveisAtivosAsync(t.Id, c.P1));
        await using var db = Db();
        Assert.Null((await db.TerritorioResponsaveis.AsNoTracking().SingleAsync(r => r.TerritorioId == t.Id)).FimEm); // valeu a de A
    }

    [FatoSqlServer]
    public async Task Transacoes_simultaneas_sem_conflito_nao_se_barram()
    {
        var (t, c) = await TerritorioComCadastrosAsync("Sem conflito");

        // Pessoas diferentes; e a mesma pessoa em outra função: sem falso positivo, mesmo em transações simultâneas.
        var (_, outraPessoa) = await DisputaAsync(
            InsertResponsavel(t.Id, c.P1, c.F1, "2026-01-01", null),
            InsertResponsavel(t.Id, c.P2, c.F1, "2026-01-01", null));
        var (_, outraFuncao) = await DisputaAsync(
            InsertResponsavel(t.Id, c.P1, c.F2, "2026-01-01", null),
            InsertResponsavel(t.Id, c.P2, c.F2, "2026-01-01", null));

        Assert.Null(outraPessoa);
        Assert.Null(outraFuncao);
        Assert.Equal(2, await ResponsaveisAtivosAsync(t.Id, c.P1));
        Assert.Equal(2, await ResponsaveisAtivosAsync(t.Id, c.P2));
    }

    // ------------------------------------------------------------------ 50071 e 50072: transações simultâneas

    [FatoSqlServer]
    public async Task Duas_transacoes_diretas_A_abaixo_de_B_e_B_abaixo_de_A_nao_formam_ciclo()
    {
        var mapa = await MapaAsync();
        var a = Novo(mapa.Id, "A");
        var b = Novo(mapa.Id, "B");
        await Repositorio().SalvarAsync(a, novo: true, null, default);
        await Repositorio().SalvarAsync(b, novo: true, null, default);

        var (esperou, erro) = await DisputaAsync(
            $"UPDATE Territorios SET PaiId = '{b.Id}' WHERE Id = '{a.Id}'",
            $"UPDATE Territorios SET PaiId = '{a.Id}' WHERE Id = '{b.Id}'");

        Assert.True(esperou);
        Assert.Equal(50071, NumeroDoErro(erro));
        await using var db = Db();
        var arvore = await db.Territorios.AsNoTracking().Where(t => t.MapaId == mapa.Id).ToDictionaryAsync(t => t.Id, t => t.PaiId);
        Assert.Equal(b.Id, arvore[a.Id]); // valeu só a de A
        Assert.Null(arvore[b.Id]);
    }

    [FatoSqlServer]
    public async Task Duas_transacoes_diretas_com_posicoes_que_se_cruzam_so_uma_confirma()
    {
        var mapa = await MapaAsync();
        var centro = Novo(mapa.Id, "Centro");
        await Repositorio().SalvarAsync(centro, novo: true, null, default); // aberta desde 01/01/2026
        await ExecutarNoBancoAsync($"UPDATE TerritorioPosicoes SET FimEm = '2026-06-30' WHERE TerritorioId = '{centro.Id}'");

        var (esperou, erro) = await DisputaAsync(
            InsertPosicao(mapa.Id, centro.Id, "2026-07-01", "2026-12-31"),
            InsertPosicao(mapa.Id, centro.Id, "2026-10-01", "2026-10-31"));

        Assert.True(esperou);
        Assert.Equal(50072, NumeroDoErro(erro));
        await using var db = Db();
        Assert.Equal(2, await db.TerritorioPosicoes.CountAsync(p => p.TerritorioId == centro.Id));
    }

    // ------------------------------------------------------------------ Várias linhas no mesmo comando

    [FatoSqlServer]
    public async Task Arvore_confere_varias_linhas_no_mesmo_comando()
    {
        var mapa = await MapaAsync();
        var a = Novo(mapa.Id, "A");
        var b = Novo(mapa.Id, "B");
        await Repositorio().SalvarAsync(a, novo: true, null, default);
        await Repositorio().SalvarAsync(b, novo: true, null, default);
        await using var db = Db();

        // Um UPDATE só que põe A abaixo de B e B abaixo de A: ciclo; recusado e nada muda.
        var troca = await Recusado(() => db.Database.ExecuteSqlAsync(
            $"UPDATE Territorios SET PaiId = CASE WHEN Id = {a.Id} THEN {b.Id} ELSE {a.Id} END WHERE Id IN ({a.Id}, {b.Id})"));
        Assert.Equal(50071, troca.Number);
        Assert.Equal(0, await db.Territorios.CountAsync(t => t.MapaId == mapa.Id && t.PaiId != null));

        // Um INSERT só com uma corrente de 12 níveis: aceito (o limite). Com 13: recusado inteiro.
        string Corrente(int niveis, string prefixo)
        {
            var ids = Enumerable.Range(0, niveis).Select(_ => Guid.NewGuid()).ToList();
            var linhas = ids.Select((id, i) =>
                $"('{id}', '{mapa.Id}', '{prefixo}{i + 1:00}', '{prefixo}{i + 1:00}', '{TiposTerritorioIniciais.Todos[0].Id}', {(i == 0 ? "NULL" : $"'{ids[i - 1]}'")}, 0, SYSUTCDATETIME())");
            return "INSERT INTO Territorios (Id, MapaId, Codigo, Nome, TipoId, PaiId, Situacao, CriadoEm) VALUES " + string.Join(",\n", linhas);
        }
        await db.Database.ExecuteSqlRawAsync(Corrente(12, "D"));
        Assert.Equal(12, await db.Territorios.CountAsync(t => t.MapaId == mapa.Id && t.Codigo.StartsWith("D")));
        var treze = await Recusado(() => db.Database.ExecuteSqlRawAsync(Corrente(13, "T")));
        Assert.Equal(50071, treze.Number);
        Assert.Equal(0, await db.Territorios.CountAsync(t => t.MapaId == mapa.Id && t.Codigo.StartsWith("T")));
    }

    [FatoSqlServer]
    public async Task Posicoes_conferem_varias_linhas_no_mesmo_insert()
    {
        var mapa = await MapaAsync();
        var centro = Novo(mapa.Id, "Centro");
        await Repositorio().SalvarAsync(centro, novo: true, null, default);
        await using var db = Db();
        await db.Database.ExecuteSqlAsync($"UPDATE TerritorioPosicoes SET FimEm = '2026-03-31' WHERE TerritorioId = {centro.Id}");

        // Duas linhas novas que se cruzam entre si (encostam em 30/06): recusadas as duas.
        var cruzadas = await Recusado(() => db.Database.ExecuteSqlRawAsync(
            InsertPosicao(mapa.Id, centro.Id, "2026-04-01", "2026-06-30") + ",\n" +
            $"('{Guid.NewGuid()}', '{mapa.Id}', '{centro.Id}', NULL, '2026-06-30', '2026-09-30', 1, SYSUTCDATETIME())"));
        Assert.Equal(50072, cruzadas.Number);
        Assert.Equal(1, await db.TerritorioPosicoes.CountAsync(p => p.TerritorioId == centro.Id));

        // Em sequência (01/04–30/06 e 01/07 em diante): aceitas.
        await db.Database.ExecuteSqlRawAsync(
            InsertPosicao(mapa.Id, centro.Id, "2026-04-01", "2026-06-30") + ",\n" +
            $"('{Guid.NewGuid()}', '{mapa.Id}', '{centro.Id}', NULL, '2026-07-01', NULL, 1, SYSUTCDATETIME())");
        Assert.Equal(3, await db.TerritorioPosicoes.CountAsync(p => p.TerritorioId == centro.Id));
    }

    // ------------------------------------------------------------------ Versão da árvore (D1)

    [FatoSqlServer]
    public async Task Versao_da_arvore_muda_so_com_mudanca_de_estrutura()
    {
        var mapa = await MapaAsync();
        var c = await CadastrosAsync();
        var v0 = await VersaoDaArvoreAsync(mapa.Id);

        // Criar: muda.
        var norte = Novo(mapa.Id, "Norte");
        var sul = Novo(mapa.Id, "Sul");
        await Repositorio().SalvarAsync(norte, novo: true, v0, default);
        var v1 = await VersaoDaArvoreAsync(mapa.Id);
        Assert.NotEqual(v0, v1);
        await Repositorio().SalvarAsync(sul, novo: true, v1, default);
        var v2 = await VersaoDaArvoreAsync(mapa.Id);

        // Nome do território e responsáveis: não são estrutura, a versão não muda (sem conflito falso de árvore).
        var ficha = await CopiaAsync(norte.Id);
        ficha.Nome = "Norte (renomeado)";
        ficha.Responsaveis.Add(new TerritorioResponsavel { Id = IdSequencial.Novo(), PessoaId = c.P1, TipoCarteiraId = c.F1, InicioEm = new(2026, 2, 1) });
        await Repositorio().SalvarAsync(ficha, novo: false, null, default);
        Assert.Equal(v2, await VersaoDaArvoreAsync(mapa.Id));

        // Cadastro do mapa (descrição): versão cadastral do mapa, não da árvore.
        var mapaFicha = (await Mapas().ObterAsync(mapa.Id, default))!;
        mapaFicha.Descricao = "Revisado";
        await Mapas().SalvarAsync(mapaFicha, novo: false, default);
        Assert.Equal(v2, await VersaoDaArvoreAsync(mapa.Id));

        // Mover: muda.
        var mover = await CopiaAsync(sul.Id);
        mover.PaiId = norte.Id;
        mover.Posicoes.Single().PaiId = norte.Id;
        await Repositorio().SalvarAsync(mover, novo: false, v2, default);
        var v3 = await VersaoDaArvoreAsync(mapa.Id);
        Assert.NotEqual(v2, v3);

        // Encerrar: muda.
        var encerrar = await CopiaAsync(sul.Id);
        RegrasArvoreTerritorial.Encerrar(encerrar, new DateOnly(2026, 9, 29));
        await Repositorio().SalvarAsync(encerrar, novo: false, v3, default);
        var v4 = await VersaoDaArvoreAsync(mapa.Id);
        Assert.NotEqual(v3, v4);

        // Reativar com a árvore velha: recusado, nada gravado, versão igual. Com a atual: reabre a posição e muda.
        var reativarVelha = await CopiaAsync(sul.Id);
        RegrasArvoreTerritorial.Reativar(reativarVelha);
        var conflito = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => Repositorio().SalvarAsync(reativarVelha, novo: false, v3, default));
        Assert.Equal(RegrasArvoreTerritorial.MensagemArvoreAlterada, conflito.Message);
        await using (var db = Db())
            Assert.Equal(SituacaoTerritorio.Encerrado, (await db.Territorios.AsNoTracking().SingleAsync(t => t.Id == sul.Id)).Situacao);
        Assert.Equal(v4, await VersaoDaArvoreAsync(mapa.Id));

        var reativar = await CopiaAsync(sul.Id);
        RegrasArvoreTerritorial.Reativar(reativar);
        await Repositorio().SalvarAsync(reativar, novo: false, v4, default);
        var v5 = await VersaoDaArvoreAsync(mapa.Id);
        Assert.NotEqual(v4, v5);
        await using (var db = Db())
            Assert.Null((await db.TerritorioPosicoes.AsNoTracking().SingleAsync(p => p.TerritorioId == sul.Id)).FimEm);

        // Desativar e reativar o mapa: mudam (mapa desativado não aceita mudança de estrutura).
        mapaFicha = (await Mapas().ObterAsync(mapa.Id, default))!;
        mapaFicha.Ativo = false;
        await Mapas().SalvarAsync(mapaFicha, novo: false, default);
        var v6 = await VersaoDaArvoreAsync(mapa.Id);
        Assert.NotEqual(v5, v6);
        mapaFicha = (await Mapas().ObterAsync(mapa.Id, default))!;
        mapaFicha.Ativo = true;
        await Mapas().SalvarAsync(mapaFicha, novo: false, default);
        Assert.NotEqual(v6, await VersaoDaArvoreAsync(mapa.Id));
    }

    [FatoSqlServer]
    public async Task Usuario_A_com_a_arvore_velha_nao_grava_nada_da_ficha()
    {
        var mapa = await MapaAsync();
        var a = Novo(mapa.Id, "A");
        var b = Novo(mapa.Id, "B");
        await Repositorio().SalvarAsync(a, novo: true, null, default);
        await Repositorio().SalvarAsync(b, novo: true, null, default);

        var vistaPorA = await VersaoDaArvoreAsync(mapa.Id);                                   // A abre a árvore ("versão 10")
        await Repositorio().SalvarAsync(Novo(mapa.Id, "C"), novo: true, vistaPorA, default); // B muda a árvore ("versão 11")

        var deA = await CopiaAsync(b.Id); // A move B para baixo de A e renomeia, olhando a versão 10
        var versaoDoTerritorio = deA.Versao;
        deA.PaiId = a.Id;
        deA.Posicoes.Single().PaiId = a.Id;
        deA.Nome = "B (não deve ficar)";
        var conflito = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => Repositorio().SalvarAsync(deA, novo: false, vistaPorA, default));

        Assert.Equal(RegrasArvoreTerritorial.MensagemArvoreAlterada, conflito.Message);
        await using var db = Db();
        var gravado = await db.Territorios.AsNoTracking().Include(t => t.Posicoes).SingleAsync(t => t.Id == b.Id);
        Assert.Null(gravado.PaiId);
        Assert.Equal("B", gravado.Nome);
        Assert.Equal(versaoDoTerritorio, gravado.Versao);
        Assert.Null(gravado.Posicoes.Single().PaiId);
        Assert.Equal(3, await db.Territorios.CountAsync(t => t.MapaId == mapa.Id)); // A, B e o C do usuário B
    }
}

/// <summary>Os testes de banco dos territórios no padrão do SQL Server (READ_COMMITTED_SNAPSHOT desligado).</summary>
public sealed class BancoTerritoriosTests : BancoTerritoriosTestesBase, IClassFixture<BancoTerritorios>
{
    public BancoTerritoriosTests(BancoTerritorios fixture) : base(fixture) { }
}

/// <summary>
/// Os mesmos testes com READ_COMMITTED_SNAPSHOT e ALLOW_SNAPSHOT_ISOLATION ligados (o cenário mais exigente: a leitura comum
/// não espera travas), mais a demonstração de por que o gatilho 50070 precisou do reforço e a transação SNAPSHOT feita por
/// fora.
/// </summary>
public sealed class BancoTerritoriosRcsiTests : BancoTerritoriosTestesBase, IClassFixture<BancoTerritoriosComVersoesDeLinha>
{
    public BancoTerritoriosRcsiTests(BancoTerritoriosComVersoesDeLinha fixture) : base(fixture) { }

    [FatoSqlServer]
    public async Task Sem_o_reforco_o_gatilho_da_migration_2_deixa_duas_verdades_e_com_o_reforco_nao()
    {
        var (t, c) = await TerritorioComCadastrosAsync("Demonstração");

        // O gatilho exatamente como a migration Fase2b1ResponsaveisSemSobreposicao o criou (é o Down do reforço).
        await ExecutarNoBancoAsync(SqlMigracaoTerritorios.DesfazerReforcoProtecaoConcorrencia);
        try
        {
            var (esperouSemReforco, erroSemReforco) = await DisputaAsync(
                InsertResponsavel(t.Id, c.P1, c.F1, "2026-01-01", null),
                InsertResponsavel(t.Id, c.P1, c.F1, "2026-03-01", null));

            Assert.False(esperouSemReforco); // B leu a versão confirmada, sem esperar A...
            Assert.Null(erroSemReforco);     // ... não viu A e confirmou:
            Assert.Equal(2, await ResponsaveisAtivosAsync(t.Id, c.P1)); // DUAS verdades — a violação era real
        }
        finally
        {
            await ExecutarNoBancoAsync(SqlMigracaoTerritorios.ReforcarProtecaoConcorrencia);
        }

        // Com o reforço (migration Fase2b1ResponsaveisConcorrencia), a mesma disputa com outra pessoa: B espera e é barrada.
        var (esperou, erro) = await DisputaAsync(
            InsertResponsavel(t.Id, c.P2, c.F1, "2026-01-01", null),
            InsertResponsavel(t.Id, c.P2, c.F1, "2026-03-01", null));
        Assert.True(esperou);
        Assert.Equal(50070, NumeroDoErro(erro));
        Assert.Equal(1, await ResponsaveisAtivosAsync(t.Id, c.P2));
    }

    [FatoSqlServer]
    public async Task Transacao_SNAPSHOT_feita_por_fora_tambem_nao_grava_duas_verdades()
    {
        var (t, c) = await TerritorioComCadastrosAsync("SNAPSHOT");

        var (esperou, erro) = await DisputaAsync(
            InsertResponsavel(t.Id, c.P1, c.F1, "2026-01-01", null),
            InsertResponsavel(t.Id, c.P1, c.F1, "2026-03-01", null),
            IsolationLevel.Snapshot);

        Assert.True(esperou);
        Assert.Equal(50070, NumeroDoErro(erro));
        Assert.Equal(1, await ResponsaveisAtivosAsync(t.Id, c.P1));
    }
}
