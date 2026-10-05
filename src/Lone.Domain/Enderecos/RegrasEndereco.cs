using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using C = Lone.Domain.Pessoas.CamposFichaPessoa;

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

    /// <summary>
    /// O texto único de "sem número" (caixa "Sem número" da ficha). A NF-e exige o número (texto de 1 a 60) e não tem marca
    /// própria de sem número: vai "S/N", o usual; nunca vazio (a nota é recusada) nem "0" (parece um número).
    /// </summary>
    public const string SemNumero = "S/N";

    /// <summary>"S/N", "SN", "s/n", "S.N.", "sem número"... (o mesmo que a conferência de duplicidade entende como sem número).</summary>
    public static bool EhSemNumero(string? numero) =>
        !string.IsNullOrWhiteSpace(numero) && DuplicidadeEndereco.Numero(numero).Length == 0;

    /// <summary>
    /// O número digitado parece "não tem número" ("casa", "x", "-", "0", "00"): nenhum algarismo, ou só zeros sem letra. Só
    /// para sugerir a caixa "Sem número" (não é erro): "100A", "KM 23", "Lote 5 Quadra 3" são números válidos.
    /// </summary>
    public static bool PareceSemNumero(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero) || EhSemNumero(numero)) return false;
        var digitos = numero.Where(char.IsAsciiDigit).ToList();
        return digitos.Count == 0 || (digitos.All(c => c == '0') && !numero.Any(char.IsLetter));
    }

    /// <summary>Qualquer forma de "sem número" vira <see cref="SemNumero"/>; o resto fica como está.</summary>
    public static string? NormalizarNumero(string? numero) => EhSemNumero(numero) ? SemNumero : numero;

    /// <summary>
    /// O que falta num endereço ativo no Brasil para ficar completo (o que a NF-e pede além de logradouro e município):
    /// CEP, número (quem não tem usa "S/N") e bairro. No exterior, nada (cidade e país já são obrigatórios).
    /// </summary>
    public static IReadOnlyList<(string Nome, string Campo)> Faltando(PessoaEndereco e)
    {
        if (!e.Ativo || !e.EhBrasil) return [];
        var faltando = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(e.Cep)) faltando.Add(("CEP", C.Cep));
        if (string.IsNullOrWhiteSpace(e.Numero)) faltando.Add(("número", C.Numero));
        if (string.IsNullOrWhiteSpace(e.Bairro)) faltando.Add(("bairro", C.Bairro));
        return faltando;
    }

    /// <summary>
    /// Endereço novo ou alterado no Brasil precisa estar completo (<see cref="Faltando"/>). Um gravado que não mudou (local
    /// igual, continua ativo) não impede a gravação: fica como pendência no resumo da pessoa até alguém completar.
    /// </summary>
    /// <param name="atuais">Os endereços como vão ser gravados (a posição dá o "Endereço N" da mensagem).</param>
    /// <param name="anteriores">Os endereços como estão gravados (vazio num cadastro novo).</param>
    public static List<ErroValidacao> ValidarCompletos(IReadOnlyList<PessoaEndereco> atuais, IEnumerable<PessoaEndereco> anteriores)
    {
        var gravados = anteriores.Where(e => e.Ativo).ToDictionary(e => e.Id, DuplicidadeEndereco.Chave);
        var erros = new List<ErroValidacao>();
        for (var i = 0; i < atuais.Count; i++)
        {
            var e = atuais[i];
            // Sem logradouro já há o erro "informe o logradouro" (PessoaValidador): o resto aparece quando ele for preenchido.
            if (string.IsNullOrWhiteSpace(e.Logradouro)) continue;
            if (gravados.TryGetValue(e.Id, out var chave) && chave == DuplicidadeEndereco.Chave(e)) continue;
            foreach (var (nome, campo) in Faltando(e))
                erros.Add(new ErroValidacao(nome == "número"
                    ? $"Endereço {i + 1}: informe o número ou marque \"Sem número\"."
                    : $"Endereço {i + 1}: informe o {nome}.", campo, e.Id));
        }
        return erros;
    }

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
