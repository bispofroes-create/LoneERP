using Lone.Application.Fiscal;
using Lone.Application.Seguranca;
using Lone.Contracts.Pessoas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Tabela de CNAEs (SQL Server). Nunca apaga: o que sai da lista oficial é desativado.</summary>
public class CnaeRepositorio : ServicoDadosBase, ICnaeRepositorio
{
    public CnaeRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<SituacaoCnaes> ObterSituacaoAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return new SituacaoCnaes
        {
            Quantidade = await db.Cnaes.CountAsync(ct),
            AtualizadoEm = await db.Cnaes.MaxAsync(c => (DateTime?)c.AtualizadoEm, ct)
        };
    }

    public async Task<Dictionary<int, Cnae>> ObterVariosAsync(IReadOnlyCollection<int> codigos, CancellationToken ct)
    {
        if (codigos.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = codigos.Distinct().ToList();
        return await db.Cnaes.AsNoTracking().Where(c => lista.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
    }

    public async Task<List<Cnae>> BuscarAsync(string texto, int limite, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var digitos = new string(texto.Where(char.IsAsciiDigit).ToArray());
        var consulta = db.Cnaes.AsNoTracking().Where(c => c.Ativo);
        if (digitos.Length >= 2 && digitos.Length == texto.Count(char.IsLetterOrDigit))
        {
            // Código: os 7 dígitos com zeros à esquerda começam pelo que foi digitado.
            var casas = 7 - digitos.Length;
            if (casas < 0) return [];
            var inicio = int.Parse(digitos, System.Globalization.CultureInfo.InvariantCulture) * (int)Math.Pow(10, casas);
            var fim = inicio + (int)Math.Pow(10, casas);
            consulta = consulta.Where(c => c.Id >= inicio && c.Id < fim);
        }
        else
            consulta = consulta.Where(c => c.Descricao.Contains(texto));
        return await consulta.OrderBy(c => c.Id).Take(limite).ToListAsync(ct);
    }

    public async Task<(int Incluidos, int Alterados, int Desativados)> SincronizarAsync(IReadOnlyList<Cnae> oficiais, DateTime agoraUtc, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Origem = OrigemAlteracao.Sistema;
        var atuais = await db.Cnaes.ToDictionaryAsync(c => c.Id, ct);
        var codigos = oficiais.Select(o => o.Id).ToHashSet();
        int incluidos = 0, alterados = 0, desativados = 0;

        foreach (var oficial in oficiais)
        {
            if (!atuais.TryGetValue(oficial.Id, out var atual))
            {
                db.Cnaes.Add(oficial);
                incluidos++;
            }
            else if (atual.Descricao != oficial.Descricao || !atual.Ativo)
            {
                atual.Descricao = oficial.Descricao;
                atual.Ativo = true;
                atual.AtualizadoEm = agoraUtc;
                alterados++;
            }
        }
        foreach (var sumido in atuais.Values.Where(c => c.Ativo && !codigos.Contains(c.Id)))
        {
            sumido.Ativo = false;
            sumido.AtualizadoEm = agoraUtc;
            desativados++;
        }

        await db.SaveChangesAsync(ct);
        return (incluidos, alterados, desativados);
    }
}
