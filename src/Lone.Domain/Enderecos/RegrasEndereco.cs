using Lone.Domain.Entidades;

namespace Lone.Domain.Enderecos;

/// <summary>Os tipos iniciais de endereço (nascem com a base, com Ids fixos; o usuário pode criar outros).</summary>
public static class TiposEnderecoIniciais
{
    public static IReadOnlyList<(Guid Id, string Nome, int Ordem)> Todos { get; } =
    [
        (new Guid("7a9e1c02-0000-0000-0000-000000000001"), "Sede", 1),
        (new Guid("7a9e1c02-0000-0000-0000-000000000002"), "Filial", 2),
        (new Guid("7a9e1c02-0000-0000-0000-000000000003"), "Depósito", 3),
        (new Guid("7a9e1c02-0000-0000-0000-000000000004"), "Residência", 4)
    ];
}

/// <summary>Regras dos endereços da pessoa e do cadastro de tipos de endereço. Não acessa banco.</summary>
public static class RegrasEndereco
{
    public const int TamanhoMaximoObservacoes = 250;

    public static void Normalizar(TipoEndereco tipo) =>
        tipo.Nome = string.IsNullOrWhiteSpace(tipo.Nome)
            ? string.Empty
            : string.Join(' ', tipo.Nome.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static List<string> Validar(TipoEndereco tipo)
    {
        var erros = new List<string>();
        if (tipo.Nome.Length == 0) erros.Add("Informe o nome do tipo de endereço.");
        else if (tipo.Nome.Length > TipoEndereco.TamanhoMaximoNome)
            erros.Add($"O nome do tipo pode ter no máximo {TipoEndereco.TamanhoMaximoNome} caracteres.");
        return erros;
    }

    /// <summary>O tipo de cada endereço precisa existir; um desativado só vale se já era o daquele endereço.</summary>
    /// <param name="anteriores">Tipo gravado de cada endereço (Id do endereço → Id do tipo).</param>
    public static List<string> ValidarTipos(IEnumerable<PessoaEndereco> enderecos, IReadOnlyDictionary<Guid, Guid?> anteriores,
                                            IReadOnlyDictionary<Guid, TipoEndereco> cadastro)
    {
        var erros = new List<string>();
        foreach (var endereco in enderecos.Where(e => e.TipoEnderecoId is not null))
        {
            if (!cadastro.TryGetValue(endereco.TipoEnderecoId!.Value, out var tipo))
                erros.Add("Um dos tipos de endereço escolhidos não existe mais. Reabra o cadastro e escolha de novo.");
            else if (!tipo.Ativo && anteriores.GetValueOrDefault(endereco.Id) != tipo.Id)
                erros.Add($"O tipo de endereço \"{tipo.Nome}\" está desativado e não pode ser escolhido em novos endereços.");
        }
        return erros.Distinct().ToList();
    }
}
