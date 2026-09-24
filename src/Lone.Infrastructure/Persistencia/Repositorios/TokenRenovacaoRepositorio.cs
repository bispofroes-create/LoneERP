using Lone.Application.Seguranca;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

public class TokenRenovacaoRepositorio : ServicoDadosBase, ITokenRenovacaoRepositorio
{
    private readonly TimeProvider _relogio;

    public TokenRenovacaoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario, TimeProvider relogio)
        : base(fabrica, usuario)
    {
        _relogio = relogio;
    }

    public async Task<TokenRenovacao?> ObterPorHashAsync(string hash, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TokensRenovacao.AsNoTracking().FirstOrDefaultAsync(t => t.Hash == hash, ct);
    }

    public async Task IncluirAsync(TokenRenovacao token, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.TokensRenovacao.Add(token);
        await db.SaveChangesAsync(ct);
    }

    public async Task SubstituirAsync(Guid usadoId, TokenRenovacao novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        // Só revoga se ainda estava ativo: duas renovações simultâneas com o mesmo token não geram duas sessões.
        var agora = Agora();
        var revogados = await db.TokensRenovacao
            .Where(t => t.Id == usadoId && t.RevogadoEm == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevogadoEm, agora)
                .SetProperty(t => t.SubstituidoPorId, novo.Id), ct);

        if (revogados == 0)
            throw new SessaoInvalidaException();

        db.TokensRenovacao.Add(novo);
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
    }

    public async Task RevogarAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var agora = Agora();
        await db.TokensRenovacao
            .Where(t => t.Id == id && t.RevogadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevogadoEm, agora), ct);
    }

    public async Task RevogarTodosDoUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var agora = Agora();
        await db.TokensRenovacao
            .Where(t => t.UsuarioId == usuarioId && t.RevogadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevogadoEm, agora), ct);
    }

    private DateTime Agora() => _relogio.GetUtcNow().UtcDateTime;
}
