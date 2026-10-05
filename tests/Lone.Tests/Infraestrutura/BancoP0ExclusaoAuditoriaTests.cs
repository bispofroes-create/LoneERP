using Lone.Contracts.Pessoas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Papeis;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Consultas;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Tests.Infraestrutura;

/// <summary>Banco temporário com o modelo atual e o gatilho da Auditoria do P0 (um por classe de teste).</summary>
public sealed class BancoComAuditoriaProtegida : IAsyncLifetime
{
    public BancoDeTeste? Banco { get; private set; }

    public async Task InitializeAsync()
    {
        if (FatoSqlServerAttribute.Conexao is null) return;
        Banco = await BancoDeTeste.CriarAsync(comProtecoes: false);
        await using var db = Banco.Contexto();
        foreach (var lote in BancoDeTeste.Lotes(SqlMigracaoAuditoria.CriarProtecao))
            await db.Database.ExecuteSqlRawAsync(lote);
    }

    public async Task DisposeAsync()
    {
        if (Banco is not null) await Banco.DisposeAsync();
    }
}

/// <summary>
/// P0 no SQL Server (revisão 2.2): o gatilho da Auditoria provado pelo comportamento (EF grava, INSERT passa, UPDATE e
/// DELETE falham), a gravação da ficha que não apaga contatos, contas e sócios ausentes (D8 e D7), e U1–U7 com o mesmo
/// resultado de antes para conta de papel inativo (D8). Pulados sem LONE_TESTES_SQLSERVER.
/// </summary>
public class BancoP0ExclusaoAuditoriaTests : IClassFixture<BancoComAuditoriaProtegida>
{
    private static readonly DateOnly Hoje = new(2026, 10, 5);
    private readonly BancoComAuditoriaProtegida _fixture;

    public BancoP0ExclusaoAuditoriaTests(BancoComAuditoriaProtegida fixture) => _fixture = fixture;

    private BancoDeTeste Banco => _fixture.Banco!;
    private LoneDbContext Db() => Banco.Contexto();

    private PessoaRepositorio Repositorio() =>
        new(new MotorTerritorialTeste.Fabrica(Banco), new MotorTerritorialTeste.UsuarioTeste(), new MotorTerritorialTeste.EscopoTudo(Hoje));

    private static RegistroAuditoria Linha(string texto) => new()
    {
        DataHora = DateTime.UtcNow, Usuario = "teste", OperacaoId = Guid.NewGuid(), Origem = OrigemAlteracao.Sistema,
        Entidade = "Teste", RegistroId = Guid.NewGuid().ToString(), RaizEntidade = "Teste", Acao = AcaoAuditoria.Evento, Descricao = texto
    };

    // ---- Gatilho TR_Auditoria_SomenteInclusao (D4) ----

    [FatoSqlServer]
    public async Task Com_o_gatilho_o_EF_grava_a_pessoa_e_a_auditoria_normalmente()
    {
        var id = Guid.NewGuid();
        await using (var db = Db())
        {
            db.Pessoas.Add(new Pessoa { Id = id, Nome = "Gatilho " + id.ToString("N")[..6], Natureza = NaturezaPessoa.Fisica });
            await db.SaveChangesAsync(); // ColetorAuditoria → INSERT na Auditoria com o gatilho ativo
        }
        await using var leitura = Db();
        Assert.True(await leitura.Auditoria.AnyAsync(a => a.RaizId == id && a.Acao == AcaoAuditoria.Inclusao));
    }

    [FatoSqlServer]
    public async Task Insert_pelo_EF_e_por_SQL_passam_e_update_e_delete_falham()
    {
        var texto = "P0 " + Guid.NewGuid().ToString("N");
        await using (var db = Db())
        {
            db.Auditoria.Add(Linha(texto)); // caminho db.Auditoria.Add (ConsultaPessoas, OperacoesTerritoriais)
            await db.SaveChangesAsync();
        }
        await using var sql = Db();
        await sql.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Auditoria (DataHora, Usuario, OperacaoId, Origem, Entidade, RegistroId, RaizEntidade, Acao, Descricao)
            VALUES (SYSUTCDATETIME(), 'teste', {Guid.NewGuid()}, 2, 'Teste', 'sql', 'Teste', 3, {texto + " sql"})
            """); // caminho INSERT INTO Auditoria (scripts de migração de etiquetas e profissões)

        var update = await Assert.ThrowsAsync<SqlException>(() =>
            sql.Database.ExecuteSqlInterpolatedAsync($"UPDATE Auditoria SET Descricao = 'alterado' WHERE Descricao = {texto}"));
        Assert.Equal(SqlMigracaoAuditoria.ErroSomenteInclusao, update.Number);

        var delete = await Assert.ThrowsAsync<SqlException>(() =>
            sql.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Auditoria WHERE Descricao LIKE {texto + "%"}"));
        Assert.Equal(SqlMigracaoAuditoria.ErroSomenteInclusao, delete.Number);

        await using var leitura = Db();
        Assert.Equal(2, await leitura.Auditoria.CountAsync(a => a.Descricao != null && a.Descricao.StartsWith(texto)));
        Assert.True(await leitura.Auditoria.AnyAsync(a => a.Descricao == texto)); // o UPDATE não mudou nada
    }

    // ---- Gravação da ficha: contatos, contas e sócios ausentes não são apagados ----

    private async Task<Guid> EmpresaComTudoAsync()
    {
        var id = Guid.NewGuid();
        var empresaDoGrupo = Guid.NewGuid();
        await using (var db = Db())
        {
            db.Pessoas.Add(new Pessoa { Id = empresaDoGrupo, Nome = "Grupo " + id.ToString("N")[..6], Natureza = NaturezaPessoa.Juridica });
            await db.SaveChangesAsync();
        }
        var p = new Pessoa { Id = id, Nome = "P0 " + id.ToString("N")[..6], Natureza = NaturezaPessoa.Juridica };
        p.Contatos.Add(new Contato { Id = Guid.NewGuid(), PessoaId = id, Nome = "Maria Souza", Cargo = "Compradora", Principal = true });
        p.ContasCliente.Add(new ContaCliente { Id = Guid.NewGuid(), PessoaId = id, LimiteCredito = 5000m });
        p.ContasCliente.Add(new ContaCliente { Id = Guid.NewGuid(), PessoaId = id, EmpresaId = empresaDoGrupo, LimiteCredito = 900m });
        p.ContasFornecedor.Add(new ContaFornecedor { Id = Guid.NewGuid(), PessoaId = id, PrazoMedioDias = 28 });
        p.Socios.Add(new PessoaSocio { Id = Guid.NewGuid(), PessoaId = id, Nome = "João", Qualificacao = "Sócio" });
        await Repositorio().SalvarAsync(p, nova: true, OrigemAlteracao.Usuario, CancellationToken.None);
        return id;
    }

    [FatoSqlServer]
    public async Task Gravar_sem_contatos_contas_e_socios_no_dto_nao_apaga_nenhum()
    {
        var id = await EmpresaComTudoAsync();
        var repo = Repositorio();
        var dados = (await repo.ObterAsync(id, CancellationToken.None))!;
        dados.Contatos.Clear();
        dados.ContasCliente.Clear();
        dados.ContasFornecedor.Clear();
        dados.Socios.Clear();

        await repo.SalvarAsync(dados, nova: false, OrigemAlteracao.Usuario, CancellationToken.None);

        await using var db = Db();
        Assert.Equal(1, await db.Contatos.CountAsync(c => c.PessoaId == id && c.Ativo));
        Assert.Equal(2, await db.ContasCliente.CountAsync(c => c.PessoaId == id));               // geral e por empresa
        Assert.Equal(1, await db.ContasFornecedor.CountAsync(c => c.PessoaId == id));
        Assert.Equal(1, await db.PessoaSocios.CountAsync(s => s.PessoaId == id && s.Ativo));
        Assert.False(await db.Auditoria.AnyAsync(a => a.RaizId == id && a.Acao == AcaoAuditoria.Exclusao));
    }

    [FatoSqlServer]
    public async Task Inativar_contato_e_socio_grava_no_mesmo_registro_e_registra_a_acao_propria()
    {
        var id = await EmpresaComTudoAsync();
        var repo = Repositorio();
        var dados = (await repo.ObterAsync(id, CancellationToken.None))!;
        var contato = dados.Contatos.Single();
        var socio = dados.Socios.Single();
        contato.Ativo = false;
        contato.Principal = false;
        socio.Ativo = false;
        socio.SaiuEm = Hoje;

        await repo.SalvarAsync(dados, nova: false, OrigemAlteracao.Usuario, CancellationToken.None);

        await using var db = Db();
        var gravado = await db.Contatos.AsNoTracking().SingleAsync(c => c.PessoaId == id);
        Assert.Equal((contato.Id, false), (gravado.Id, gravado.Ativo));
        var ex = await db.PessoaSocios.AsNoTracking().SingleAsync(s => s.PessoaId == id);
        Assert.Equal((socio.Id, false, (DateOnly?)Hoje), (ex.Id, ex.Ativo, ex.SaiuEm));
        var acoes = await db.Auditoria.AsNoTracking().Where(a => a.RaizId == id && a.Acao == AcaoAuditoria.Inativacao).ToListAsync();
        Assert.Contains(acoes, a => a.Entidade == nameof(Contato) && a.Descricao == "Maria Souza (Compradora)");
        Assert.Contains(acoes, a => a.Entidade == nameof(PessoaSocio) && a.Descricao == "João (Sócio)");
        Assert.False(await db.Auditoria.AnyAsync(a => a.RaizId == id && a.Acao == AcaoAuditoria.Exclusao));
    }

    [FatoSqlServer]
    public async Task Contato_incluido_ja_inativo_fica_inativo_no_banco()
    {
        // Padrão 1 da coluna com sentinela "true": o EF manda o falso; o padrão do banco nunca o transforma em ativo.
        var id = Guid.NewGuid();
        var p = new Pessoa { Id = id, Nome = "Sentinela " + id.ToString("N")[..6], Natureza = NaturezaPessoa.Fisica };
        p.Contatos.Add(new Contato { Id = Guid.NewGuid(), PessoaId = id, Nome = "Inativo", Ativo = false });
        p.Contatos.Add(new Contato { Id = Guid.NewGuid(), PessoaId = id, Nome = "Ativo" });
        await Repositorio().SalvarAsync(p, nova: true, OrigemAlteracao.Usuario, CancellationToken.None);

        await using var db = Db();
        var gravados = await db.Contatos.AsNoTracking().Where(c => c.PessoaId == id).ToDictionaryAsync(c => c.Nome, c => c.Ativo);
        Assert.False(gravados["Inativo"]);
        Assert.True(gravados["Ativo"]);
    }

    [FatoSqlServer]
    public async Task Reativar_socio_volta_o_mesmo_registro_sem_a_data_de_saida()
    {
        var id = await EmpresaComTudoAsync();
        var repo = Repositorio();
        var dados = (await repo.ObterAsync(id, CancellationToken.None))!;
        dados.Socios[0].Ativo = false;
        dados.Socios[0].SaiuEm = new DateOnly(2026, 1, 10);
        await repo.SalvarAsync(dados, nova: false, OrigemAlteracao.Usuario, CancellationToken.None);

        var denovo = (await repo.ObterAsync(id, CancellationToken.None))!;
        denovo.Socios[0].Ativo = true;
        denovo.Socios[0].SaiuEm = null;
        await repo.SalvarAsync(denovo, nova: false, OrigemAlteracao.ConsultaExterna, CancellationToken.None);

        await using var db = Db();
        var s = await db.PessoaSocios.AsNoTracking().SingleAsync(x => x.PessoaId == id);
        Assert.Equal((dados.Socios[0].Id, true, (DateOnly?)null), (s.Id, s.Ativo, s.SaiuEm));
        // O histórico guarda a saída anterior e a volta.
        Assert.True(await db.Auditoria.AnyAsync(a => a.RaizId == id && a.Acao == AcaoAuditoria.Inativacao && a.Entidade == nameof(PessoaSocio)));
        Assert.True(await db.Auditoria.AnyAsync(a => a.RaizId == id && a.Acao == AcaoAuditoria.Reativacao && a.Entidade == nameof(PessoaSocio)));
    }

    // ---- U1–U7: conta de papel inativo continua aparecendo como hoje (D8, não regressão) ----

    private async Task<(Guid Pessoa, Guid Perfil, Guid Condicao)> ClienteEFornecedorInativosAsync()
    {
        var id = Guid.NewGuid();
        var perfil = Guid.NewGuid();
        var condicao = Guid.NewGuid();
        await using var db = Db();
        db.PerfisComerciais.Add(new PerfilComercial { Id = perfil, Nome = "Perfil " + id.ToString("N")[..8] });
        db.CondicoesPagamento.Add(new CondicaoPagamento { Id = condicao, Nome = "Cond " + id.ToString("N")[..8], Parcelas = "30" });
        var p = new Pessoa { Id = id, Nome = "U " + id.ToString("N")[..6], Natureza = NaturezaPessoa.Juridica };
        p.Papeis.Add(new PessoaPapel
        {
            Id = Guid.NewGuid(), PessoaId = id, PapelId = PapeisSistema.Id(TipoPapel.Cliente), Papel = TipoPapel.Cliente,
            Ativo = false, InicioEm = new DateOnly(2026, 1, 1), FimEm = new DateOnly(2026, 6, 30)
        });
        p.Papeis.Add(new PessoaPapel
        {
            Id = Guid.NewGuid(), PessoaId = id, PapelId = PapeisSistema.Id(TipoPapel.Fornecedor), Papel = TipoPapel.Fornecedor,
            Ativo = false, InicioEm = new DateOnly(2026, 1, 1), FimEm = new DateOnly(2026, 6, 30)
        });
        p.ContasCliente.Add(new ContaCliente { Id = Guid.NewGuid(), PessoaId = id, LimiteCredito = 7777m, PerfilComercialId = perfil, CondicaoPagamentoId = condicao });
        p.ContasFornecedor.Add(new ContaFornecedor { Id = Guid.NewGuid(), PessoaId = id, CondicaoPagamentoId = condicao, Avaliacao = 4 });
        db.Pessoas.Add(p);
        await db.SaveChangesAsync();
        return (id, perfil, condicao);
    }

    private async Task<bool> EncontraAsync(Guid pessoa, string campo, OperadorFiltro operador, params string[] valores)
    {
        await using var db = Db();
        var condicao = new CondicaoFiltro { Campo = campo, Operador = operador, Valores = [.. valores] };
        return await FiltrosPessoasSql.Aplicar(db.Pessoas.Where(p => p.Id == pessoa), [condicao],
                new FiltrosPessoasSql.Contexto(db, Hoje, new ParametrosRelacionamento()))
            .AnyAsync(p => p.Id == pessoa);
    }

    [FatoSqlServer]
    public async Task Filtros_de_conta_continuam_achando_a_conta_de_papel_inativo()
    {
        var (pessoa, perfil, condicao) = await ClienteEFornecedorInativosAsync();

        Assert.True(await EncontraAsync(pessoa, CamposFiltroPessoas.LimiteCredito, OperadorFiltro.Entre, "7000", "8000"));      // U3
        Assert.True(await EncontraAsync(pessoa, CamposFiltroPessoas.PerfilComercial, OperadorFiltro.UmDestes, perfil.ToString())); // U4 (e U8)
        Assert.False(await EncontraAsync(pessoa, CamposFiltroPessoas.PerfilComercial, OperadorFiltro.NenhumDestes, perfil.ToString()));
        Assert.True(await EncontraAsync(pessoa, CamposFiltroPessoas.CondicaoCliente, OperadorFiltro.UmDestes, condicao.ToString())); // U5 (e U8)
        Assert.True(await EncontraAsync(pessoa, CamposFiltroPessoas.CondicaoFornecedor, OperadorFiltro.UmDestes, condicao.ToString())); // U6
        Assert.True(await EncontraAsync(pessoa, CamposFiltroPessoas.AvaliacaoFornecedor, OperadorFiltro.Entre, "4", "5"));        // U7
    }

    [FatoSqlServer]
    public async Task Colunas_de_conta_continuam_mostrando_a_conta_de_papel_inativo()
    {
        var (pessoa, _, _) = await ClienteEFornecedorInativosAsync();
        await using var db = Db();
        var linha = new PessoaResumo { Id = pessoa };

        await ColunasPessoasSql.PreencherAsync([linha], [CamposFiltroPessoas.LimiteCredito, CamposFiltroPessoas.PerfilComercial],
            new ColunasPessoasSql.Contexto(db, Hoje), CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(linha.Valores[CamposFiltroPessoas.LimiteCredito]));                     // U1
        Assert.StartsWith("Perfil ", linha.Valores[CamposFiltroPessoas.PerfilComercial]);                           // U2
    }
}
