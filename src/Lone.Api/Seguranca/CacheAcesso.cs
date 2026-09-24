using Lone.Application.Seguranca;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Lone.Api.Seguranca;

/// <summary>
/// Guarda por 30 segundos as permissões calculadas de cada usuário/empresa, para não consultar o banco
/// em toda requisição. Qualquer alteração de usuário, perfil ou senha limpa tudo na hora (Invalidar).
/// </summary>
public sealed class CacheAcesso
{
    private static readonly TimeSpan Validade = TimeSpan.FromSeconds(30);

    private readonly IMemoryCache _cache;
    private readonly Lock _trava = new();
    private CancellationTokenSource _geracao = new();

    public CacheAcesso(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<AcessoDoUsuario?> ObterAsync(Guid usuarioId, Guid? empresaId, IAcessoService servico, CancellationToken ct)
    {
        var chave = (nameof(CacheAcesso), usuarioId, empresaId);
        if (_cache.TryGetValue(chave, out AcessoDoUsuario? guardado))
            return guardado;

        var acesso = await servico.CarregarAsync(usuarioId, empresaId, ct);
        if (acesso is not null)
        {
            CancellationToken geracao;
            lock (_trava) geracao = _geracao.Token;

            _cache.Set(chave, acesso, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(Validade)
                .AddExpirationToken(new CancellationChangeToken(geracao)));
        }
        return acesso;
    }

    /// <summary>Descarta todas as permissões guardadas (chamado depois de salvar usuário, perfil ou senha).</summary>
    public void Invalidar()
    {
        CancellationTokenSource anterior;
        lock (_trava)
        {
            anterior = _geracao;
            _geracao = new CancellationTokenSource();
        }
        anterior.Cancel();
        anterior.Dispose();
    }
}
