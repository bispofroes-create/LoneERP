using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enderecos;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Entidades;
using CepValor = Lone.Domain.ObjetosDeValor.Cep;

namespace Lone.Application.Pessoas;

/// <summary>
/// Procedência do CEP aplicado a partir de uma sugestão da conferência (DM3). A alteração continua sendo do usuário
/// (<c>Origem = Usuario</c>, <c>Motivo</c> dele); a fonte da sugestão vira uma frase de evento na mesma gravação. A marca
/// enviada pelo aplicativo só é aceita quando é coerente: endereço ativo no Brasil; CEP salvo = CEP sugerido; o CEP mudou
/// nesta gravação; e a API emitiu essa sugestão (<see cref="SugestoesCepEmitidas"/>). Incoerente: ignorada, sem erro (a
/// gravação segue pelas regras normais). Nunca altera o endereço.
/// </summary>
public static class ProcedenciaSugestaoCep
{
    /// <summary>As frases de evento para os endereços com marca coerente (na ordem dos endereços).</summary>
    public static List<string> Frases(IEnumerable<EnderecoDto> enviados, IReadOnlyCollection<PessoaEndereco> atuais,
                                      IReadOnlyCollection<PessoaEndereco>? gravados, Func<string, string, CepFonte, bool> foiEmitida)
    {
        var frases = new List<string>();
        foreach (var dto in enviados)
        {
            if (dto.SugestaoCepAplicada is not { } marca) continue;
            var atual = atuais.FirstOrDefault(e => e.Id == dto.Id);
            var gravado = gravados?.FirstOrDefault(e => e.Id == dto.Id);
            if (atual is not null && Coerente(marca, atual, gravado, foiEmitida))
                frases.Add(Frase(marca, atual));
        }
        return frases;
    }

    /// <summary>A marca bate com o que vai ser gravado e com uma sugestão que a API emitiu?</summary>
    public static bool Coerente(SugestaoCepAplicadaDto marca, PessoaEndereco atual, PessoaEndereco? gravado,
                                Func<string, string, CepFonte, bool> foiEmitida)
    {
        if (!Enum.IsDefined(marca.Fonte)) return false;
        if (!CepValor.TentarCriar(marca.CepSugerido, out var sugerido)) return false;
        // CEP conferido vazio = o CEP veio da busca pelo endereço sem CEP (Checkpoint D). Preenchido, precisa ser um CEP válido
        // e diferente do sugerido (conferência). Texto que não é CEP nem vazio continua incoerente.
        var conferidoValor = string.Empty;
        if (!PelaBuscaPorEndereco(marca))
        {
            if (!CepValor.TentarCriar(marca.CepConferido, out var conferido)) return false;
            if (sugerido!.Valor == conferido!.Valor) return false;
            conferidoValor = conferido.Valor;
        }
        if (!atual.Ativo || !atual.EhBrasil) return false;
        if (DuplicidadeEndereco.Cep(atual.Cep) != sugerido!.Valor) return false;                // salvo = sugerido
        if (gravado is not null && DuplicidadeEndereco.Cep(gravado.Cep) == sugerido.Valor) return false; // não mudou agora
        return foiEmitida(conferidoValor, sugerido.Valor, marca.Fonte);                          // a API emitiu
    }

    /// <summary>A marca é de um CEP escolhido na busca pelo endereço sem CEP (CEP conferido vazio).</summary>
    public static bool PelaBuscaPorEndereco(SugestaoCepAplicadaDto marca) => string.IsNullOrWhiteSpace(marca.CepConferido);

    public static string Frase(SugestaoCepAplicadaDto marca, PessoaEndereco atual) => PelaBuscaPorEndereco(marca)
        ? $"Endereço '{DuplicidadeEndereco.Resumo(atual)}': CEP {Formatar(marca.CepSugerido)} escolhido pelo usuário entre os " +
          $"candidatos da busca de CEP pelo endereço (fonte: {NomeFonte(marca.Fonte)})."
        : $"Endereço '{DuplicidadeEndereco.Resumo(atual)}': CEP {Formatar(marca.CepConferido)} → {Formatar(marca.CepSugerido)} " +
          $"aplicado pelo usuário a partir da sugestão da conferência de CEP (fonte: {NomeFonte(marca.Fonte)}).";

    public static string NomeFonte(CepFonte fonte) => fonte switch
    {
        CepFonte.ViaCep => "ViaCEP",
        CepFonte.BrasilApi => "BrasilAPI",
        CepFonte.Correios => "Correios",
        CepFonte.Receita => "Receita",
        CepFonte.Usuario => "usuário",
        CepFonte.Importacao => "importação",
        CepFonte.Integracao => "integração",
        _ => fonte.ToString()
    };

    private static string Formatar(string cep) => CepValor.TentarCriar(cep, out var c) ? c!.Formatado : cep;
}
