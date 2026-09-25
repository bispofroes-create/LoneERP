using Lone.Application.Colaboradores;
using Lone.Application.Seguranca;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Títulos da tabela oficial da CBO para os cargos.</summary>
public class OcupacoesDoCargo : ServicoDadosBase, IOcupacoesDoCargo
{
    public OcupacoesDoCargo(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<Dictionary<int, string>> TitulosAsync(IReadOnlyCollection<int> codigos, CancellationToken ct)
    {
        if (codigos.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = codigos.Distinct().ToList();
        return await db.OcupacoesCbo.AsNoTracking().Where(o => lista.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Titulo, ct);
    }
}
