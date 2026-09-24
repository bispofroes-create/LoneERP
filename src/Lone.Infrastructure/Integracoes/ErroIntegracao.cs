using Lone.Application.Integracoes;

namespace Lone.Infrastructure.Integracoes;

internal static class ErroIntegracao
{
    public static ServicoExternoException SemConexao(string servico, Exception interna) =>
        new($"Não foi possível conectar ao serviço de {servico}. Tente de novo em instantes.", interna);

    public static ServicoExternoException RespostaInesperada(string servico, int status) =>
        new($"O serviço de {servico} respondeu com erro (HTTP {status}). Tente de novo mais tarde.");
}
