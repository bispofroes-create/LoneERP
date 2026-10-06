using System.Text.Json;
using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Domain.Comum;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lone.Infrastructure.Integracoes.Cep;

/// <summary>
/// Cache postal persistente no SQL Server (F3, tabela CacheCep). Cada operação abre o próprio contexto (fora de qualquer
/// transação de cadastro; a chamada HTTP nunca fica dentro de transação). Falha do banco é registrada no log e não muda a
/// consulta: lendo, volta "sem cache"; gravando, segue sem guardar. Duas gravações da mesma chave ao mesmo tempo: a
/// última vence (as duas são respostas válidas da fonte); a chave primária impede linha duplicada.
/// </summary>
public sealed class CachePostalCepSql : ICachePostalCep
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<LoneDbContext> _fabrica;
    private readonly ILogger<CachePostalCepSql> _log;

    public CachePostalCepSql(IDbContextFactory<LoneDbContext> fabrica, ILogger<CachePostalCepSql> log)
    {
        _fabrica = fabrica;
        _log = log;
    }

    public async Task<RespostaGuardadaCep?> ObterAsync(string chave, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _fabrica.CreateDbContextAsync(ct);
            var c = await db.CacheCep.AsNoTracking().SingleOrDefaultAsync(x => x.Chave == chave, ct);
            return c is null ? null
                : new RespostaGuardadaCep(c.Situacao, c.Fonte, Ler(c.Registros), c.LimiteAtingido, c.ConsultadoEm, c.ExpiraEm, c.UtilizavelAte);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "Cache postal de CEP: leitura falhou ({Chave}); a consulta segue sem ele.", chave);
            return null;
        }
    }

    public async Task GuardarAsync(string chave, TipoConsultaCep tipo, string? cep, RespostaGuardadaCep r, CancellationToken ct = default)
    {
        try
        {
            var registros = JsonSerializer.Serialize(r.Registros, Json);
            await using var db = await _fabrica.CreateDbContextAsync(ct);
            if (await AtualizarAsync(db, chave, tipo, cep, r, registros, ct) > 0) return;
            db.CacheCep.Add(new CacheCep
            {
                Chave = chave, Tipo = tipo, Cep = cep, Situacao = r.Situacao, Fonte = r.Fonte, ConsultadoEm = r.ConsultadoEm,
                ExpiraEm = r.ExpiraEm, UtilizavelAte = r.UtilizavelAte, LimiteAtingido = r.LimiteAtingido, Registros = registros
            });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Outra gravação da mesma chave entrou antes: atualiza (a última resposta vence).
                db.ChangeTracker.Clear();
                await AtualizarAsync(db, chave, tipo, cep, r, registros, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "Cache postal de CEP: gravação falhou ({Chave}); a consulta segue sem guardar.", chave);
        }
    }

    private static Task<int> AtualizarAsync(LoneDbContext db, string chave, TipoConsultaCep tipo, string? cep, RespostaGuardadaCep r,
                                            string registros, CancellationToken ct) =>
        db.CacheCep.Where(x => x.Chave == chave).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Tipo, tipo).SetProperty(x => x.Cep, cep).SetProperty(x => x.Situacao, r.Situacao)
            .SetProperty(x => x.Fonte, r.Fonte).SetProperty(x => x.ConsultadoEm, r.ConsultadoEm).SetProperty(x => x.ExpiraEm, r.ExpiraEm)
            .SetProperty(x => x.UtilizavelAte, r.UtilizavelAte).SetProperty(x => x.LimiteAtingido, r.LimiteAtingido)
            .SetProperty(x => x.Registros, registros), ct);

    private static IReadOnlyList<RegistroCep> Ler(string json) =>
        JsonSerializer.Deserialize<List<RegistroCep>>(json, Json) ?? [];
}

/// <summary>
/// Histórico técnico das consultas de CEP no SQL Server (F3, tabela ConsultasCep). Registrar nunca lança: falhou, fica no
/// log (sem dados da pessoa) e a consulta segue. Grava fora de qualquer transação de cadastro, mesmo se quem pediu
/// cancelou (o registro do cancelamento é curto e não usa o token dele).
/// </summary>
public sealed class HistoricoConsultasCepSql : IHistoricoConsultasCep
{
    private readonly IDbContextFactory<LoneDbContext> _fabrica;
    private readonly ILogger<HistoricoConsultasCepSql> _log;

    public HistoricoConsultasCepSql(IDbContextFactory<LoneDbContext> fabrica, ILogger<HistoricoConsultasCepSql> log)
    {
        _fabrica = fabrica;
        _log = log;
    }

    public async Task RegistrarAsync(ConsultaCepOcorrida c)
    {
        try
        {
            await using var db = await _fabrica.CreateDbContextAsync();
            db.ConsultasCep.Add(new ConsultaCep
            {
                Id = IdSequencial.Novo(), Operacao = c.Operacao, Tipo = c.Tipo, Chave = c.Chave, Cep = c.Cep, Fonte = c.Fonte,
                OcorridoEm = c.OcorridoEm, DuracaoMs = (int)Math.Min(int.MaxValue, Math.Max(0, c.Duracao.TotalMilliseconds)),
                Resultado = c.Resultado, Origem = c.Origem, LimiteAtingido = c.LimiteAtingido
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Histórico de consultas de CEP: registro falhou ({Operacao}, {Resultado}); a consulta segue.", c.Operacao, c.Resultado);
        }
    }
}
