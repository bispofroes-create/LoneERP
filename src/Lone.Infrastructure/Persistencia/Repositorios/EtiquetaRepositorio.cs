using Lone.Application.Etiquetas;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Microsoft.Data.SqlClient;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Cadastro de etiquetas (SQL Server via EF Core). Nunca apaga etiquetas.</summary>
public class EtiquetaRepositorio : ServicoDadosBase, IEtiquetaRepositorio
{
    /// <summary>Cadastros alterados por gravação ao mesclar (cada lote gera o histórico de cada cadastro).</summary>
    private const int LoteMesclagem = 500;

    public EtiquetaRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<Etiqueta>> ListarAsync(bool incluirInativas, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consulta = db.Etiquetas.AsNoTracking();
        if (!incluirInativas) consulta = consulta.Where(e => e.Ativo);
        return await consulta.OrderBy(e => e.Nome).ToListAsync(ct);
    }

    public async Task<Etiqueta?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Etiquetas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<Dictionary<Guid, Etiqueta>> ObterVariasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.Etiquetas.AsNoTracking().Where(e => lista.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
    }

    public async Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // A coluna usa collation sem maiúsculas nem acentos: a comparação no banco já segue a regra do índice único.
        return await db.Etiquetas.AnyAsync(e => e.Nome == nome && e.Id != ignorarId, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarPessoasAsync(Guid? somenteEtiquetaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var ligacoes = db.PessoaEtiquetas.AsNoTracking();
        if (somenteEtiquetaId is { } id) ligacoes = ligacoes.Where(e => e.EtiquetaId == id);
        // Índice (EtiquetaId, PessoaId): agrupa sem ler a tabela de pessoas.
        return await ligacoes.GroupBy(e => e.EtiquetaId)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task SalvarAsync(Etiqueta etiqueta, bool nova, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        await AplicarAsync(db, etiqueta, nova, ct);
        await GravarAsync(db, ct);
    }

    public async Task<int> MesclarAsync(Etiqueta origem, Etiqueta destino, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        // 1) As duas etiquetas primeiro: se a origem mudou desde que o usuário abriu, para aqui (conflito).
        await AplicarAsync(db, origem, nova: false, ct);
        await AplicarAsync(db, destino, nova: false, ct);
        await GravarAsync(db, ct);

        // 2) Os cadastros, em lotes. Cada ligação alterada entra no histórico do próprio cadastro.
        var total = 0;
        while (true)
        {
            db.ChangeTracker.Clear();
            var lote = await db.PessoaEtiquetas
                .Where(e => e.EtiquetaId == origem.Id)
                .OrderBy(e => e.Id)
                .Take(LoteMesclagem)
                .ToListAsync(ct);
            if (lote.Count == 0) break;

            var pessoas = lote.Select(e => e.PessoaId).ToList();
            var jaTemDestino = (await db.PessoaEtiquetas
                    .Where(e => e.EtiquetaId == destino.Id && pessoas.Contains(e.PessoaId))
                    .Select(e => e.PessoaId)
                    .ToListAsync(ct))
                .ToHashSet();

            foreach (var ligacao in lote)
            {
                if (jaTemDestino.Contains(ligacao.PessoaId))
                    db.PessoaEtiquetas.Remove(ligacao); // já tinha as duas: fica só a de destino ("Etiqueta: origem → vazio")
                else
                    ligacao.EtiquetaId = destino.Id;    // "Etiqueta: origem → destino"
            }

            await GravarAsync(db, ct);
            total += lote.Count;
        }

        await transacao.CommitAsync(ct);
        return total;
    }

    /// <summary>Inclui, ou copia os dados para a gravada conferindo a versão que o usuário via.</summary>
    private static async Task AplicarAsync(LoneDbContext db, Etiqueta dados, bool nova, CancellationToken ct)
    {
        if (nova)
        {
            if (dados.Id == Guid.Empty) dados.Id = IdSequencial.Novo();
            db.Etiquetas.Add(dados);
            return;
        }

        var atual = await db.Etiquetas.FirstOrDefaultAsync(e => e.Id == dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
        var versaoAberta = dados.Versao;
        dados.Versao = atual.Versao;
        dados.CriadoEm = atual.CriadoEm;
        dados.AtualizadoEm = atual.AtualizadoEm;

        var entrada = db.Entry(atual);
        entrada.CurrentValues.SetValues(dados);
        entrada.Property(e => e.Versao).OriginalValue = versaoAberta;
        atual.ReceberEventosDe(dados);
        entrada.Property(e => e.AtualizadoEm).IsModified = true; // confere e troca a versão mesmo sem outra mudança
    }

    private static async Task GravarAsync(LoneDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoDeEdicaoException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Outra pessoa gravou o mesmo nome entre a conferência e a gravação (índice único).
            throw new ValidacaoException(["Já existe uma etiqueta com este nome (maiúsculas e acentos não contam)."]);
        }
    }
}
