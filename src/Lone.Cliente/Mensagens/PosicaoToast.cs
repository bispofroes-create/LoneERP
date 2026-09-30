namespace Lone.Cliente.Mensagens;

/// <summary>
/// Onde a camada global de mensagens fica na janela (regra pura, sem MAUI, para ser testada; quem aplica é a CamadaMensagens).
/// Computador: canto inferior direito, 400 de largura (em área estreita: a largura dela menos margens de 16), acima da barra de ações fixa das
/// fichas. Celular: embaixo, na largura toda, com margem de 16 dos lados.
/// </summary>
/// <param name="Largura">Nulo = ocupa a largura disponível (celular).</param>
public sealed record PosicaoToast(double? Largura, bool AlinhadaADireita, double MargemEsquerda, double MargemDireita, double MargemInferior)
{
    public const double LarguraComputador = 400;
    public const double MargemLateralComputador = 24;
    public const double MargemLateralCelular = 16;

    /// <summary>Acima da barra de ações fixa das fichas (Salvar/Descartar/Fechar: botão de 44 + 12 em cima e embaixo, + folga).</summary>
    public const double MargemInferiorPadrao = 80;

    public static PosicaoToast Calcular(double larguraArea, bool celular)
    {
        if (celular)
            return new(null, false, MargemLateralCelular, MargemLateralCelular, MargemInferiorPadrao);

        // Área estreita (menos que o toast + as margens normais): margens de celular, para o texto ganhar largura e não
        // quebrar palavras no meio.
        var margem = larguraArea < LarguraComputador + 2 * MargemLateralComputador ? MargemLateralCelular : MargemLateralComputador;
        var largura = Math.Max(0, Math.Min(LarguraComputador, larguraArea - 2 * margem));
        return new(largura, true, 0, margem, MargemInferiorPadrao);
    }
}
