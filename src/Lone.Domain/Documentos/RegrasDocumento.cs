using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

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

    /// <summary>
    /// A quem o tipo se aplica: RG e CNH só pessoa física; passaporte, física e estrangeiro; documento estrangeiro,
    /// só estrangeiro. "Outro" e os tipos criados pelo usuário (sem tipo de sistema) valem para todos.
    /// </summary>
    public static bool AplicaA(TipoDocumento? tipoSistema, NaturezaPessoa natureza) => tipoSistema switch
    {
        TipoDocumento.Rg or TipoDocumento.Cnh => natureza == NaturezaPessoa.Fisica,
        TipoDocumento.Passaporte => natureza != NaturezaPessoa.Juridica,
        TipoDocumento.DocumentoEstrangeiro => natureza == NaturezaPessoa.Estrangeiro,
        _ => true
    };

    /// <summary>Documento pessoal de sistema (RG, CNH, passaporte, documento estrangeiro): tem órgão emissor.</summary>
    public static bool TemOrgaoEmissor(TipoDocumento? tipoSistema) => tipoSistema is not (null or TipoDocumento.Outro);

    /// <summary>RG e CNH são emitidos por um estado: têm UF.</summary>
    public static bool TemUf(TipoDocumento? tipoSistema) => tipoSistema is TipoDocumento.Rg or TipoDocumento.Cnh;

    /// <summary>
    /// P1-8: as regras de cada tipo de sistema como dados — a semente da migração e do banco novo. Reproduz exatamente
    /// <see cref="AplicaA"/>, <see cref="TemOrgaoEmissor"/> e <see cref="TemUf"/> (teste de paridade); formato livre (como
    /// sempre foi) e número repetido só com aviso (nada passa a bloquear por causa da migração).
    /// </summary>
    public static (bool Fisica, bool Juridica, bool Estrangeiro, UsoCampoDocumento Orgao, UsoCampoDocumento Uf,
                   UsoCampoDocumento Emissao, FormatoNumeroDocumento Formato, UnicidadeDocumento Unicidade) Semente(TipoDocumento tipo) => tipo switch
    {
        TipoDocumento.Rg => (true, false, false, UsoCampoDocumento.Opcional, UsoCampoDocumento.Opcional, UsoCampoDocumento.Opcional,
                             FormatoNumeroDocumento.Livre, UnicidadeDocumento.Aviso),
        TipoDocumento.Cnh => (true, false, false, UsoCampoDocumento.Opcional, UsoCampoDocumento.Opcional, UsoCampoDocumento.Opcional,
                              FormatoNumeroDocumento.Livre, UnicidadeDocumento.Aviso),
        TipoDocumento.Passaporte => (true, false, true, UsoCampoDocumento.Opcional, UsoCampoDocumento.Oculto, UsoCampoDocumento.Opcional,
                                     FormatoNumeroDocumento.Livre, UnicidadeDocumento.Aviso),
        TipoDocumento.DocumentoEstrangeiro => (false, false, true, UsoCampoDocumento.Opcional, UsoCampoDocumento.Oculto, UsoCampoDocumento.Opcional,
                                               FormatoNumeroDocumento.Livre, UnicidadeDocumento.Aviso),
        _ => (true, true, true, UsoCampoDocumento.Oculto, UsoCampoDocumento.Oculto, UsoCampoDocumento.Opcional,
              FormatoNumeroDocumento.Livre, UnicidadeDocumento.Nenhuma)
    };

    /// <summary>Aplica a <see cref="Semente"/> do tipo de sistema ao registro do cadastro.</summary>
    public static TipoDocumentoCadastro ComSemente(TipoDocumentoCadastro tipo, TipoDocumento tipoSistema)
    {
        var s = Semente(tipoSistema);
        tipo.AplicaPessoaFisica = s.Fisica;
        tipo.AplicaPessoaJuridica = s.Juridica;
        tipo.AplicaEstrangeiro = s.Estrangeiro;
        tipo.UsoOrgaoEmissor = s.Orgao;
        tipo.UsoUf = s.Uf;
        tipo.UsoEmissao = s.Emissao;
        tipo.FormatoNumero = s.Formato;
        tipo.Unicidade = s.Unicidade;
        return tipo;
    }
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

        // P1-8: as regras do tipo.
        if (!tipo.AplicaPessoaFisica && !tipo.AplicaPessoaJuridica && !tipo.AplicaEstrangeiro)
            erros.Add("Marque ao menos um tipo de pessoa que pode usar este documento.");
        if (!Enum.IsDefined(tipo.UsoOrgaoEmissor) || !Enum.IsDefined(tipo.UsoUf) || !Enum.IsDefined(tipo.UsoEmissao))
            erros.Add("Uso de órgão emissor, UF ou data de emissão inválido.");
        if (!Enum.IsDefined(tipo.FormatoNumero))
            erros.Add("Formato do número inválido.");
        if (!Enum.IsDefined(tipo.Unicidade))
            erros.Add("Opção de número repetido inválida.");
        if (tipo.TamanhoMinimoNumero is < 1 or > NumeroDocumento.TamanhoMaximo || tipo.TamanhoMaximoNumero is < 1 or > NumeroDocumento.TamanhoMaximo)
            erros.Add($"Os tamanhos do número devem ficar entre 1 e {NumeroDocumento.TamanhoMaximo}.");
        else if (tipo.TamanhoMinimoNumero is { } minimo && tipo.TamanhoMaximoNumero is { } maximo && minimo > maximo)
            erros.Add("O tamanho mínimo do número é maior que o máximo.");
        if (tipo.Unicidade == UnicidadeDocumento.PorTipoEUf && tipo.UsoUf != UsoCampoDocumento.Obrigatorio)
            erros.Add("Para bloquear número repetido na mesma UF, a UF precisa ser obrigatória.");
        return erros;
    }

    /// <summary>O modo bloqueia número repetido entre pessoas (garantia no SQL Server).</summary>
    public static bool Bloqueia(UnicidadeDocumento modo) => modo is UnicidadeDocumento.PorTipo or UnicidadeDocumento.PorTipoEUf;

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
    /// <param name="natureza">
    /// Natureza da pessoa: um tipo que não se aplica a ela (RG numa empresa) não vale em documento novo nem em troca de tipo;
    /// o documento que já o tinha continua como está (nada some sem ação do usuário).
    /// </param>
    public static List<string> Aplicar(IReadOnlyList<PessoaDocumento> documentos, IReadOnlyDictionary<Guid, Guid> anteriores,
                                       IReadOnlyDictionary<Guid, TipoDocumentoCadastro> cadastro, NaturezaPessoa? natureza = null)
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
            // P1-8: a quem o tipo se aplica vem do cadastro (os de sistema nascem iguais ao TiposDocumentoSistema.AplicaA).
            if (natureza is { } n && !tipo.AplicaA(n) && anteriores.GetValueOrDefault(d.Id) != tipo.Id)
                erros.Add($"O tipo de documento \"{tipo.Nome}\" não se aplica a {NomeNatureza(n)}.");
            if (d.Ativo && tipo.ExigeValidade && d.ValidoAte is null)
                erros.Add($"Documento {i + 1} ({tipo.Nome}): informe a validade.");

            // P1-8: a chave da unicidade entre pessoas segue o modo do tipo (nula quando ele não bloqueia).
            d.NumeroNormalizado = NumeroDocumento.Normalizar(d.Numero);
            d.ChaveUnicidade = NumeroDocumento.ChaveUnicidade(tipo.Unicidade, tipo.Id, d.Uf, d.NumeroNormalizado);
        }
        return erros.Distinct().ToList();
    }

    /// <summary>
    /// P1-8B (D6): documento que passa pelas regras novas — novo, reativado ou com tipo, número, órgão, UF ou datas
    /// alterados. Um documento antigo que não foi mexido (só observações, por exemplo) nunca trava a gravação.
    /// </summary>
    public static bool Tocado(PessoaDocumento d, PessoaDocumento? anterior) =>
        anterior is null ||
        (!anterior.Ativo && d.Ativo) ||
        anterior.TipoDocumentoId != d.TipoDocumentoId ||
        !string.Equals(anterior.Numero, d.Numero, StringComparison.Ordinal) ||
        !string.Equals(anterior.OrgaoEmissor, d.OrgaoEmissor, StringComparison.Ordinal) ||
        !string.Equals(anterior.Uf, d.Uf, StringComparison.Ordinal) ||
        anterior.EmitidoEm != d.EmitidoEm ||
        anterior.ValidoAte != d.ValidoAte;

    /// <summary>
    /// P1-8B: as regras do tipo (metadados) para cada documento ATIVO e TOCADO (<see cref="Tocado"/>): órgão emissor, UF e
    /// emissão conforme o uso (Obrigatório exige; Oculto/Opcional não), emissão no futuro, formato e tamanho do número, e
    /// o mesmo número (comparável) duas vezes na pessoa, no mesmo tipo. Inativo nunca é revalidado; documento sem número
    /// comparável não entra na comparação. Os erros vêm com o campo e o Id do documento (a ficha leva até lá).
    /// </summary>
    public static List<ErroValidacao> ValidarTocados(IReadOnlyList<PessoaDocumento> documentos, IReadOnlyCollection<PessoaDocumento> anteriores,
                                                   IReadOnlyDictionary<Guid, TipoDocumentoCadastro> cadastro, DateOnly hoje)
    {
        var erros = new List<ErroValidacao>();
        var gravados = anteriores.ToDictionary(a => a.Id);
        foreach (var d in documentos)
        {
            if (!d.Ativo || !cadastro.TryGetValue(d.TipoDocumentoId, out var tipo) || !Tocado(d, gravados.GetValueOrDefault(d.Id)))
                continue;

            var rotulo = tipo.Nome + (d.Numero.Length > 0 ? " " + d.Numero : string.Empty);
            if (tipo.UsoOrgaoEmissor == UsoCampoDocumento.Obrigatorio && string.IsNullOrWhiteSpace(d.OrgaoEmissor))
                erros.Add(new($"{rotulo}: informe o órgão emissor.", CamposFichaPessoa.DocumentoOrgaoEmissor, d.Id));
            if (tipo.UsoUf == UsoCampoDocumento.Obrigatorio && string.IsNullOrWhiteSpace(d.Uf))
                erros.Add(new($"{rotulo}: informe a UF.", CamposFichaPessoa.DocumentoUf, d.Id));
            if (tipo.UsoEmissao == UsoCampoDocumento.Obrigatorio && d.EmitidoEm is null)
                erros.Add(new($"{rotulo}: informe a data de emissão.", CamposFichaPessoa.DocumentoEmitidoEm, d.Id));
            if (d.EmitidoEm is { } emissao && emissao > hoje)
                erros.Add(new($"{rotulo}: a data de emissão não pode ser no futuro.", CamposFichaPessoa.DocumentoEmitidoEm, d.Id));

            if (d.Numero.Length > 0 &&
                NumeroDocumento.ConferirFormato(d.Numero, tipo.FormatoNumero, tipo.TamanhoMinimoNumero, tipo.TamanhoMaximoNumero) is { } problema)
                erros.Add(new($"{rotulo}: o número {problema}.", CamposFichaPessoa.DocumentoNumero, d.Id));

            var comparavel = NumeroDocumento.Normalizar(d.Numero);
            if (comparavel.Length > 0 && documentos.Any(o => o.Id != d.Id && o.Ativo && o.TipoDocumentoId == d.TipoDocumentoId &&
                                                            NumeroDocumento.Normalizar(o.Numero) == comparavel))
                erros.Add(new($"{rotulo}: este número já está em outro documento \"{tipo.Nome}\" desta pessoa (pontos, traços e espaços não contam).",
                              CamposFichaPessoa.DocumentoNumero, d.Id));
        }
        return erros;
    }

    private static string NomeNatureza(NaturezaPessoa n) => n switch
    {
        NaturezaPessoa.Fisica => "pessoa física",
        NaturezaPessoa.Juridica => "pessoa jurídica",
        _ => "pessoa estrangeira"
    };

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
