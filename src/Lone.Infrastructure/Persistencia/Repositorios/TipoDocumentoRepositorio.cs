using Lone.Application.Documentos;
using Lone.Application.Pessoas;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Tipos de documento (SQL Server via EF Core). Nunca apaga tipos.</summary>
public class TipoDocumentoRepositorio : ServicoDadosBase, ITipoDocumentoRepositorio
{
    public TipoDocumentoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<TipoDocumentoCadastro>> ListarAsync(bool incluirInativos, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consulta = db.TiposDocumento.AsNoTracking();
        if (!incluirInativos) consulta = consulta.Where(t => t.Ativo);
        return await consulta.OrderBy(t => t.Ordem).ThenBy(t => t.Nome).ToListAsync(ct);
    }

    public async Task<TipoDocumentoCadastro?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposDocumento.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Dictionary<Guid, TipoDocumentoCadastro>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.TiposDocumento.AsNoTracking().Where(t => lista.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
    }

    public async Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposDocumento.AnyAsync(t => t.Nome == nome && t.Id != ignorarId, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteTipoId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var documentos = db.PessoaDocumentos.AsNoTracking().Where(d => d.Ativo);
        if (somenteTipoId is { } id) documentos = documentos.Where(d => d.TipoDocumentoId == id);
        return await documentos.GroupBy(d => d.TipoDocumentoId)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task<int> ProximaOrdemAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return (await db.TiposDocumento.MaxAsync(t => (int?)t.Ordem, ct) ?? 0) + 1;
    }

    public async Task<List<DocumentoEmOutraPessoa>> BuscarIguaisEmOutrasPessoasAsync(Guid pessoaId, IReadOnlyCollection<Guid> tipos,
                                                                                     IReadOnlyCollection<string> numerosComparaveis, CancellationToken ct)
    {
        if (tipos.Count == 0 || numerosComparaveis.Count == 0) return [];
        await using var db = await AbrirAsync(ct);
        var listaTipos = tipos.ToList();
        var numeros = numerosComparaveis.Where(n => n.Length > 0).ToList();
        // Pelo índice (NumeroNormalizado, TipoDocumentoId); só ativos; sem escopo aqui (o serviço filtra o que mostra).
        var linhas = await db.PessoaDocumentos.AsNoTracking()
            .Where(d => d.Ativo && d.PessoaId != pessoaId && numeros.Contains(d.NumeroNormalizado) && listaTipos.Contains(d.TipoDocumentoId))
            // Sem escopo: de propósito — o serviço decide o que mostrar (quem está fora do alcance nunca é identificado).
            .Join(db.Pessoas.AsNoTracking(), d => d.PessoaId, p => p.Id,
                  (d, p) => new { d.TipoDocumentoId, d.NumeroNormalizado, d.Uf, p.Id, p.Codigo, p.Nome })
            // V2-0.2: ordem estável antes do corte (o mesmo documento repetido sempre traz as mesmas pessoas, sem o aviso 10102).
            .OrderBy(x => x.Codigo).ThenBy(x => x.Id).ThenBy(x => x.TipoDocumentoId).ThenBy(x => x.NumeroNormalizado).ThenBy(x => x.Uf)
            .Take(50)
            .ToListAsync(ct);
        return linhas.Select(l => new DocumentoEmOutraPessoa(l.TipoDocumentoId, l.NumeroNormalizado, l.Uf, new PessoaIdentificacao(l.Id, l.Codigo, l.Nome)))
            .ToList();
    }

    /// <summary>Índice único da chave de unicidade dos documentos (P1-8).</summary>
    public const string IndiceChaveUnicidade = "IX_PessoaDocumentos_ChaveUnicidade";

    /// <summary>
    /// Expressão SQL da chave de unicidade de cada documento do tipo — a mesma regra de NumeroDocumento.ChaveUnicidade
    /// (o prefixo é o Id do tipo no formato "N", montado em C#; sem número comparável ou sem a UF exigida: nula).
    /// </summary>
    private const string ExpressaoChave = """
        CASE WHEN d.NumeroNormalizado = '' THEN NULL
             WHEN @modo = 2 THEN CONCAT(@prefixo, '|', d.NumeroNormalizado)
             WHEN @modo = 3 AND d.Uf IS NOT NULL AND LEN(d.Uf) = 2 THEN CONCAT(@prefixo, '|', UPPER(d.Uf), '|', d.NumeroNormalizado)
             ELSE NULL END
        """;

    public const string NaoPodeBloquear =
        "Não é possível bloquear número repetido neste tipo: há {0} número(s) repetido(s) entre documentos ativos de pessoas diferentes " +
        "(ou na mesma pessoa). Corrija esses cadastros antes; nada foi alterado.";

    public async Task SalvarAsync(TipoDocumentoCadastro tipo, bool novo, bool recalcularChaves, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);

        if (novo)
        {
            if (tipo.Id == Guid.Empty) tipo.Id = IdSequencial.Novo();
            db.TiposDocumento.Add(tipo);
        }
        else
        {
            var atual = await db.TiposDocumento.FirstOrDefaultAsync(t => t.Id == tipo.Id, ct) ?? throw new ConflitoDeEdicaoException();
            var versaoAberta = tipo.Versao;
            tipo.Versao = atual.Versao;
            tipo.CriadoEm = atual.CriadoEm;
            tipo.AtualizadoEm = atual.AtualizadoEm;

            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(tipo);
            entrada.Property(t => t.Versao).OriginalValue = versaoAberta;
            atual.ReceberEventosDe(tipo);
            entrada.Property(t => t.AtualizadoEm).IsModified = true;
        }

        try
        {
            if (!recalcularChaves)
            {
                await db.SaveChangesAsync(ct);
                return;
            }

            await using var transacao = await db.Database.BeginTransactionAsync(ct);
            await db.SaveChangesAsync(ct);
            var parametros = new object[]
            {
                new SqlParameter("@tipo", tipo.Id),
                new SqlParameter("@modo", (int)tipo.Unicidade),
                new SqlParameter("@prefixo", System.Data.SqlDbType.VarChar, 32) { Value = tipo.Id.ToString("N") }
            };
            if (Lone.Domain.Documentos.RegrasDocumento.Bloqueia(tipo.Unicidade))
            {
                // Diagnóstico específico antes de ligar o bloqueio: chaves repetidas entre os ATIVOS do tipo.
                var repetidos = await db.Database.SqlQueryRaw<int>($"""
                    SELECT COUNT(*) AS Value FROM (
                        SELECT {ExpressaoChave} AS Chave FROM PessoaDocumentos d WHERE d.TipoDocumentoId = @tipo AND d.Ativo = 1
                    ) x WHERE x.Chave IS NOT NULL GROUP BY x.Chave HAVING COUNT(*) > 1
                    """, CopiaDe(parametros)).ToListAsync(ct);
                if (repetidos.Count > 0)
                    throw new ValidacaoException([string.Format(System.Globalization.CultureInfo.InvariantCulture, NaoPodeBloquear, repetidos.Count)]);
            }
            await db.Database.ExecuteSqlRawAsync(
                $"UPDATE d SET ChaveUnicidade = {ExpressaoChave} FROM PessoaDocumentos d WHERE d.TipoDocumentoId = @tipo",
                CopiaDe(parametros), ct);
            await transacao.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoDeEdicaoException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql && !sql.Message.Contains(IndiceChaveUnicidade))
        {
            throw new ValidacaoException(["Já existe um tipo de documento com este nome."]);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627 && ex.Message.Contains(IndiceChaveUnicidade, StringComparison.Ordinal))
        {
            // Outro usuário gravou um número repetido entre a conferência e o bloqueio: nada fica.
            throw new ValidacaoException([string.Format(System.Globalization.CultureInfo.InvariantCulture, NaoPodeBloquear, 1)]);
        }
    }

    /// <summary>SqlParameter não pode ser reusado entre comandos: uma cópia por comando.</summary>
    private static object[] CopiaDe(object[] parametros) =>
        parametros.Cast<SqlParameter>().Select(p => (object)new SqlParameter(p.ParameterName, p.SqlDbType, p.Size) { Value = p.Value }).ToArray();
}
