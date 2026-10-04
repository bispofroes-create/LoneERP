using Lone.Application.Profissoes;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Cadastro de profissões (SQL Server via EF Core). Nunca apaga profissões.</summary>
public class ProfissaoRepositorio : ServicoDadosBase, IProfissaoRepositorio
{
    /// <summary>Pessoas alteradas por gravação ao mesclar (cada lote gera o histórico de cada pessoa).</summary>
    private const int LoteMesclagem = 500;

    public ProfissaoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<Profissao>> ListarAsync(bool incluirInativas, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consulta = db.Profissoes.AsNoTracking();
        if (!incluirInativas) consulta = consulta.Where(p => p.Ativo);
        return await consulta.OrderBy(p => p.Nome).ToListAsync(ct);
    }

    public async Task<Profissao?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Profissoes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Collation da coluna sem maiúsculas nem acentos: a comparação no banco segue a regra do índice único.
        return await db.Profissoes.AnyAsync(p => p.Nome == nome && p.Id != ignorarId, ct);
    }

    public async Task<Profissao?> ObterParaOcupacaoAsync(int codigo, string nome, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Profissoes.AsNoTracking()
                   .Where(p => p.Ativo && p.OcupacaoCboId == codigo).OrderBy(p => p.Nome).FirstOrDefaultAsync(ct)
               ?? await db.Profissoes.AsNoTracking().FirstOrDefaultAsync(p => p.Nome == nome, ct); // collation CI_AI
    }

    public async Task<Dictionary<Guid, int>> ContarPessoasAsync(Guid? somenteProfissaoId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Sem escopo: configuração de profissões (contagem de uso e troca em lote), sem mostrar cadastros.
        var pessoas = db.Pessoas.AsNoTracking().Where(p => p.ProfissaoId != null);
        if (somenteProfissaoId is { } id) pessoas = pessoas.Where(p => p.ProfissaoId == id);
        // Índice em Pessoas.ProfissaoId: agrupa sem ler o resto do cadastro.
        return await pessoas.GroupBy(p => p.ProfissaoId!.Value)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task SalvarAsync(Profissao profissao, bool nova, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        await AplicarAsync(db, profissao, nova, ct);
        await GravarAsync(db, ct);
    }

    public async Task<int> MesclarAsync(Profissao origem, Profissao destino, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        // 1) As duas profissões primeiro: se a origem mudou desde que o usuário abriu, para aqui (conflito).
        await AplicarAsync(db, origem, nova: false, ct);
        await AplicarAsync(db, destino, nova: false, ct);
        await GravarAsync(db, ct);

        // 2) As pessoas, em lotes. A pessoa muda de versão (quem estiver com a ficha aberta relê antes de salvar),
        //    e a troca fica no histórico dela ("Profissão: origem → destino").
        var total = 0;
        while (true)
        {
            db.ChangeTracker.Clear();
            // Sem escopo: configuração de profissões (contagem de uso e troca em lote), sem mostrar cadastros.
            var lote = await db.Pessoas
                .Where(p => p.ProfissaoId == origem.Id)
                .OrderBy(p => p.Id)
                .Take(LoteMesclagem)
                .ToListAsync(ct);
            if (lote.Count == 0) break;

            foreach (var pessoa in lote) pessoa.ProfissaoId = destino.Id;
            await GravarAsync(db, ct);
            total += lote.Count;
        }

        await transacao.CommitAsync(ct);
        return total;
    }

    /// <summary>Inclui, ou copia os dados para a gravada conferindo a versão que o usuário via.</summary>
    private static async Task AplicarAsync(LoneDbContext db, Profissao dados, bool nova, CancellationToken ct)
    {
        if (nova)
        {
            if (dados.Id == Guid.Empty) dados.Id = IdSequencial.Novo();
            db.Profissoes.Add(dados);
            return;
        }

        var atual = await db.Profissoes.FirstOrDefaultAsync(p => p.Id == dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
        var versaoAberta = dados.Versao;
        dados.Versao = atual.Versao;
        dados.CriadoEm = atual.CriadoEm;
        dados.AtualizadoEm = atual.AtualizadoEm;

        var entrada = db.Entry(atual);
        entrada.CurrentValues.SetValues(dados);
        entrada.Property(p => p.Versao).OriginalValue = versaoAberta;
        atual.ReceberEventosDe(dados);
        entrada.Property(p => p.AtualizadoEm).IsModified = true; // confere e troca a versão mesmo sem outra mudança
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
            throw new ValidacaoException(["Já existe uma profissão com este nome (maiúsculas e acentos não contam)."]);
        }
    }
}

/// <summary>Tabela oficial da CBO. Nunca apaga: a ocupação que sai do arquivo oficial é desativada.</summary>
public class OcupacaoCboRepositorio : ServicoDadosBase, IOcupacaoCboRepositorio
{
    public OcupacaoCboRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<OcupacaoCbo>> ListarAtivasAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.OcupacoesCbo.AsNoTracking().Where(o => o.Ativo).OrderBy(o => o.Titulo).ToListAsync(ct);
    }

    public async Task<Dictionary<int, OcupacaoCbo>> ObterAsync(IReadOnlyCollection<int> codigos, CancellationToken ct)
    {
        if (codigos.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = codigos.Distinct().ToList();
        return await db.OcupacoesCbo.AsNoTracking().Where(o => lista.Contains(o.Id)).ToDictionaryAsync(o => o.Id, ct);
    }

    public async Task<(int Quantidade, DateTime? AtualizadaEm)> ObterSituacaoAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var quantidade = await db.OcupacoesCbo.CountAsync(o => o.Ativo, ct);
        var atualizadaEm = await db.OcupacoesCbo.MaxAsync(o => (DateTime?)o.AtualizadoEm, ct);
        return (quantidade, atualizadaEm);
    }

    public async Task<(int Incluidas, int Alteradas, int Desativadas)> SincronizarAsync(
        IReadOnlyCollection<OcupacaoCbo> oficiais, DateTime agoraUtc, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Origem = OrigemAlteracao.Importacao;

        var atuais = await db.OcupacoesCbo.ToDictionaryAsync(o => o.Id, ct);
        var codigosOficiais = oficiais.Select(o => o.Id).ToHashSet();
        int incluidas = 0, alteradas = 0, desativadas = 0;

        foreach (var oficial in oficiais)
        {
            if (!atuais.TryGetValue(oficial.Id, out var atual))
            {
                oficial.AtualizadoEm = agoraUtc;
                db.OcupacoesCbo.Add(oficial);
                incluidas++;
                continue;
            }

            if (atual.Titulo != oficial.Titulo || !atual.Ativo)
            {
                atual.Titulo = oficial.Titulo;
                atual.Ativo = true;
                atual.AtualizadoEm = agoraUtc;
                alteradas++;
            }
        }

        // Saiu do arquivo oficial: desativa (profissões antigas continuam apontando para ela).
        foreach (var sumida in atuais.Values.Where(o => o.Ativo && !codigosOficiais.Contains(o.Id)))
        {
            sumida.Ativo = false;
            sumida.AtualizadoEm = agoraUtc;
            desativadas++;
        }

        await db.SaveChangesAsync(ct);
        return (incluidas, alteradas, desativadas);
    }
}
