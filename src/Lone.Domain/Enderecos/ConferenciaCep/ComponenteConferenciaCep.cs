namespace Lone.Domain.Enderecos.ConferenciaCep;

/// <summary>As partes do endereço que a conferência do CEP avalia, uma a uma. Bairro e complemento não entram.</summary>
public enum ComponenteCep
{
    /// <summary>O CEP informado existe na fonte consultada (sozinho, não confirma o endereço).</summary>
    Cep = 1,
    Uf = 2,
    /// <summary>Pelo código IBGE quando os dois lados têm; senão, pelo nome.</summary>
    Municipio = 3,
    Logradouro = 4,
    /// <summary>O número do endereço contra a faixa, o lado (par/ímpar) ou o número do prédio do CEP.</summary>
    Numero = 5
}

/// <summary>
/// Como ficou cada componente na conferência. Descreve a regra do <see cref="MotorCep"/>, não a redefine: "não validável" e
/// "não informado" nunca são divergência (ausência de evidência não é evidência de erro).
/// </summary>
public enum SituacaoComponenteCep
{
    /// <summary>Há evidência e os valores são compatíveis.</summary>
    Confirmado = 1,
    /// <summary>Há evidência e os valores são incompatíveis. É o que torna o resultado geral <see cref="ResultadoDecisaoCep.Divergente"/>.</summary>
    Divergente = 2,
    /// <summary>O dado existe, mas a fonte ou a regra não permite validá-lo (S/N, número ilegível, faixa não interpretada, fonte sem resposta).</summary>
    NaoValidavel = 3,
    /// <summary>Um dos lados não trouxe o dado (endereço sem UF, CEP geral sem logradouro, CEP sem faixa de numeração).</summary>
    NaoInformado = 4
}

/// <summary>Um componente avaliado, com o motivo em texto para o usuário (o estado vem do enum, nunca do texto).</summary>
public sealed record ConferenciaComponenteCep(ComponenteCep Componente, SituacaoComponenteCep Situacao, string Motivo);
