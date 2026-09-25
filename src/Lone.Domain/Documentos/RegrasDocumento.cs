using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Documentos;

/// <summary>Os tipos de documento de sistema: nascem com a base, com Ids fixos, ligados ao enum.</summary>
public static class TiposDocumentoSistema
{
    public static IReadOnlyList<(Guid Id, TipoDocumento Tipo, string Nome, int Ordem, bool ExigeValidade)> Todos { get; } =
    [
        (new Guid("7a9e1c03-0000-0000-0000-000000000001"), TipoDocumento.Rg, "RG", 1, false),
        (new Guid("7a9e1c03-0000-0000-0000-000000000002"), TipoDocumento.Cnh, "CNH", 2, true),
        (new Guid("7a9e1c03-0000-0000-0000-000000000003"), TipoDocumento.Passaporte, "Passaporte", 3, true),
        (new Guid("7a9e1c03-0000-0000-0000-000000000004"), TipoDocumento.DocumentoEstrangeiro, "Documento estrangeiro", 4, false),
        (new Guid("7a9e1c03-0000-0000-0000-000000000009"), TipoDocumento.Outro, "Outro", 9, false)
    ];

    /// <summary>Id fixo do tipo de sistema (chamadas antigas mandam só o enum).</summary>
    public static Guid Id(TipoDocumento tipo) =>
        Todos.FirstOrDefault(t => t.Tipo == tipo) is { Id: var id } && id != Guid.Empty ? id : Todos[^1].Id;
}

/// <summary>Regras dos documentos da pessoa e do cadastro de tipos de documento. Não acessa banco.</summary>
public static class RegrasDocumento
{
    public static void Normalizar(TipoDocumentoCadastro tipo)
    {
        tipo.Nome = string.IsNullOrWhiteSpace(tipo.Nome)
            ? string.Empty
            : string.Join(' ', tipo.Nome.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static List<string> Validar(TipoDocumentoCadastro tipo)
    {
        var erros = new List<string>();
        if (tipo.Nome.Length == 0) erros.Add("Informe o nome do tipo de documento.");
        else if (tipo.Nome.Length > TipoDocumentoCadastro.TamanhoMaximoNome)
            erros.Add($"O nome do tipo pode ter no máximo {TipoDocumentoCadastro.TamanhoMaximoNome} caracteres.");
        if (tipo.DiasAvisoVencimento is < 0 or > TipoDocumentoCadastro.MaximoDiasAviso)
            erros.Add($"Os dias de aviso do vencimento devem ficar entre 0 e {TipoDocumentoCadastro.MaximoDiasAviso}.");
        return erros;
    }

    /// <summary>Chamadas antigas mandam só o enum: liga ao tipo de sistema correspondente.</summary>
    public static void CompletarIds(Pessoa pessoa)
    {
        foreach (var d in pessoa.Documentos.Where(d => d.TipoDocumentoId == Guid.Empty))
            d.TipoDocumentoId = TiposDocumentoSistema.Id(d.Tipo);
    }

    /// <summary>
    /// Confere o tipo de cada documento contra o cadastro e copia o enum (a coluna antiga) do tipo escolhido:
    /// o aplicativo nunca decide o enum. Um tipo desativado só vale se já era o daquele documento.
    /// Validade obrigatória só para documentos ativos (um antigo, já removido, não trava a gravação).
    /// </summary>
    /// <param name="anteriores">Tipo gravado de cada documento (Id do documento → Id do tipo).</param>
    public static List<string> Aplicar(IReadOnlyList<PessoaDocumento> documentos, IReadOnlyDictionary<Guid, Guid> anteriores,
                                       IReadOnlyDictionary<Guid, TipoDocumentoCadastro> cadastro)
    {
        var erros = new List<string>();
        for (var i = 0; i < documentos.Count; i++)
        {
            var d = documentos[i];
            if (!cadastro.TryGetValue(d.TipoDocumentoId, out var tipo))
            {
                erros.Add("Um dos tipos de documento escolhidos não existe mais. Reabra o cadastro e escolha de novo.");
                continue;
            }

            d.Tipo = tipo.TipoSistema ?? TipoDocumento.Outro;
            if (!tipo.Ativo && anteriores.GetValueOrDefault(d.Id) != tipo.Id)
                erros.Add($"O tipo de documento \"{tipo.Nome}\" está desativado e não pode ser escolhido em novos documentos.");
            if (d.Ativo && tipo.ExigeValidade && d.ValidoAte is null)
                erros.Add($"Documento {i + 1} ({tipo.Nome}): informe a validade.");
        }
        return erros.Distinct().ToList();
    }

    /// <summary>Válido, vence em breve (dentro da antecedência do tipo) ou vencido. Vence no fim do dia "válido até".</summary>
    public static SituacaoValidade Situacao(DateOnly? validoAte, int diasAviso, DateOnly hoje)
    {
        if (validoAte is not { } validade) return SituacaoValidade.SemValidade;
        if (validade < hoje) return SituacaoValidade.Vencido;
        return validade.DayNumber - hoje.DayNumber <= Math.Max(diasAviso, 0) ? SituacaoValidade.VenceEmBreve : SituacaoValidade.Valido;
    }

    /// <summary>Frase curta para a tela (ex.: "Vence em 12 dias", "Vencido há 3 dias").</summary>
    public static string TextoSituacao(DateOnly? validoAte, int diasAviso, DateOnly hoje)
    {
        if (validoAte is not { } validade) return string.Empty;
        var dias = validade.DayNumber - hoje.DayNumber;
        return Situacao(validoAte, diasAviso, hoje) switch
        {
            SituacaoValidade.Vencido => dias == -1 ? "Vencido ontem" : $"Vencido há {-dias} dias",
            SituacaoValidade.VenceEmBreve => dias switch { 0 => "Vence hoje", 1 => "Vence amanhã", _ => $"Vence em {dias} dias" },
            _ => string.Empty
        };
    }
}
