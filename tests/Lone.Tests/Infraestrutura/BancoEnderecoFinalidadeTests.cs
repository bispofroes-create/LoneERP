using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Tests.Infraestrutura;

/// <summary>Banco temporário com o modelo atual e os gatilhos da migração (um por classe de teste).</summary>
public sealed class BancoComProtecoes : IAsyncLifetime
{
    public BancoDeTeste? Banco { get; private set; }

    public async Task InitializeAsync()
    {
        if (FatoSqlServerAttribute.Conexao is not null) Banco = await BancoDeTeste.CriarAsync(comProtecoes: true);
    }

    public async Task DisposeAsync()
    {
        if (Banco is not null) await Banco.DisposeAsync();
    }
}

/// <summary>
/// As regras estruturais de endereço × finalidade garantidas PELO BANCO (não pela aplicação): cada teste tenta gravar
/// um estado inválido direto em SQL e confere que o SQL Server recusa. Pulados sem LONE_TESTES_SQLSERVER.
/// </summary>
public class BancoEnderecoFinalidadeTests : IClassFixture<BancoComProtecoes>
{
    private static readonly Guid Entrega = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Entrega);
    private static readonly Guid Fiscal = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Fiscal);
    private static readonly Guid Correspondencia = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Correspondencia);
    private readonly BancoComProtecoes _fixture;

    public BancoEnderecoFinalidadeTests(BancoComProtecoes fixture) => _fixture = fixture;

    private LoneDbContext Db() => _fixture.Banco!.Contexto();

    private async Task<(Guid Pessoa, Guid[] Enderecos)> PessoaAsync(int enderecos = 2)
    {
        await using var db = Db();
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Teste " + Guid.NewGuid().ToString("N")[..6], Natureza = NaturezaPessoa.Fisica };
        for (var i = 0; i < enderecos; i++)
            p.Enderecos.Add(new PessoaEndereco { Id = Guid.NewGuid(), PessoaId = p.Id, Logradouro = $"Rua {i}", Numero = $"{i}", Cidade = "Curvelo", Ordem = i });
        db.Pessoas.Add(p);
        await db.SaveChangesAsync();
        return (p.Id, p.Enderecos.Select(e => e.Id).ToArray());
    }

    private async Task InserirRelacaoAsync(Guid pessoa, Guid endereco, Guid finalidade, bool principal = false, bool ativo = true)
    {
        await using var db = Db();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO PessoaEnderecoFinalidades (Id, PessoaId, PessoaEnderecoId, FinalidadeId, Principal, Ativo, CriadoEm)
            VALUES ({Guid.NewGuid()}, {pessoa}, {endereco}, {finalidade}, {principal}, {ativo}, SYSUTCDATETIME())
            """);
    }

    private async Task ExecutarAsync(FormattableString sql)
    {
        await using var db = Db();
        await db.Database.ExecuteSqlAsync(sql);
    }

    private static async Task<SqlException> Recusado(Func<Task> acao)
    {
        var erro = await Assert.ThrowsAnyAsync<Exception>(acao);
        return erro as SqlException ?? erro.InnerException as SqlException ?? throw new Xunit.Sdk.XunitException("Esperava erro do SQL Server: " + erro);
    }

    [FatoSqlServer]
    public async Task Relacao_com_endereco_de_outra_pessoa_e_recusada_pela_FK_composta()
    {
        var (pessoaA, _) = await PessoaAsync();
        var (_, enderecosB) = await PessoaAsync();

        Assert.Equal(547, (await Recusado(() => InserirRelacaoAsync(pessoaA, enderecosB[0], Entrega))).Number);
    }

    [FatoSqlServer]
    public async Task MescladoEmId_nao_aponta_para_endereco_de_outra_pessoa()
    {
        var (_, enderecosA) = await PessoaAsync();
        var (_, enderecosB) = await PessoaAsync();

        var erro = await Recusado(() => ExecutarAsync($"UPDATE PessoaEnderecos SET Ativo = 0, MescladoEmId = {enderecosB[0]} WHERE Id = {enderecosA[0]}"));
        Assert.Equal(547, erro.Number);

        await ExecutarAsync($"UPDATE PessoaEnderecos SET Ativo = 0, MescladoEmId = {enderecosA[1]} WHERE Id = {enderecosA[0]}"); // mesma pessoa: aceito
    }

    [FatoSqlServer]
    public async Task Consolidado_nao_fica_ativo_nem_aponta_para_si()
    {
        var (_, enderecos) = await PessoaAsync();

        Assert.Equal(547, (await Recusado(() => ExecutarAsync($"UPDATE PessoaEnderecos SET MescladoEmId = {enderecos[1]} WHERE Id = {enderecos[0]}"))).Number);
        Assert.Equal(547, (await Recusado(() => ExecutarAsync($"UPDATE PessoaEnderecos SET Ativo = 0, MescladoEmId = {enderecos[0]} WHERE Id = {enderecos[0]}"))).Number);
    }

    [FatoSqlServer]
    public async Task Finalidade_ativa_repetida_no_endereco_e_recusada_e_historico_nao()
    {
        var (pessoa, enderecos) = await PessoaAsync();
        await InserirRelacaoAsync(pessoa, enderecos[0], Entrega, ativo: false);   // histórico
        await InserirRelacaoAsync(pessoa, enderecos[0], Entrega);                 // ativa

        var erro = await Recusado(() => InserirRelacaoAsync(pessoa, enderecos[0], Entrega));
        Assert.Contains(erro.Number, new[] { 2601, 2627 });
        Assert.Equal(ConflitosEnderecoFinalidade.FinalidadeRepetida, ConflitosEnderecoFinalidade.Mensagem(erro));
    }

    [FatoSqlServer]
    public async Task Dois_principais_da_mesma_finalidade_sao_recusados_mesmo_por_dois_usuarios()
    {
        var (pessoa, enderecos) = await PessoaAsync();
        await InserirRelacaoAsync(pessoa, enderecos[0], Fiscal, principal: true);  // usuário A: X é o principal fiscal

        // Usuário B (outra conexão) tenta Y como principal fiscal: o índice único é a última barreira.
        var erro = await Recusado(() => InserirRelacaoAsync(pessoa, enderecos[1], Fiscal, principal: true));
        Assert.Contains(erro.Number, new[] { 2601, 2627 });
        Assert.Equal(ConflitosEnderecoFinalidade.PrincipalRepetido, ConflitosEnderecoFinalidade.Mensagem(erro));

        await InserirRelacaoAsync(pessoa, enderecos[1], Entrega, principal: true); // outra finalidade: aceito
        await InserirRelacaoAsync(pessoa, enderecos[0], Entrega);                  // mesma finalidade sem principal: aceito
    }

    [FatoSqlServer]
    public async Task Troca_de_principal_na_ordem_certa_numa_transacao_e_aceita()
    {
        var (pessoa, enderecos) = await PessoaAsync();
        await InserirRelacaoAsync(pessoa, enderecos[0], Fiscal, principal: true);
        await InserirRelacaoAsync(pessoa, enderecos[1], Fiscal);

        await ExecutarAsync($"""
            BEGIN TRANSACTION;
            UPDATE PessoaEnderecoFinalidades SET Principal = 0 WHERE PessoaEnderecoId = {enderecos[0]} AND FinalidadeId = {Fiscal};
            UPDATE PessoaEnderecoFinalidades SET Principal = 1 WHERE PessoaEnderecoId = {enderecos[1]} AND FinalidadeId = {Fiscal};
            COMMIT;
            """);

        await using var db = Db();
        Assert.Equal(enderecos[1], await db.PessoaEnderecoFinalidades.Where(u => u.PessoaId == pessoa && u.Principal).Select(u => u.PessoaEnderecoId).SingleAsync());
    }

    [FatoSqlServer]
    public async Task Relacao_inativa_nao_e_principal()
    {
        var (pessoa, enderecos) = await PessoaAsync();
        Assert.Equal(547, (await Recusado(() => InserirRelacaoAsync(pessoa, enderecos[0], Entrega, principal: true, ativo: false))).Number);
    }

    [FatoSqlServer]
    public async Task Endereco_inativo_nao_e_nem_continua_principal()
    {
        var (pessoa, enderecos) = await PessoaAsync();
        await ExecutarAsync($"UPDATE PessoaEnderecos SET Ativo = 0 WHERE Id = {enderecos[0]}");
        Assert.Equal(SqlMigracaoFinalidadesEndereco.ErroPrincipalEmEnderecoInativo,
            (await Recusado(() => InserirRelacaoAsync(pessoa, enderecos[0], Entrega, principal: true))).Number);

        await InserirRelacaoAsync(pessoa, enderecos[1], Entrega, principal: true);
        var erro = await Recusado(() => ExecutarAsync($"UPDATE PessoaEnderecos SET Ativo = 0 WHERE Id = {enderecos[1]}"));
        Assert.Equal(SqlMigracaoFinalidadesEndereco.ErroEnderecoInativoComPrincipal, erro.Number);
        Assert.Equal(ConflitosEnderecoFinalidade.EnderecoInativoPrincipal, ConflitosEnderecoFinalidade.Mensagem(erro));

        // Na ordem certa (tira o principal, depois desativa) é aceito.
        await ExecutarAsync($"UPDATE PessoaEnderecoFinalidades SET Principal = 0 WHERE PessoaEnderecoId = {enderecos[1]}");
        await ExecutarAsync($"UPDATE PessoaEnderecos SET Ativo = 0 WHERE Id = {enderecos[1]}");
    }

    [FatoSqlServer]
    public async Task Finalidade_inexistente_e_recusada()
    {
        var (pessoa, enderecos) = await PessoaAsync();
        Assert.Equal(547, (await Recusado(() => InserirRelacaoAsync(pessoa, enderecos[0], Guid.NewGuid()))).Number);
    }

    [FatoSqlServer]
    public async Task Finalidade_de_sistema_nao_muda_de_codigo_nao_e_desativada_nem_excluida()
    {
        Assert.Equal(SqlMigracaoFinalidadesEndereco.ErroFinalidadeSistema,
            (await Recusado(() => ExecutarAsync($"UPDATE FinalidadesEndereco SET Codigo = 'FISCAL2' WHERE Id = {Fiscal}"))).Number);
        Assert.Equal(547, (await Recusado(() => ExecutarAsync($"UPDATE FinalidadesEndereco SET Ativo = 0 WHERE Id = {Fiscal}"))).Number);
        Assert.Equal(SqlMigracaoFinalidadesEndereco.ErroFinalidadeSistema,
            (await Recusado(() => ExecutarAsync($"DELETE FROM FinalidadesEndereco WHERE Id = {Correspondencia}"))).Number); // sem relações (a FK não interfere)

        await ExecutarAsync($"UPDATE FinalidadesEndereco SET Nome = N'Fiscal (sede)' WHERE Id = {Fiscal}"); // nome pode mudar
        await ExecutarAsync($"UPDATE FinalidadesEndereco SET Nome = N'Fiscal' WHERE Id = {Fiscal}");
    }
}
