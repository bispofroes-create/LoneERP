using System.Diagnostics;
using Lone.Contracts.Territorios;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using static Lone.Tests.Infraestrutura.MotorTerritorialTeste;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Volume (seção Q e DN-12): uma operação com 5 mudanças de estrutura e exatamente o limite de clientes (50.000) aplicada
/// numa transação só, com os tempos medidos (a medição de 10.000 — simular 1,5 s, aplicar 2,8 s — definiu o limite; este
/// teste prova que ele cabe), e um erro de banco no último lote de atribuições que desfaz os lotes
/// anteriores e tudo o mais. Banco temporário próprio (padrão do SQL Server). Pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public sealed class VolumeOperacoesTerritoriaisTests : IClassFixture<BancoTerritorios>
{
    private readonly BancoTerritorios _fixture;
    private readonly ITestOutputHelper _saida;

    public VolumeOperacoesTerritoriaisTests(BancoTerritorios fixture, ITestOutputHelper saida)
    {
        _fixture = fixture;
        _saida = saida;
    }

    private BancoDeTeste Banco => _fixture.Banco!;

    [FatoSqlServer]
    public async Task Cinco_mudancas_de_estrutura_e_o_limite_de_clientes_numa_transacao_so()
    {
        var c = await CenarioAsync(Banco, porEtiqueta: 0);
        var relogio = Stopwatch.StartNew();
        const int limite = RegrasOperacaoTerritorial.LimiteClientesPorOperacao;
        var clientes = await ClientesAsync(Banco, limite, c.EtiquetaMg);
        _saida.WriteLine($"Cadastro de {limite:N0} clientes (preparação): {relogio.Elapsed.TotalSeconds:N1} s");

        var repositorio = new TerritorioRepositorio(new Fabrica(Banco), new UsuarioTeste());
        var cinco = new List<Guid>();
        for (var i = 1; i <= 5; i++)
        {
            var t = Novo(c.Mapa.Id, $"Região {i}", c.Brasil.Id);
            await repositorio.SalvarAsync(t, novo: true, null, null, default);
            cinco.Add(t.Id);
        }

        var s = Operacoes(Banco);
        var mudancas = new List<IncluirMudancaTerritorialRequisicao> { Regra(c.Mg.Id, c.EtiquetaMg) };
        mudancas.AddRange(cinco.Select(t => new IncluirMudancaTerritorialRequisicao { Tipo = TipoMudancaTerritorial.MoverTerritorio, TerritorioId = t, NovoPaiId = c.Mg.Id }));
        var op = await RascunhoAsync(s, c.Mapa.Id, Hoje, [.. mudancas]);

        relogio.Restart();
        var simulada = await SimularAsync(s, op);
        var tempoSimular = relogio.Elapsed;
        relogio.Restart();
        var aplicada = await AplicarAsync(s, simulada);
        var tempoAplicar = relogio.Elapsed;
        _saida.WriteLine($"Simular ({limite:N0} clientes, 6 mudanças): {tempoSimular.TotalSeconds:N1} s");
        _saida.WriteLine($"Aplicar ({limite:N0} atribuições, 5 posições, uma transação): {tempoAplicar.TotalSeconds:N1} s");

        Assert.Equal(limite, aplicada.Entraram);
        var atribuidos = await AtribuidosAsync(Banco, c.Mapa.Id, Hoje);
        Assert.Equal(limite, atribuidos.Count);
        Assert.All(clientes, p => Assert.Equal(c.Mg.Id, atribuidos[p]));
        await using var db = Banco.Contexto();
        Assert.Equal(5, await db.Territorios.CountAsync(t => cinco.Contains(t.Id) && t.PaiId == c.Mg.Id));
        Assert.Equal(limite, await db.OperacaoTerritorialItens.CountAsync(i => i.OperacaoId == aplicada.Id));
    }

    [FatoSqlServer]
    public async Task Erro_de_banco_no_ultimo_lote_desfaz_os_lotes_anteriores_e_nada_fica()
    {
        var c = await CenarioAsync(Banco, porEtiqueta: 0);
        var clientes = await ClientesAsync(Banco, 2_500, c.EtiquetaSp); // três lotes de atribuições
        var s = Operacoes(Banco);

        // Uma operação anterior (regra em MG que não pega ninguém) só para existir regra e operação que a linha "de fora" cite.
        var semNinguem = await CenarioAsync(Banco, porEtiqueta: 0);
        var anterior = await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Mg.Id, semNinguem.EtiquetaMg)));
        Guid regraMg;
        await using (var db = Banco.Contexto())
            regraMg = await db.RegrasTerritorio.Where(r => r.TerritorioId == c.Mg.Id).Select(r => r.Id).SingleAsync();

        // Gravação feita por fora: o último cliente (na ordem em que a aplicação grava) já em MG a partir de daqui a 5 dias.
        var ultimo = clientes.Max();
        await using (var sql = new Microsoft.Data.SqlClient.SqlConnection(Banco.Conexao))
        {
            await sql.OpenAsync();
            await using var comando = new Microsoft.Data.SqlClient.SqlCommand($"""
                INSERT INTO AtribuicoesTerritorio (Id, MapaId, Exclusivo, TerritorioId, PessoaId, InicioEm, FimEm, Origem, RegraId, ExcecaoId, Ativo, OperacaoId, CriadoEm)
                VALUES ('{IdSequencial.Novo()}', '{c.Mapa.Id}', 1, '{c.Mg.Id}', '{ultimo}', '{Hoje.AddDays(5):yyyy-MM-dd}', NULL, 1, '{regraMg}', NULL, 1, '{anterior.Id}', SYSUTCDATETIME())
                """, sql);
            await comando.ExecuteNonQueryAsync();
        }

        var simulada = await SimularAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Sp.Id, c.EtiquetaSp)));
        var erro = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => AplicarAsync(s, simulada));
        _saida.WriteLine(erro.Message);
        Assert.Contains("recusou", erro.Message, StringComparison.Ordinal);

        await using var depois = Banco.Contexto();
        Assert.Equal(0, await depois.AtribuicoesTerritorio.CountAsync(a => a.OperacaoId == simulada.Id));
        Assert.Equal(0, await depois.RegrasTerritorio.CountAsync(r => r.OperacaoId == simulada.Id));
        Assert.Equal(0, await depois.OperacaoTerritorialItens.CountAsync(i => i.OperacaoId == simulada.Id));
        Assert.Equal(SituacaoOperacaoTerritorial.Simulada, (await s.ObterAsync(simulada.Id))!.Situacao);
        Assert.Equal(anterior.Id, (await depois.MapaTerritorialMotores.AsNoTracking().SingleAsync(m => m.MapaId == c.Mapa.Id)).UltimaOperacaoId);
    }
}
