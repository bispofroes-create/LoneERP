using Lone.Application.Comercial;
using Lone.Application.Seguranca;
using Lone.Contracts.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Grava uma raiz de agregado sem filhos: nova (Add) ou existente (valores sobre o rastreado, com a versão aberta).</summary>
internal static class GravacaoSimples
{
    public static async Task SalvarAsync<T>(LoneDbContext db, DbSet<T> conjunto, T item, bool novo, string duplicado, CancellationToken ct)
        where T : AgregadoRaiz
    {
        if (novo)
        {
            if (item.Id == Guid.Empty) item.Id = IdSequencial.Novo();
            conjunto.Add(item);
        }
        else
        {
            var atual = await conjunto.FirstOrDefaultAsync(x => x.Id == item.Id, ct) ?? throw new ConflitoDeEdicaoException();
            var versaoAberta = item.Versao;
            item.Versao = atual.Versao;
            item.CriadoEm = atual.CriadoEm;
            item.AtualizadoEm = atual.AtualizadoEm;

            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(item);
            entrada.Property(x => x.Versao).OriginalValue = versaoAberta;
            atual.ReceberEventosDe(item);
            entrada.Property(x => x.AtualizadoEm).IsModified = true;
        }

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
            throw new ValidacaoException([duplicado]);
        }
    }
}

/// <summary>Tipos de ausência (SQL Server via EF Core). Nunca apaga.</summary>
public class TipoAusenciaRepositorio : ServicoDadosBase, ITipoAusenciaRepositorio
{
    public TipoAusenciaRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<TipoAusencia>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposAusencia.AsNoTracking().ToListAsync(ct);
    }

    public async Task<TipoAusencia?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposAusencia.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var coberturas = db.CoberturasComerciais.AsNoTracking().Where(c => !c.Cancelada);
        if (somenteId is { } id) coberturas = coberturas.Where(c => c.TipoAusenciaId == id);
        return await coberturas.GroupBy(c => c.TipoAusenciaId)
            .Select(g => new { g.Key, Quantidade = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task SalvarAsync(TipoAusencia item, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        await GravacaoSimples.SalvarAsync(db, db.TiposAusencia, item, novo, "Já existe tipo de ausência com este nome.", ct);
    }
}

/// <summary>Parâmetros do módulo Comercial (um registro só, criado pela migração).</summary>
public class ParametrosComerciaisRepositorio : ServicoDadosBase, IParametrosComerciaisRepositorio
{
    public ParametrosComerciaisRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<ParametrosComerciais> ObterAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Sem o registro (banco antes da migração ou apagado à mão), valem os padrões.
        return await db.ParametrosComerciais.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ParametrosComerciais.IdUnico, ct)
               ?? new ParametrosComerciais { Id = ParametrosComerciais.IdUnico };
    }

    public async Task SalvarAsync(ParametrosComerciais parametros, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var existe = await db.ParametrosComerciais.AnyAsync(x => x.Id == ParametrosComerciais.IdUnico, ct);
        parametros.Id = ParametrosComerciais.IdUnico;
        parametros.RegistrarEvento("Parâmetros comerciais alterados.");
        await GravacaoSimples.SalvarAsync(db, db.ParametrosComerciais, parametros, novo: !existe, "Parâmetros já existem.", ct);
    }
}

/// <summary>Coberturas de ausência (SQL Server via EF Core). Nunca apaga.</summary>
public class CoberturaRepositorio : ServicoDadosBase, ICoberturaRepositorio
{
    public CoberturaRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<CoberturaComercial?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.CoberturasComerciais.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<List<CoberturaComercial>> DoTitularAsync(Guid titularId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.CoberturasComerciais.AsNoTracking().Where(x => x.TitularId == titularId).ToListAsync(ct);
    }

    public async Task<List<CoberturaComercial>> ListarAsync(DateOnly desde, bool incluirEncerradas, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consulta = db.CoberturasComerciais.AsNoTracking();
        if (!incluirEncerradas) consulta = consulta.Where(x => !x.Cancelada && x.FimEm >= desde);
        return await consulta.OrderBy(x => x.InicioEm).ThenBy(x => x.FimEm).ToListAsync(ct);
    }

    public async Task SalvarAsync(CoberturaComercial item, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        await GravacaoSimples.SalvarAsync(db, db.CoberturasComerciais, item, novo, "Esta cobertura já existe.", ct);
    }
}
