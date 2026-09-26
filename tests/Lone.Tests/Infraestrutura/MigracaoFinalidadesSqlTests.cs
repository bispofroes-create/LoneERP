using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Cenário legado (bits antigos em PessoaEnderecos.Finalidades) montado num banco temporário e migrado UMA vez com o
/// SQL real da migração (SqlMigracaoFinalidadesEndereco.MigrarFinalidades, com as conferências independentes).
/// Cada caso é uma pessoa. Pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public sealed class CenarioLegadoMigrado : IAsyncLifetime
{
    public BancoDeTeste? Banco { get; private set; }
    public Dictionary<string, (Guid Pessoa, Guid[] Enderecos)> Casos { get; } = new();

    private static readonly FinalidadeEndereco P = FinalidadeEndereco.Principal;
    private static readonly FinalidadeEndereco E = FinalidadeEndereco.Entrega;
    private static readonly FinalidadeEndereco C = FinalidadeEndereco.Cobranca;
    private static readonly FinalidadeEndereco F = FinalidadeEndereco.Fiscal;

    public async Task InitializeAsync()
    {
        if (FatoSqlServerAttribute.Conexao is null) return;
        Banco = await BancoDeTeste.CriarAsync(comProtecoes: false); // gatilhos só depois dos dados, como na migração

        await using (var db = Banco.Contexto())
        {
            Caso(db, "16_uma_finalidade", (E, true));
            Caso(db, "17_varias_finalidades", (E | C | F, true));
            Caso(db, "18_antigo_principal_varias", (P | E | C, true), (E, true));
            Caso(db, "19_dois_sem_antigo_principal", (E, true), (E, true));
            Caso(db, "20_varios_com_antigo_principal", (E, true), (P | E, true), (E, true));
            Caso(db, "21_antigo_principal_sem_finalidade", (P, true), (F, true));
            Caso(db, "22_inativo", (P | E, false), (E, true));
            Caso(db, "23_antigos_principais_repetidos", (P | E, true), (P | E | F, true));
            Caso(db, "sem_finalidade_nenhuma", (FinalidadeEndereco.Nenhuma, true));
            await db.SaveChangesAsync();
        }

        await using var migracao = Banco.Contexto();
        await migracao.Database.ExecuteSqlRawAsync(SqlMigracaoFinalidadesEndereco.MigrarFinalidades);
    }

    private void Caso(LoneDbContext db, string nome, params (FinalidadeEndereco Bits, bool Ativo)[] enderecos)
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = nome, Natureza = NaturezaPessoa.Fisica };
        for (var i = 0; i < enderecos.Length; i++)
            p.Enderecos.Add(new PessoaEndereco
            {
                Id = Guid.NewGuid(), PessoaId = p.Id, Logradouro = $"Rua {i}", Numero = $"{i}", Cidade = "Curvelo", Ordem = i,
                Finalidades = enderecos[i].Bits, Ativo = enderecos[i].Ativo
            });
        db.Pessoas.Add(p);
        Casos[nome] = (p.Id, p.Enderecos.Select(e => e.Id).ToArray());
    }

    public async Task DisposeAsync()
    {
        if (Banco is not null) await Banco.DisposeAsync();
    }
}

public class MigracaoFinalidadesSqlTests : IClassFixture<CenarioLegadoMigrado>
{
    private static readonly Guid Entrega = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Entrega);
    private static readonly Guid Fiscal = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Fiscal);
    private readonly CenarioLegadoMigrado _cenario;

    public MigracaoFinalidadesSqlTests(CenarioLegadoMigrado cenario) => _cenario = cenario;

    private async Task<(List<PessoaEnderecoFinalidade> Usos, List<PessoaEndereco> Enderecos, bool Revisar)> LerAsync(string caso)
    {
        var (pessoa, _) = _cenario.Casos[caso];
        await using var db = _cenario.Banco!.Contexto();
        var usos = await db.PessoaEnderecoFinalidades.AsNoTracking().Where(u => u.PessoaId == pessoa).ToListAsync();
        var enderecos = await db.PessoaEnderecos.AsNoTracking().Where(e => e.PessoaId == pessoa).OrderBy(e => e.Ordem).ToListAsync();
        var revisar = await db.Pessoas.Where(p => p.Id == pessoa).Select(p => p.RevisarFinalidadesEndereco).SingleAsync();
        return (usos, enderecos, revisar);
    }

    private Guid Endereco(string caso, int i) => _cenario.Casos[caso].Enderecos[i];

    [FatoSqlServer]
    public async Task Um_endereco_com_uma_finalidade_vira_principal_dela()
    {
        var (usos, _, revisar) = await LerAsync("16_uma_finalidade");
        var uso = Assert.Single(usos);
        Assert.Equal(Entrega, uso.FinalidadeId);
        Assert.True(uso.Principal);
        Assert.False(revisar);
    }

    [FatoSqlServer]
    public async Task Um_endereco_com_varias_finalidades_e_principal_de_todas()
    {
        var (usos, _, revisar) = await LerAsync("17_varias_finalidades");
        Assert.Equal(3, usos.Count);
        Assert.All(usos, u => Assert.True(u.Principal && u.Ativo));
        Assert.False(revisar);
    }

    [FatoSqlServer]
    public async Task Antigo_principal_e_principal_so_das_finalidades_que_ele_tinha()
    {
        const string caso = "18_antigo_principal_varias";
        var (usos, _, revisar) = await LerAsync(caso);
        Assert.Equal(3, usos.Count);                                             // o bit Principal não virou finalidade
        Assert.All(usos.Where(u => u.PessoaEnderecoId == Endereco(caso, 0)), u => Assert.True(u.Principal));
        Assert.False(usos.Single(u => u.PessoaEnderecoId == Endereco(caso, 1)).Principal);
        Assert.False(revisar);
    }

    [FatoSqlServer]
    public async Task Dois_enderecos_com_a_mesma_finalidade_sem_antigo_principal_ficam_sem_principal_e_em_revisao()
    {
        var (usos, _, revisar) = await LerAsync("19_dois_sem_antigo_principal");
        Assert.Equal(2, usos.Count);
        Assert.All(usos, u => Assert.False(u.Principal)); // nada escolhido por Id, ordem ou data
        Assert.True(revisar);
    }

    [FatoSqlServer]
    public async Task Varios_enderecos_e_o_antigo_principal_com_a_finalidade()
    {
        const string caso = "20_varios_com_antigo_principal";
        var (usos, _, revisar) = await LerAsync(caso);
        Assert.Equal(Endereco(caso, 1), usos.Single(u => u.Principal).PessoaEnderecoId);
        Assert.False(revisar);
    }

    [FatoSqlServer]
    public async Task Antigo_principal_sem_finalidade_nao_ganha_finalidade_inventada_e_fica_para_revisao()
    {
        const string caso = "21_antigo_principal_sem_finalidade";
        var (usos, enderecos, revisar) = await LerAsync(caso);
        Assert.DoesNotContain(usos, u => u.PessoaEnderecoId == Endereco(caso, 0));
        Assert.True(usos.Single(u => u.FinalidadeId == Fiscal).Principal);          // regra 2 no outro endereço
        Assert.Equal(MotivoRevisaoEndereco.AntigoPrincipalSemFinalidade, enderecos[0].RevisaoMigracao);
        Assert.True(revisar);
    }

    [FatoSqlServer]
    public async Task Endereco_inativo_guarda_historico_sem_principal()
    {
        const string caso = "22_inativo";
        var (usos, enderecos, revisar) = await LerAsync(caso);
        var doInativo = usos.Single(u => u.PessoaEnderecoId == Endereco(caso, 0));
        Assert.True(doInativo.Ativo);
        Assert.False(doInativo.Principal);
        Assert.True(usos.Single(u => u.PessoaEnderecoId == Endereco(caso, 1)).Principal); // único ATIVO com Entrega
        Assert.Equal(MotivoRevisaoEndereco.Nenhum, enderecos[0].RevisaoMigracao);
        Assert.False(revisar);
    }

    [FatoSqlServer]
    public async Task Antigos_principais_repetidos_nao_sao_escolhidos_e_ficam_para_revisao()
    {
        const string caso = "23_antigos_principais_repetidos";
        var (usos, enderecos, revisar) = await LerAsync(caso);
        Assert.All(usos.Where(u => u.FinalidadeId == Entrega), u => Assert.False(u.Principal)); // ambígua: ninguém
        Assert.True(usos.Single(u => u.FinalidadeId == Fiscal).Principal);                     // só num endereço: regra 2
        Assert.All(enderecos, e => Assert.Equal(MotivoRevisaoEndereco.AntigoPrincipalRepetido, e.RevisaoMigracao));
        Assert.True(revisar);
    }

    [FatoSqlServer]
    public async Task Revisao_so_quando_necessaria()
    {
        foreach (var caso in new[] { "16_uma_finalidade", "17_varias_finalidades", "18_antigo_principal_varias",
                                     "20_varios_com_antigo_principal", "22_inativo", "sem_finalidade_nenhuma" })
            Assert.False((await LerAsync(caso)).Revisar, caso);
        Assert.Empty((await LerAsync("sem_finalidade_nenhuma")).Usos); // sem bits: nada inventado
    }

    [FatoSqlServer]
    public async Task Rodar_de_novo_e_recusado_sem_alterar_nada()
    {
        await using var db = _cenario.Banco!.Contexto();
        var antes = await db.PessoaEnderecoFinalidades.CountAsync();

        var erro = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(SqlMigracaoFinalidadesEndereco.MigrarFinalidades));
        var sql = erro as SqlException ?? erro.InnerException as SqlException;
        Assert.Equal(50020, sql?.Number);
        Assert.Equal(antes, await db.PessoaEnderecoFinalidades.CountAsync());
    }
}
