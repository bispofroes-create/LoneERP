using Lone.Application.Documentos;
using Lone.Application.Seguranca;

namespace Lone.Api.Seguranca;

/// <summary>
/// Escopo de acesso por registro nas rotas de Pessoas (Motor Comercial, Fase 2a-2), aplicado ao grupo inteiro: toda rota
/// com o id de uma pessoa (<see cref="ParametrosDaPessoa"/>) confere o alcance do usuário antes do endpoint (abrir,
/// gravar, desativar, histórico, privacidade, relacionamentos, anexos...). Fora do alcance, a resposta é a mesma de um
/// cadastro que não existe (<see cref="ForaDoEscopoException"/> → 404), e com alcance restrito o id inexistente também
/// recebe essa resposta, menos na gravação (PUT {id}), que cria o cadastro novo com o id do aparelho. Rota nova no grupo já
/// nasce protegida; o teste de arquitetura confere que nenhuma rota usa outro nome para o id da pessoa.
/// </summary>
public sealed class FiltroEscopoPessoa : IEndpointFilter
{
    /// <summary>Nomes de parâmetro de rota que, no grupo de Pessoas, sempre são o id de uma pessoa.</summary>
    public static readonly IReadOnlyList<string> ParametrosDaPessoa = ["id", "pessoaId"];

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
    {
        var http = contexto.HttpContext;
        foreach (var nome in ParametrosDaPessoa)
            if (http.Request.RouteValues.TryGetValue(nome, out var valor) && Guid.TryParse(valor?.ToString(), out var id))
                await http.RequestServices.GetRequiredService<IEscopoPessoas>().ExigirAsync(id, GravacaoDaFicha(http), http.RequestAborted);
        return await proximo(contexto);
    }

    /// <summary>A gravação da ficha (PUT pessoas/{id}): a única rota em que o id pode ainda não existir (cadastro novo).</summary>
    private static bool GravacaoDaFicha(HttpContext http) =>
        HttpMethods.IsPut(http.Request.Method) &&
        (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText?.TrimEnd('/').EndsWith("{id:guid}", StringComparison.Ordinal) == true;
}

/// <summary>Rotas de anexos: o {id} é o do anexo; vale o alcance sobre a pessoa dona dele.</summary>
public sealed class FiltroEscopoAnexo : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
    {
        var http = contexto.HttpContext;
        if (http.Request.RouteValues.TryGetValue("id", out var valor) && Guid.TryParse(valor?.ToString(), out var id) &&
            await http.RequestServices.GetRequiredService<IAnexoRepositorio>().ObterAsync(id, http.RequestAborted) is { } anexo)
            await http.RequestServices.GetRequiredService<IEscopoPessoas>().ExigirAsync(anexo.PessoaId, ct: http.RequestAborted);
        return await proximo(contexto);
    }
}
