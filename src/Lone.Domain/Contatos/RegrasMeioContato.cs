using Lone.Domain.Entidades;
using Lone.Domain.Enums;

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
}
