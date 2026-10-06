using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Domain.Contatos;

/// <summary>Os tipos iniciais de telefone e e-mail (nascem com a base, com Ids fixos; o usuário pode criar outros).</summary>
public static class TiposMeioContatoIniciais
{
    public static IReadOnlyList<(Guid Id, CategoriaMeioContato Categoria, string Nome, int Ordem)> Todos { get; } =
    [
        (new Guid("7a9e1c01-0000-0000-0000-000000000001"), CategoriaMeioContato.Telefone, "Comercial", 1),
        (new Guid("7a9e1c01-0000-0000-0000-000000000002"), CategoriaMeioContato.Telefone, "Residencial", 2),
        (new Guid("7a9e1c01-0000-0000-0000-000000000003"), CategoriaMeioContato.Telefone, "Pessoal", 3),
        (new Guid("7a9e1c01-0000-0000-0000-000000000004"), CategoriaMeioContato.Email, "Comercial", 1),
        (new Guid("7a9e1c01-0000-0000-0000-000000000005"), CategoriaMeioContato.Email, "Pessoal", 2)
    ];
}

/// <summary>Regras de telefones/e-mails da pessoa e do cadastro de tipos. Não acessa banco.</summary>
public static class RegrasMeioContato
{
    public const int TamanhoMaximoRamal = 10;

    /// <summary>Categoria do meio (nula para "outro", que não tem tipo do cadastro).</summary>
    public static CategoriaMeioContato? Categoria(TipoContato tipo) => tipo switch
    {
        TipoContato.Email => CategoriaMeioContato.Email,
        TipoContato.Telefone or TipoContato.Celular or TipoContato.WhatsApp => CategoriaMeioContato.Telefone,
        _ => null
    };

    public static bool EhTelefone(TipoContato tipo) => Categoria(tipo) == CategoriaMeioContato.Telefone;

    public static void Normalizar(TipoMeioContato tipo) =>
        tipo.Nome = string.IsNullOrWhiteSpace(tipo.Nome)
            ? string.Empty
            : string.Join(' ', tipo.Nome.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static List<string> Validar(TipoMeioContato tipo)
    {
        var erros = new List<string>();
        if (tipo.Nome.Length == 0) erros.Add("Informe o nome do tipo.");
        else if (tipo.Nome.Length > TipoMeioContato.TamanhoMaximoNome)
            erros.Add($"O nome do tipo pode ter no máximo {TipoMeioContato.TamanhoMaximoNome} caracteres.");
        return erros;
    }

    /// <summary>
    /// Confere o tipo (classificação) de cada telefone/e-mail: precisa existir e ser da mesma categoria; um tipo
    /// desativado só vale se já era o daquele contato.
    /// </summary>
    /// <param name="anteriores">Tipo gravado de cada contato (Id do contato → Id do tipo).</param>
    public static List<string> ValidarTipos(IEnumerable<MeioContato> meios, IReadOnlyDictionary<Guid, Guid?> anteriores,
                                            IReadOnlyDictionary<Guid, TipoMeioContato> cadastro)
    {
        var erros = new List<string>();
        foreach (var meio in meios.Where(m => m.TipoMeioContatoId is not null))
        {
            if (!cadastro.TryGetValue(meio.TipoMeioContatoId!.Value, out var tipo))
                erros.Add("Um dos tipos de telefone/e-mail escolhidos não existe mais. Reabra o cadastro e escolha de novo.");
            else if (tipo.Categoria != Categoria(meio.Tipo))
                erros.Add($"O tipo \"{tipo.Nome}\" é de {(tipo.Categoria == CategoriaMeioContato.Email ? "e-mail" : "telefone")} e não serve para este contato.");
            else if (!tipo.Ativo && anteriores.GetValueOrDefault(meio.Id) != tipo.Id)
                erros.Add($"O tipo \"{tipo.Nome}\" está desativado e não pode ser escolhido em novos contatos.");
        }
        return erros.Distinct().ToList();
    }

    /// <summary>
    /// Bloco G (P1-12): o mesmo telefone ou e-mail duas vezes na mesma pessoa, entre os ativos. Compara o valor já
    /// normalizado (telefone só com dígitos, sem o 55; e-mail em minúsculas), então "(11) 98765-4321" e "11987654321" são
    /// o mesmo número. Telefone, celular e WhatsApp são a mesma categoria (um número é um número); e-mail é outra; "outro"
    /// (texto livre) não é conferido. Inativo não conta (é histórico). Dois que já estavam gravados assim (cadastro antigo)
    /// não impedem a gravação; o erro vai para o que é novo, alterado ou reativado.
    /// </summary>
    /// <param name="meios">Como vão ser gravados, já normalizados (a posição dá o "Telefone/e-mail N").</param>
    /// <param name="anteriores">Como estão gravados (vazio num cadastro novo).</param>
    public static List<ErroValidacao> ValidarRepetidos(IReadOnlyList<MeioContato> meios, IEnumerable<MeioContato> anteriores)
    {
        var gravados = anteriores.Where(m => m.Ativo).ToDictionary(m => m.Id, Chave);
        bool ComoGravado(MeioContato m) => gravados.TryGetValue(m.Id, out var chave) && chave == Chave(m);

        var primeiros = new Dictionary<(CategoriaMeioContato, string), int>();
        var erros = new List<ErroValidacao>();
        for (var i = 0; i < meios.Count; i++)
        {
            var meio = meios[i];
            if (!meio.Ativo || Chave(meio) is not { } chave) continue;
            if (!primeiros.TryGetValue(chave, out var j))
            {
                primeiros[chave] = i;
                continue;
            }

            var outro = meios[j];
            if (ComoGravado(meio) && ComoGravado(outro)) continue;
            // Aponta o que mudou (o gravado igual fica como está).
            var (alvo, posicao, primeiro) = ComoGravado(meio) ? (outro, j, i) : (meio, i, j);
            var oQue = chave.Item1 == CategoriaMeioContato.Email ? "este e-mail" : "este número";
            erros.Add(new ErroValidacao($"Telefone/e-mail {posicao + 1}: {oQue} já está na lista (Telefone/e-mail {primeiro + 1}).",
                global::Lone.Domain.Pessoas.CamposFichaPessoa.MeioContatoValor, alvo.Id));
        }
        return erros;
    }

    /// <summary>Categoria + valor normalizado; nulo para "outro" e para valor vazio.</summary>
    private static (CategoriaMeioContato, string)? Chave(MeioContato m) =>
        Categoria(m.Tipo) is { } categoria && m.Valor.Length > 0
            ? (categoria, categoria == CategoriaMeioContato.Email ? m.Valor.ToLowerInvariant() : m.Valor)
            : null;
}
