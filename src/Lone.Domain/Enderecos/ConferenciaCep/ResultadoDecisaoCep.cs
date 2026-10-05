namespace Lone.Domain.Enderecos.ConferenciaCep;

/// <summary>Os seis casos da decisão do motor de CEP (plano v1.3, §7; arquitetura §4.1), mais a fonte indisponível.</summary>
public enum ResultadoDecisaoCep
{
    /// <summary>Caso 1: o CEP existe e corresponde ao endereço.</summary>
    Conferido = 1,
    /// <summary>Caso 2: o CEP existe, mas é de outro logradouro, município, UF ou faixa. Não troca.</summary>
    Divergente = 2,
    /// <summary>Caso 3: o CEP não existe. Pode levar à busca pelo endereço (feita fora do domínio).</summary>
    NaoEncontrado = 3,
    /// <summary>Caso 4: a busca achou exatamente um CEP compatível. Vira sugestão; o endereço não é alterado.</summary>
    UmCandidato = 4,
    /// <summary>Caso 5: a busca achou vários CEPs compatíveis. Vão para escolha; nenhum é escolhido.</summary>
    VariosCandidatos = 5,
    /// <summary>Caso 6: a busca não achou CEP compatível: "não localizado". Nenhum CEP é inventado.</summary>
    NenhumCandidato = 6,
    /// <summary>A fonte não respondeu. Não diz nada sobre o endereço (não o invalida).</summary>
    FonteIndisponivel = 7
}
