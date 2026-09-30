using System.Data;
using Lone.Contracts.Territorios;
using Lone.Domain.Comum;
using Lone.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using static Lone.Tests.Infraestrutura.MotorTerritorialTeste;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// RCSI (seção Q): cada gatilho novo que lê a própria tabela (50073, 50074, 50075) demonstrado sem a dica
/// READCOMMITTEDLOCK (duas gravações simultâneas feitas por fora não se enxergam: duas verdades) e com a dica, como a
/// migration cria (a segunda espera e é barrada). O 50076 não lê a tabela (só nega UPDATE/DELETE): não depende do
/// isolamento.
/// </summary>
public sealed partial class BancoTerritoriosRcsiTests
{
    /// <summary>O mesmo gatilho sem a dica (só para a demonstração; volta ao texto da migration no fim).</summary>
    private static string SemDica(string criar) =>
        criar.Replace(" WITH (READCOMMITTEDLOCK)", string.Empty, StringComparison.Ordinal)
             .Replace("EXEC (N'CREATE TRIGGER ", "EXEC (N'CREATE OR ALTER TRIGGER ", StringComparison.Ordinal);

    private static string ComDica(string criar) =>
        criar.Replace("EXEC (N'CREATE TRIGGER ", "EXEC (N'CREATE OR ALTER TRIGGER ", StringComparison.Ordinal);

    private async Task DemonstrarAsync(string criarGatilho, int numero, Func<Task<(string A, string B, Func<Task<int>> Contar)>> par)
    {
        await ExecutarNoBancoAsync(SemDica(criarGatilho));
        try
        {
            var (a, b, contar) = await par();
            var (esperouSem, erroSem) = await DisputaAsync(a, b);
            Assert.False(esperouSem);
            Assert.Null(erroSem);
            Assert.Equal(2, await contar()); // duas verdades: a proteção sem a dica não vale sob RCSI
        }
        finally
        {
            await ExecutarNoBancoAsync(ComDica(criarGatilho));
        }

        var (a2, b2, contar2) = await par();
        var (esperou, erro) = await DisputaAsync(a2, b2);
        Assert.True(esperou);
        Assert.Equal(numero, NumeroDoErro(erro));
        Assert.Equal(1, await contar2());
    }

    private async Task<(Guid Mapa, Guid Mg, Guid Sp, Guid Brasil, Guid Operacao, Guid Regra)> BaseDoMotorAsync()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var aplicada = await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Mg.Id, c.EtiquetaMg)));
        await using var db = Banco.Contexto();
        var regra = await db.RegrasTerritorio.Where(r => r.TerritorioId == c.Mg.Id).Select(r => r.Id).SingleAsync();
        return (c.Mapa.Id, c.Mg.Id, c.Sp.Id, c.Brasil.Id, aplicada.Id, regra);
    }

    [FatoSqlServer]
    public async Task Gatilho_50073_sem_a_dica_deixa_duas_versoes_da_regra_e_com_a_dica_nao()
    {
        var m = await BaseDoMotorAsync();
        var numero = 10;
        await DemonstrarAsync(SqlMigracaoTerritorios.CriarProtecaoRegras, 50073, () =>
        {
            numero += 2;
            string Versao(int n, string inicio, string fim) => $"""
                INSERT INTO RegrasTerritorio (Id, MapaId, TerritorioId, Numero, Grupos, Criterios, Prioridade, InicioEm, FimEm, Ativo, OperacaoId, CriadoEm)
                VALUES ('{IdSequencial.Novo()}', '{m.Mapa}', '{m.Brasil}', {n}, '{"{}"}', 'x', NULL, '{inicio}', '{fim}', 1, '{m.Operacao}', SYSUTCDATETIME())
                """;
            var ano = 2030 + numero;
            Task<int> Contar() => ContarAsync($"SELECT COUNT(*) FROM RegrasTerritorio WHERE TerritorioId = '{m.Brasil}' AND Ativo = 1 AND InicioEm >= '{ano}-01-01' AND InicioEm < '{ano + 1}-01-01'");
            return Task.FromResult((Versao(numero, $"{ano}-01-01", $"{ano}-01-31"), Versao(numero + 1, $"{ano}-01-15", $"{ano}-02-15"), (Func<Task<int>>)Contar));
        });
    }

    [FatoSqlServer]
    public async Task Gatilho_50074_sem_a_dica_deixa_duas_fixacoes_no_exclusivo_e_com_a_dica_nao()
    {
        var m = await BaseDoMotorAsync();
        await DemonstrarAsync(SqlMigracaoTerritorios.CriarProtecaoExcecoes, 50074, async () =>
        {
            var cliente = (await ClientesAsync(Banco, 1, null))[0];
            string Fixar(Guid territorio, string inicio, string fim) => $"""
                INSERT INTO ExcecoesTerritorio (Id, MapaId, Exclusivo, TerritorioId, PessoaId, Tipo, InicioEm, FimEm, Motivo, Origem, Ativo, OperacaoId, CriadoEm)
                VALUES ('{IdSequencial.Novo()}', '{m.Mapa}', 1, '{territorio}', '{cliente}', 1, '{inicio}', '{fim}', 'demonstração', 1, 1, '{m.Operacao}', SYSUTCDATETIME())
                """;
            Task<int> Contar() => ContarAsync($"SELECT COUNT(*) FROM ExcecoesTerritorio WHERE PessoaId = '{cliente}' AND Ativo = 1");
            return (Fixar(m.Mg, "2027-01-01", "2027-01-31"), Fixar(m.Sp, "2027-01-15", "2027-02-15"), Contar);
        });
    }

    [FatoSqlServer]
    public async Task Gatilho_50075_sem_a_dica_deixa_o_cliente_em_dois_territorios_e_com_a_dica_nao()
    {
        var m = await BaseDoMotorAsync();
        await DemonstrarAsync(SqlMigracaoTerritorios.CriarProtecaoAtribuicoes, 50075, async () =>
        {
            var cliente = (await ClientesAsync(Banco, 1, null))[0];
            string Atribuir(Guid territorio, string inicio, string fim) => $"""
                INSERT INTO AtribuicoesTerritorio (Id, MapaId, Exclusivo, TerritorioId, PessoaId, InicioEm, FimEm, Origem, RegraId, ExcecaoId, Ativo, OperacaoId, CriadoEm)
                VALUES ('{IdSequencial.Novo()}', '{m.Mapa}', 1, '{territorio}', '{cliente}', '{inicio}', '{fim}', 1, '{m.Regra}', NULL, 1, '{m.Operacao}', SYSUTCDATETIME())
                """;
            Task<int> Contar() => ContarAsync($"SELECT COUNT(*) FROM AtribuicoesTerritorio WHERE PessoaId = '{cliente}' AND Ativo = 1");
            return (Atribuir(m.Mg, "2027-01-01", "2027-01-31"), Atribuir(m.Sp, "2027-01-15", "2027-02-15"), Contar);
        });
    }

    private async Task<int> ContarAsync(string sql)
    {
        await using var conexao = await AbrirConexaoAsync();
        await using var comando = new Microsoft.Data.SqlClient.SqlCommand(sql, conexao);
        return Convert.ToInt32(await comando.ExecuteScalarAsync());
    }
}
