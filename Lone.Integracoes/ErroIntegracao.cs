namespace Lone.Integracoes;

internal static class ErroIntegracao
{
    public static InvalidOperationException SemConexao(string servico, Exception interna) =>
        new($"Não foi possível conectar ao serviço de {servico}. Verifique a internet e tente de novo.", interna);

    public static InvalidOperationException RespostaInesperada(string servico, int status) =>
        new($"O serviço de {servico} respondeu com erro (HTTP {status}). Tente de novo mais tarde.");
}
