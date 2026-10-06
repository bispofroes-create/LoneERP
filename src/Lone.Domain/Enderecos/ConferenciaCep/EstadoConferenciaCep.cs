using Lone.Domain.Entidades;

namespace Lone.Domain.Enderecos.ConferenciaCep;

/// <summary>Uma conferência que a API fez e que pode sustentar o estado gravado do endereço (F3).</summary>
public sealed record ConferenciaCepRealizada(CepSituacao Situacao, CepFonte Fonte, DateTime ConferidoEm);

/// <summary>
/// Estado persistido da conferência do CEP no endereço (F3). Regras únicas, usadas pela API no Salvar e pela ficha para
/// saber se o estado gravado ainda vale para o que está na tela:
/// <list type="bullet">
/// <item>só resultado com conclusão vira estado (<see cref="SituacaoPersistida"/>); fonte indisponível não;</item>
/// <item>o estado gravado vale enquanto os dados que participaram da conferência (CEP, UF, município, logradouro, número,
/// bairro) não mudarem (<see cref="Assinatura(EnderecoConferenciaCep)"/>); complemento, descrição e observações não contam;</item>
/// <item>mudou sem nova conferência: <see cref="CepSituacao.NaoConferido"/>, sem fonte e sem data (nada é presumido).</item>
/// </list>
/// Nunca altera o endereço em si (CEP, logradouro...): só as três colunas de conferência.
/// </summary>
public static class EstadoConferenciaCep
{
    /// <summary>
    /// Resultado do motor → situação persistida. O CEP informado não existe (com ou sem candidatos) = NaoEncontrado: os
    /// candidatos são da tela, não do cadastro. Fonte indisponível = nulo (não é conclusão; o estado anterior fica).
    /// </summary>
    public static CepSituacao? SituacaoPersistida(ResultadoDecisaoCep resultado) => resultado switch
    {
        ResultadoDecisaoCep.Conferido => CepSituacao.Conferido,
        ResultadoDecisaoCep.Divergente => CepSituacao.Divergente,
        ResultadoDecisaoCep.NaoEncontrado or ResultadoDecisaoCep.NenhumCandidato or ResultadoDecisaoCep.UmCandidato
            or ResultadoDecisaoCep.VariosCandidatos => CepSituacao.NaoEncontrado,
        _ => null
    };

    /// <summary>
    /// Os dados que participam da conferência, comparáveis (CEP e IBGE só dígitos; textos sem caixa, acento e pontuação).
    /// O número fica como texto ("S/N" ≠ vazio), para "Sem número" contar como mudança.
    /// </summary>
    public static string Assinatura(EnderecoConferenciaCep e) => string.Join('|',
        DuplicidadeEndereco.Cep(e.Cep), DuplicidadeEndereco.Texto(e.Uf), DuplicidadeEndereco.Cep(e.CodigoMunicipioIbge),
        DuplicidadeEndereco.Texto(e.Cidade), DuplicidadeEndereco.Texto(e.Logradouro), DuplicidadeEndereco.Texto(e.Numero),
        DuplicidadeEndereco.Texto(e.Bairro));

    /// <summary>A assinatura do endereço gravado ou a gravar; nula no exterior (não há conferência de CEP).</summary>
    public static string? Assinatura(PessoaEndereco e) => e.EhBrasil
        ? Assinatura(new EnderecoConferenciaCep(e.Cep, e.Logradouro, e.Numero, e.Bairro, e.Cidade, e.Uf,
            e.MunicipioId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? e.CodigoMunicipioIbge))
        : null;

    /// <summary>Os dados conferíveis mudaram entre o gravado e o novo (exterior conta como mudança).</summary>
    public static bool DadosMudaram(PessoaEndereco gravado, PessoaEndereco novo) =>
        Assinatura(gravado) is not { } antes || Assinatura(novo) != antes;

    /// <summary>
    /// Define as três colunas do endereço a gravar: a <paramref name="conferencia"/> feita para exatamente estes dados (a
    /// API confere antes de passar), senão o estado gravado se os dados não mudaram, senão NaoConferido.
    /// </summary>
    public static void Aplicar(PessoaEndereco novo, PessoaEndereco? gravado, ConferenciaCepRealizada? conferencia)
    {
        ArgumentNullException.ThrowIfNull(novo);
        if (!novo.EhBrasil) Limpar(novo);
        else if (conferencia is not null)
        {
            novo.CepSituacao = conferencia.Situacao;
            novo.CepFonte = conferencia.Fonte;
            novo.CepConferidoEm = conferencia.ConferidoEm;
        }
        else if (gravado is not null && !DadosMudaram(gravado, novo))
        {
            novo.CepSituacao = gravado.CepSituacao;
            novo.CepFonte = gravado.CepFonte;
            novo.CepConferidoEm = gravado.CepConferidoEm;
        }
        else Limpar(novo);
    }

    private static void Limpar(PessoaEndereco e)
    {
        e.CepSituacao = CepSituacao.NaoConferido;
        e.CepFonte = null;
        e.CepConferidoEm = null;
    }
}
