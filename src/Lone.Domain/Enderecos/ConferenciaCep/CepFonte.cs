namespace Lone.Domain.Enderecos.ConferenciaCep;

/// <summary>
/// De onde veio a informação do CEP (plano v1.3, §7). Fonte não é situação: a situação (resultado) é <see cref="CepSituacao"/>.
/// Na F1 é só o nome da origem; nenhum provedor é chamado aqui (provedores ficam para a F2).
/// </summary>
public enum CepFonte : byte
{
    ViaCep = 1,
    BrasilApi = 2,
    Correios = 3,
    Receita = 4,
    Usuario = 5,
    Importacao = 6,
    Integracao = 7
}
