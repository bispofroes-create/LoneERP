using System.Security.Claims;
using Lone.Api.Erros;
using Lone.Application.Seguranca;
using Lone.Contracts.Comum;
using Lone.Infrastructure.Seguranca;

namespace Lone.Api.Seguranca;

/// <summary>
/// Depois que o token foi validado: confere se o usuário ainda existe e está ativo, e carrega as
/// permissões dele na empresa do token. Usuário que precisa trocar a senha só acessa a autenticação.
/// </summary>
public sealed class CarregarUsuarioMiddleware
{
    private static readonly PathString CaminhoAutenticacao = "/" + Rotas.Autenticacao.Grupo;

    private readonly RequestDelegate _proximo;

    public CarregarUsuarioMiddleware(RequestDelegate proximo)
    {
        _proximo = proximo;
    }

    public async Task InvokeAsync(HttpContext contexto, UsuarioDaRequisicao usuario, CacheAcesso cache, IAcessoService acessos)
    {
        if (contexto.User.Identity?.IsAuthenticated == true)
        {
            var usuarioId = LerGuid(contexto.User, ClaimsLone.Usuario);
            var empresaId = LerGuid(contexto.User, ClaimsLone.Empresa);
            var estabelecimentoId = LerGuid(contexto.User, ClaimsLone.Estabelecimento);

            var acesso = usuarioId is Guid id ? await cache.ObterAsync(id, empresaId, acessos, contexto.RequestAborted) : null;
            if (acesso is null)
            {
                await Problemas.Escrever(contexto, Problemas.NaoAutenticado("Sua sessão não é mais válida. Entre novamente."));
                return;
            }

            usuario.Definir(acesso, empresaId, estabelecimentoId);

            if (acesso.DeveTrocarSenha && !contexto.Request.Path.StartsWithSegments(CaminhoAutenticacao))
            {
                await Problemas.Escrever(contexto, Problemas.TrocaDeSenhaObrigatoria());
                return;
            }
        }

        await _proximo(contexto);
    }

    private static Guid? LerGuid(ClaimsPrincipal usuario, string tipo) =>
        Guid.TryParse(usuario.FindFirst(tipo)?.Value, out var valor) ? valor : null;
}
