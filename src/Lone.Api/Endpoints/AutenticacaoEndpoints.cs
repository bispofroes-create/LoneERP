using Lone.Api.Erros;
using Lone.Api.Seguranca;
using Lone.Application.Seguranca;
using Lone.Contracts.Comum;
using Lone.Contracts.Seguranca;

namespace Lone.Api.Endpoints;

/// <summary>Login, primeiro acesso, renovação, troca de empresa, troca de senha e saída.</summary>
public static class AutenticacaoEndpoints
{
    public static IEndpointRouteBuilder MapAutenticacao(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(string.Empty).WithTags("Autenticação");

        grupo.MapGet(Rotas.Autenticacao.Situacao,
                (IAutenticacaoService servico, CancellationToken ct) => servico.ObterSituacaoAsync(ct))
            .AllowAnonymous();

        grupo.MapPost(Rotas.Autenticacao.PrimeiroAcesso,
                (PrimeiroAcessoRequisicao requisicao, IAutenticacaoService servico, CancellationToken ct) =>
                    servico.CriarPrimeiroAdministradorAsync(requisicao, ct))
            .AllowAnonymous()
            .RequireRateLimiting(ConfiguracaoApi.PoliticaLogin);

        grupo.MapPost(Rotas.Autenticacao.Entrar, EntrarAsync)
            .AllowAnonymous()
            .RequireRateLimiting(ConfiguracaoApi.PoliticaLogin);

        grupo.MapPost(Rotas.Autenticacao.Renovar,
                (RenovarSessaoRequisicao requisicao, IAutenticacaoService servico, CancellationToken ct) =>
                    servico.RenovarAsync(requisicao, ct))
            .AllowAnonymous()
            .RequireRateLimiting(ConfiguracaoApi.PoliticaLogin);

        grupo.MapPost(Rotas.Autenticacao.Empresa,
                (SelecionarEmpresaRequisicao requisicao, UsuarioDaRequisicao usuario, IAutenticacaoService servico, CancellationToken ct) =>
                    servico.SelecionarEmpresaAsync(usuario.IdObrigatorio, requisicao, ct))
            .RequireAuthorization();

        grupo.MapPost(Rotas.Autenticacao.Senha, TrocarSenhaAsync)
            .RequireAuthorization();

        // Sem exigir login: o token de acesso pode já ter vencido quando o usuário sai.
        grupo.MapPost(Rotas.Autenticacao.Sair, async (SairRequisicao requisicao, IAutenticacaoService servico, CancellationToken ct) =>
            {
                await servico.SairAsync(requisicao, ct);
                return Results.NoContent();
            })
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> EntrarAsync(EntrarRequisicao requisicao, IAutenticacaoService servico, CancellationToken ct)
    {
        var resultado = await servico.EntrarAsync(requisicao, ct);
        return resultado.Sucesso
            ? Results.Ok(resultado.Sessao)
            : Problemas.Resultado(Problemas.LoginRecusado(resultado));
    }

    private static async Task<IResult> TrocarSenhaAsync(TrocarSenhaRequisicao requisicao, UsuarioDaRequisicao usuario,
                                                        IAutenticacaoService servico, CacheAcesso cache, CancellationToken ct)
    {
        await servico.TrocarSenhaAsync(usuario.IdObrigatorio, requisicao, ct);
        cache.Invalidar(); // libera o usuário que estava obrigado a trocar a senha
        return Results.NoContent();
    }
}
