namespace Lone.Domain.Enums;

/// <summary>
/// Base legal do tratamento (LGPD, art. 7º). A finalidade é que é cadastro; a base legal é uma lista da própria lei.
/// Nesta fase só "Consentimento" tem regra no Lone (<see cref="Lone.Domain.Privacidade.RegrasComunicacao"/>): as demais estão
/// modeladas para o futuro e, enquanto não tiverem regra, a comunicação responde "sem base legal aplicável".
/// Os números são a identidade gravada: nunca reaproveitar.
/// </summary>
public enum BaseLegal : byte
{
    NaoDefinida = 0,
    Consentimento = 1,
    ExecucaoContrato = 2,
    ObrigacaoLegal = 3,
    LegitimoInteresse = 4,
    ExercicioRegularDireitos = 5
}

/// <summary>
/// Classificação de uso que uma finalidade exige do canal (ex.: Marketing exige o e-mail marcado "Uso para marketing").
/// É classificação, nunca autorização. Nesta fase só existe Marketing, e só no e-mail.
/// </summary>
public enum ClassificacaoCanal : byte
{
    Nenhuma = 0,
    Marketing = 1
}

/// <summary>Situação do consentimento de uma pessoa para uma finalidade (e canal). "Não informado" não é "Revogado".</summary>
public enum SituacaoConsentimento : byte
{
    /// <summary>Não há registro: não se pode afirmar que houve consentimento.</summary>
    NaoInformado = 0,
    /// <summary>Há um período em vigor (concedido e ainda não revogado).</summary>
    Concedido = 1,
    /// <summary>Houve consentimento e ele foi retirado (nenhum período em vigor).</summary>
    Revogado = 2,
    /// <summary>A base legal da finalidade não é consentimento.</summary>
    NaoAplicavel = 3
}

/// <summary>Resultado da regra central de comunicação (<see cref="Lone.Domain.Privacidade.RegrasComunicacao.PodeComunicar"/>).</summary>
public enum ResultadoComunicacao : byte
{
    Permitido = 0,
    BloqueadoPorConsentimento = 1,
    BloqueadoPeloCanal = 2,
    BloqueadoPorFinalidade = 3,
    BloqueadoPorCanalInativo = 4,
    BloqueadoPorPessoaInativa = 5,
    SemBaseLegalAplicavel = 6
}
