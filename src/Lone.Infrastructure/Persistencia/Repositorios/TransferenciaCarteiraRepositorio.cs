using Lone.Application.Comercial;
using Lone.Application.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Transferências de carteira (SQL Server via EF Core, Motor Comercial, Fase 1d). Nunca apaga nem altera o concluído.</summary>
public class TransferenciaCarteiraRepositorio : ServicoDadosBase, ITransferenciaCarteiraRepositorio
{
    public TransferenciaCarteiraRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<ClienteDaOrigem>> ClientesDaOrigemAsync(FiltroTransferencia filtro, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var (origem, papel, empresa, efeito) = (filtro.OrigemId, filtro.TipoCarteiraId, filtro.EmpresaId, filtro.EfeitoEm);
        // A mesma condição de RegrasTransferencia.Candidatos, no banco (índice VendedorId, Ativo, FimEm).
        var clientes = db.CarteiraClientes.AsNoTracking()
            .Where(v => v.Ativo && v.VendedorId == origem && (v.FimEm == null || v.FimEm >= efeito) &&
                        (papel == null || v.TipoCarteiraId == papel) &&
                        (empresa == null || v.EmpresaId == empresa))
            .Select(v => v.PessoaId).Distinct();
        var lista = await db.Pessoas.AsNoTracking()
            .Where(p => clientes.Contains(p.Id))
            .Select(p => new { p.Id, Nome = p.NomeExibicao ?? p.Nome })
            .OrderBy(p => p.Nome).ThenBy(p => p.Id)
            .ToListAsync(ct);
        return [.. lista.Select(p => new ClienteDaOrigem(p.Id, p.Nome))];
    }

    public async Task<Dictionary<Guid, int>> CargasAsync(IReadOnlyCollection<Guid> pessoas, Guid? tipoCarteiraId, DateOnly data, CancellationToken ct)
    {
        if (pessoas.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var ids = pessoas.Distinct().ToList();
        return await db.CarteiraClientes.AsNoTracking()
            .Where(v => v.Ativo && ids.Contains(v.VendedorId) && v.InicioEm <= data && (v.FimEm == null || v.FimEm >= data) &&
                        (tipoCarteiraId == null || v.TipoCarteiraId == tipoCarteiraId))
            .GroupBy(v => v.VendedorId)
            .Select(g => new { g.Key, Clientes = g.Select(v => v.PessoaId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Clientes, ct);
    }

    public async Task<int> ProximaSequenciaAsync(int ano, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return (await db.TransferenciasCarteira.AsNoTracking().Where(t => t.Ano == ano).MaxAsync(t => (int?)t.Sequencia, ct) ?? 0) + 1;
    }

    public async Task IncluirAsync(TransferenciaCarteira transferencia, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.TransferenciasCarteira.Add(transferencia);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Duas transferências no mesmo instante pegaram o mesmo número: nada foi gravado; basta repetir.
            throw new ValidacaoException(["Outra transferência foi registrada ao mesmo tempo. Nada foi gravado: tente de novo."]);
        }
    }

    public async Task ConcluirAsync(TransferenciaCarteira transferencia, IReadOnlyList<TransferenciaCarteiraItem> itens, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var atual = await db.TransferenciasCarteira.FirstAsync(t => t.Id == transferencia.Id, ct);
        atual.Transferidos = transferencia.Transferidos;
        atual.NaoProcessados = transferencia.NaoProcessados;
        atual.Erros = transferencia.Erros;
        atual.Concluida = transferencia.Concluida;
        atual.ReceberEventosDe(transferencia);
        db.TransferenciaCarteiraItens.AddRange(itens);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<TransferenciaCarteira>> ListarAsync(int limite, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TransferenciasCarteira.AsNoTracking()
            .OrderByDescending(t => t.Ano).ThenByDescending(t => t.Sequencia)
            .Take(limite).ToListAsync(ct);
    }

    public async Task<TransferenciaCarteira?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TransferenciasCarteira.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Dictionary<Guid, string>> NumerosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        var linhas = await db.TransferenciasCarteira.AsNoTracking().Where(t => lista.Contains(t.Id))
            .Select(t => new { t.Id, t.Ano, t.Sequencia }).ToListAsync(ct);
        return linhas.ToDictionary(t => t.Id, t => RegrasTransferencia.Numero(t.Ano, t.Sequencia));
    }

    public async Task<List<TransferenciaCarteiraItem>> ItensAsync(Guid transferenciaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TransferenciaCarteiraItens.AsNoTracking().Where(i => i.TransferenciaId == transferenciaId).ToListAsync(ct);
    }
}
