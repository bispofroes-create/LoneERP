namespace Lone.Domain.Enderecos.ConferenciaCep;

/// <summary>
/// O resultado da conferência do CEP de um endereço (arquitetura §3.7; plano v1.3, §7). Situação não é fonte: de onde veio a
/// informação é <see cref="CepFonte"/>. Na F1 nada é gravado: a situação só sai na <see cref="DecisaoCep"/>.
/// </summary>
public enum CepSituacao : byte
{
    NaoConferido = 0,
    Conferido = 1,
    NaoEncontrado = 2,
    /// <summary>
    /// O usuário aplicou a sugestão e gravou. O motor nunca devolve esta situação: ele não troca o CEP (quem troca é a ficha,
    /// a partir da F2).
    /// </summary>
    Corrigido = 3,
    Divergente = 4,
    MultiplosCandidatos = 5,
    FonteIndisponivel = 6,
    /// <summary>Há uma sugestão de CEP ainda não aceita pelo usuário.</summary>
    PendenteDeDecisao = 7
}
