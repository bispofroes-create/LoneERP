namespace Lone.Domain.Enums;

/// <summary>
/// Como um campo do documento (órgão emissor, UF, data de emissão) aparece para um tipo de documento.
/// Gravado no banco: nunca renumere; valores novos entram no fim.
/// </summary>
public enum UsoCampoDocumento : byte
{
    /// <summary>Não aparece (um valor já gravado continua à vista e intacto).</summary>
    Oculto = 0,
    Opcional = 1,
    Obrigatorio = 2
}

/// <summary>
/// Formato aceito para o número do documento. Lista fechada (sem expressão regular do usuário). A conferência é sobre o
/// número comparável (<see cref="Lone.Domain.Documentos.NumeroDocumento"/>). Gravado no banco: valores novos entram no fim.
/// </summary>
public enum FormatoNumeroDocumento : byte
{
    /// <summary>Qualquer texto (o comportamento de sempre).</summary>
    Livre = 0,

    /// <summary>Letras e dígitos, com pontos, hífens, barras e espaços como separadores.</summary>
    Alfanumerico = 1,

    /// <summary>Só dígitos, com pontos, hífens, barras e espaços como separadores.</summary>
    SomenteDigitos = 2
}

/// <summary>
/// O que fazer quando o mesmo número (do mesmo tipo) aparece em outra pessoa. Dentro da mesma pessoa a repetição é
/// sempre erro, seja qual for o modo. Gravado no banco: valores novos entram no fim. "Por tipo e país" fica para quando
/// houver o cadastro estruturado de países (não existe aqui de propósito, para não poder ser escolhido).
/// </summary>
public enum UnicidadeDocumento : byte
{
    /// <summary>Não verifica.</summary>
    Nenhuma = 0,

    /// <summary>Avisa a possível duplicidade, sem impedir a gravação.</summary>
    Aviso = 1,

    /// <summary>Bloqueia: o número não se repete no tipo (garantido pelo SQL Server).</summary>
    PorTipo = 2,

    /// <summary>Bloqueia: o número não se repete no tipo dentro da mesma UF (exige a UF; garantido pelo SQL Server).</summary>
    PorTipoEUf = 3
}
