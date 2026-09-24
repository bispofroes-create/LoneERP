using Lone.Application.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Servicos;

/// <summary>Base dos repositórios e consultas: abre um contexto por operação, já com o usuário da auditoria.</summary>
public abstract class ServicoDadosBase
{
    private readonly IDbContextFactory<LoneDbContext> _fabrica;
    private readonly IUsuarioAtual _usuario;

    protected ServicoDadosBase(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
    {
        _fabrica = fabrica;
        _usuario = usuario;
    }

    protected async Task<LoneDbContext> AbrirAsync(CancellationToken ct)
    {
        var db = await _fabrica.CreateDbContextAsync(ct);
        db.Usuario = _usuario.Nome;
        return db;
    }
}
