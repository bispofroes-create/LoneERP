using Lone.Application.Comercial;
using Lone.Application.Seguranca;
using Lone.Contracts.Comercial;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

/// <summary>Consultas das coberturas e da carteira vencendo (Motor Comercial, Fase 1c), contadas e filtradas no banco.</summary>
public class CoberturaConsultas : ServicoDadosBase, ICoberturaConsultas
{
    public CoberturaConsultas(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<Dictionary<Guid, int>> ContarClientesAsync(IReadOnlyCollection<CoberturaComercial> coberturas, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, int>();
        if (coberturas.Count == 0) return resultado;
        await using var db = await AbrirAsync(ct);
        foreach (var c in coberturas)
        {
            var (titular, papel, empresa, inicio, fim) = (c.TitularId, c.TipoCarteiraId, c.EmpresaId, c.InicioEm, c.FimEm);
            resultado[c.Id] = await db.CarteiraClientes.AsNoTracking()
                .Where(v => v.Ativo && v.VendedorId == titular && v.InicioEm <= fim && (v.FimEm == null || v.FimEm >= inicio) &&
                            (papel == null || v.TipoCarteiraId == papel) &&
                            (empresa == null || v.EmpresaId == null || v.EmpresaId == empresa))
                .Select(v => v.PessoaId).Distinct().CountAsync(ct);
        }
        return resultado;
    }

    public async Task<List<CarteiraCliente>> VinculosEmDataAsync(Guid? clienteId, Guid? pessoaId, DateOnly data, int limite, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Mesma condição de CarteiraCliente.Vigente(data), no banco.
        var consulta = db.CarteiraClientes.AsNoTracking()
            .Where(v => v.Ativo && v.InicioEm <= data && (v.FimEm == null || v.FimEm >= data));
        if (clienteId is { } cliente) consulta = consulta.Where(v => v.PessoaId == cliente);
        if (pessoaId is { } pessoa) consulta = consulta.Where(v => v.VendedorId == pessoa);
        return await consulta.OrderBy(v => v.TipoCarteiraId).ThenBy(v => v.InicioEm).Take(limite).ToListAsync(ct);
    }

    public async Task<List<VinculoVencendoDto>> CarteiraVencendoAsync(DateOnly de, DateOnly ate, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var linhas = await db.CarteiraClientes.AsNoTracking()
            .Where(v => v.Ativo && v.FimEm != null && v.FimEm >= de && v.FimEm <= ate &&
                        // Já trocado: outro vínculo do mesmo papel e empresa começa no dia seguinte (não precisa de ação).
                        !db.CarteiraClientes.Any(n => n.Ativo && n.Id != v.Id && n.PessoaId == v.PessoaId && n.TipoCarteiraId == v.TipoCarteiraId &&
                                                      n.EmpresaId == v.EmpresaId && n.InicioEm == v.FimEm.Value.AddDays(1)))
            .Select(v => new
            {
                v.Id,
                v.PessoaId,
                Cliente = db.Pessoas.Where(p => p.Id == v.PessoaId).Select(p => p.NomeExibicao ?? p.Nome).FirstOrDefault(),
                Papel = db.TiposCarteira.Where(t => t.Id == v.TipoCarteiraId).Select(t => t.Nome).FirstOrDefault(),
                Pessoa = db.Pessoas.Where(p => p.Id == v.VendedorId).Select(p => p.NomeExibicao ?? p.Nome).FirstOrDefault(),
                Empresa = v.EmpresaId == null ? null : db.Pessoas.Where(p => p.Id == v.EmpresaId).Select(p => p.NomeExibicao ?? p.Nome).FirstOrDefault(),
                v.InicioEm,
                FimEm = v.FimEm!.Value
            })
            .OrderBy(x => x.FimEm).ThenBy(x => x.Cliente)
            .ToListAsync(ct);
        return [.. linhas.Select(l => new VinculoVencendoDto
        {
            VinculoId = l.Id,
            ClienteId = l.PessoaId,
            Cliente = l.Cliente ?? "?",
            Papel = l.Papel ?? "?",
            Pessoa = l.Pessoa ?? "?",
            Empresa = l.Empresa,
            InicioEm = l.InicioEm,
            FimEm = l.FimEm,
            DiasRestantes = l.FimEm.DayNumber - de.DayNumber
        })];
    }

    public async Task<Dictionary<Guid, string>> NomesEquipesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.Equipes.AsNoTracking().Where(e => lista.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Nome, ct);
    }
}
