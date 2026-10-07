using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Documentos;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// V2-0.2 (07/10/2026): as duas consultas com corte (<c>Take</c>) que rodam no Salvar da ficha de Pessoas têm ordem estável
/// antes do corte. Sem ela o SQL Server devolve linhas quaisquer, o resultado pode variar entre execuções e o EF Core avisa
/// 10102 ("row limiting operator without OrderBy"). Aqui o contexto transforma esse aviso em erro: se a ordem sumir, o teste
/// quebra. Banco temporário; pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class LimiteOrdenadoConsultasTests : IClassFixture<AmbienteCadastroPessoas>
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);
    private static readonly Guid Rg = TiposDocumentoSistema.Id(TipoDocumento.Rg);

    private readonly AmbienteCadastroPessoas _ambiente;

    public LimiteOrdenadoConsultasTests(AmbienteCadastroPessoas ambiente) => _ambiente = ambiente;

    private BancoDeTeste Banco => _ambiente.Banco!;

    /// <summary>Contexto em que o aviso 10102 vira exceção (só nestes testes; a aplicação não muda).</summary>
    private sealed class FabricaEstrita(BancoDeTeste banco) : IDbContextFactory<LoneDbContext>
    {
        public LoneDbContext CreateDbContext() => new(new DbContextOptionsBuilder<LoneDbContext>()
            .UseSqlServer(banco.Conexao)
            .ConfigureWarnings(w => w.Throw(CoreEventId.RowLimitingOperationWithoutOrderByWarning))
            .Options);
    }

    private PessoaRepositorio Repositorio(IDbContextFactory<LoneDbContext> fabrica) =>
        new(fabrica, new MotorTerritorialTeste.UsuarioTeste(), new MotorTerritorialTeste.EscopoTudo(Hoje));

    /// <summary>Grava <paramref name="quantas"/> pessoas jurídicas com o mesmo nome (e, se pedido, o mesmo RG) e devolve os Ids.</summary>
    private async Task<List<Guid>> GravarAsync(int quantas, string nome, string? rg = null)
    {
        var repo = Repositorio(new MotorTerritorialTeste.Fabrica(Banco));
        var ids = new List<Guid>();
        for (var i = 0; i < quantas; i++)
        {
            var id = Guid.NewGuid();
            var p = new Pessoa { Id = id, Nome = nome, Natureza = NaturezaPessoa.Juridica };
            if (rg is not null)
                p.Documentos.Add(new PessoaDocumento
                {
                    Id = Guid.NewGuid(), PessoaId = id, TipoDocumentoId = Rg, Tipo = TipoDocumento.Rg, Numero = rg, NumeroNormalizado = rg
                });
            await repo.SalvarAsync(p, nova: true, OrigemAlteracao.Usuario, CancellationToken.None);
            ids.Add(id);
        }
        return ids;
    }

    /// <summary>Os Ids na ordem do código (o cadastro mais antigo primeiro), lidos direto do banco.</summary>
    private async Task<List<Guid>> PorCodigoAsync(IEnumerable<Guid> ids)
    {
        var lista = ids.ToList();
        await using var db = Banco.Contexto();
        return await db.Pessoas.AsNoTracking().Where(p => lista.Contains(p.Id)).OrderBy(p => p.Codigo).ThenBy(p => p.Id)
            .Select(p => p.Id).ToListAsync();
    }

    [FatoSqlServer]
    public async Task Pessoas_semelhantes_vem_em_ordem_estavel_e_sem_o_aviso_10102()
    {
        var nome = "V2-0 Semelhante " + Guid.NewGuid().ToString("N")[..8];
        var ids = await GravarAsync(7, nome);
        var esperado = (await PorCodigoAsync(ids)).Take(5).ToList();
        var repo = Repositorio(new FabricaEstrita(Banco));

        // Três execuções: sempre as mesmas 5, na mesma ordem (os 5 cadastros mais antigos), sem repetir nenhuma.
        for (var i = 0; i < 3; i++)
        {
            var semelhantes = await repo.BuscarSemelhantesAsync(Guid.NewGuid(), nome, [], CancellationToken.None);
            Assert.Equal(esperado, semelhantes.Select(s => s.Id).ToList());
            Assert.Equal(5, semelhantes.Select(s => s.Id).Distinct().Count());
        }
    }

    [FatoSqlServer]
    public async Task Pessoas_semelhantes_continuam_ignorando_a_propria_pessoa()
    {
        var nome = "V2-0 Propria " + Guid.NewGuid().ToString("N")[..8];
        var ids = await GravarAsync(3, nome);
        var repo = Repositorio(new FabricaEstrita(Banco));

        var semelhantes = await repo.BuscarSemelhantesAsync(ids[0], nome, [], CancellationToken.None);

        Assert.Equal((await PorCodigoAsync(ids.Skip(1))), semelhantes.Select(s => s.Id).ToList());
    }

    [FatoSqlServer]
    public async Task Documento_igual_em_outras_pessoas_vem_em_ordem_estavel_e_sem_o_aviso_10102()
    {
        var rg = Random.Shared.Next(10_000_000, 99_999_999).ToString();
        var ids = await GravarAsync(4, "V2-0 Documento " + rg, rg);
        var esperado = await PorCodigoAsync(ids);
        var repo = new TipoDocumentoRepositorio(new FabricaEstrita(Banco), new MotorTerritorialTeste.UsuarioTeste());

        for (var i = 0; i < 3; i++)
        {
            var iguais = await repo.BuscarIguaisEmOutrasPessoasAsync(Guid.NewGuid(), [Rg], [rg], CancellationToken.None);
            Assert.Equal(esperado, iguais.Select(d => d.Pessoa.Id).ToList());
            Assert.All(iguais, d => Assert.Equal((Rg, rg), (d.TipoDocumentoId, d.NumeroNormalizado)));
        }
    }
}
