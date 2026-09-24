namespace Lone.Domain.Validacao.InscricoesEstaduais;

/// <summary>
/// Regra de validação da Inscrição Estadual de uma UF.
/// </summary>
internal interface IRegraInscricaoEstadual
{
    /// <summary>
    /// Indica se a inscrição tem o formato da UF e dígitos verificadores corretos.
    /// </summary>
    /// <param name="digitos">
    /// Valor já normalizado (sem pontuação nem espaços, em maiúsculas).
    /// Para produtor rural de SP o 'P' inicial é mantido.
    /// </param>
    bool Valida(string digitos);
}
