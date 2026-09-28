using Lone.Application.Integracoes;
using Lone.Application.Seguranca;
using Lone.Contracts.Comum;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Validacao;
using Microsoft.AspNetCore.Mvc;

namespace Lone.Api.Erros;

/// <summary>
/// Monta as respostas de erro (ProblemDetails, RFC 9457) no formato que o aplicativo entende:
/// "detail" é a mensagem para o usuário e "codigo" diz qual exceção recriar do lado do aplicativo.
/// </summary>
public static class Problemas
{
    /// <summary>Erros esperados (regra, permissão, conflito...). Nulo = erro inesperado (500).</summary>
    public static ProblemDetails? De(Exception erro) => erro switch
    {
        ValidacaoException v => Criar(StatusCodes.Status400BadRequest, "Dados inválidos", v.Message, ErrosApi.Validacao,
                                      (ErrosApi.CampoErros, v.Erros)),
        AcessoNegadoException a => Criar(StatusCodes.Status403Forbidden, "Acesso negado", a.Message, ErrosApi.AcessoNegado,
                                         (ErrosApi.CampoPermissao, a.Permissao)),
        ConflitoDeEdicaoException c => Criar(StatusCodes.Status409Conflict, "Conflito de edição", c.Message, ErrosApi.Conflito),
        SessaoInvalidaException s => NaoAutenticado(s.Message),
        // Fase 2a-2: fora do alcance responde como cadastro inexistente (não revela que existe).
        ForaDoEscopoException f => NaoEncontrado(f.Message),
        ServicoExternoException e => Criar(StatusCodes.Status502BadGateway, "Serviço externo indisponível", e.Message,
                                           ErrosApi.ServicoExterno),
        _ => null
    };

    public static ProblemDetails NaoAutenticado(string mensagem = "Entre no sistema para continuar.") =>
        Criar(StatusCodes.Status401Unauthorized, "Não autenticado", mensagem, ErrosApi.NaoAutenticado);

    public static ProblemDetails LoginRecusado(ResultadoEntrada resultado) =>
        Criar(StatusCodes.Status401Unauthorized, "Login recusado", resultado.Mensagem, ErrosApi.LoginRecusado,
              (ErrosApi.CampoSituacaoLogin, resultado.Situacao.ToString()));

    public static ProblemDetails TrocaDeSenhaObrigatoria() =>
        Criar(StatusCodes.Status403Forbidden, "Troca de senha obrigatória",
              "Troque a sua senha antes de continuar.", ErrosApi.TrocaDeSenhaObrigatoria);

    public static ProblemDetails NaoEncontrado(string mensagem) =>
        Criar(StatusCodes.Status404NotFound, "Não encontrado", mensagem, ErrosApi.NaoEncontrado);

    public static ProblemDetails MuitasTentativas() =>
        Criar(StatusCodes.Status429TooManyRequests, "Muitas tentativas",
              "Muitas tentativas em pouco tempo. Aguarde um minuto e tente de novo.", ErrosApi.Validacao,
              (ErrosApi.CampoErros, new[] { "Muitas tentativas em pouco tempo. Aguarde um minuto e tente de novo." }));

    public static ProblemDetails Validacao(string mensagem) =>
        Criar(StatusCodes.Status400BadRequest, "Dados inválidos", mensagem, ErrosApi.Validacao,
              (ErrosApi.CampoErros, new[] { mensagem }));

    /// <summary>Resposta para quem chama a API (endpoints).</summary>
    public static IResult Resultado(ProblemDetails problema) => Results.Problem(problema);

    /// <summary>Escreve a resposta direto no contexto (middlewares e eventos de autenticação).</summary>
    public static Task Escrever(HttpContext contexto, ProblemDetails problema) =>
        Results.Problem(problema).ExecuteAsync(contexto);

    private static ProblemDetails Criar(int status, string titulo, string mensagem, string codigo,
                                        params (string Nome, object Valor)[] extras)
    {
        var problema = new ProblemDetails { Status = status, Title = titulo, Detail = mensagem };
        problema.Extensions[ErrosApi.CampoCodigo] = codigo;
        foreach (var (nome, valor) in extras)
            problema.Extensions[nome] = valor;
        return problema;
    }
}
