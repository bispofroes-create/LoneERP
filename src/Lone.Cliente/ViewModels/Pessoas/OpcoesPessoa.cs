using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Listas de escolha do cadastro de pessoas, com os textos em português. São arrays (e não listas somente
/// leitura) porque o Picker da tela exige uma lista indexável (IList).
/// </summary>
public static class OpcoesPessoa
{
    public static Opcao<NaturezaPessoa>[] Naturezas { get; } =
        [.. new[] { NaturezaPessoa.Fisica, NaturezaPessoa.Juridica, NaturezaPessoa.Estrangeiro }
            .Select(n => new Opcao<NaturezaPessoa>(n, NomesPessoa.Natureza(n)))];

    /// <summary>O que o formulário pode escolher (desativar e reativar são ações próprias).</summary>
    public static Opcao<SituacaoPessoa>[] SituacoesEditaveis { get; } =
        [.. new[] { SituacaoPessoa.Ativo, SituacaoPessoa.EmAnalise }.Select(s => new Opcao<SituacaoPessoa>(s, NomesPessoa.Situacao(s)))];

    public static Opcao<SituacaoPessoa>[] Situacoes { get; } =
        [.. new[] { SituacaoPessoa.Ativo, SituacaoPessoa.EmAnalise, SituacaoPessoa.Inativo, SituacaoPessoa.Arquivado }
            .Select(s => new Opcao<SituacaoPessoa>(s, NomesPessoa.Situacao(s)))];

    public static Opcao<IndicadorIE>[] IndicadoresIE { get; } =
    [
        new(IndicadorIE.NaoInformado, "Não informado"),
        new(IndicadorIE.Contribuinte, "Contribuinte do ICMS"),
        new(IndicadorIE.Isento, "Contribuinte isento"),
        new(IndicadorIE.NaoContribuinte, "Não contribuinte")
    ];

    public static Opcao<RegimeTributario>[] Regimes { get; } =
    [
        new(RegimeTributario.NaoInformado, "Não informado"),
        new(RegimeTributario.SimplesNacional, "Simples Nacional"),
        new(RegimeTributario.Mei, "MEI"),
        new(RegimeTributario.RegimeNormal, "Regime normal")
    ];

    public static Opcao<TipoContato>[] TiposContato { get; } =
    [
        new(TipoContato.Celular, "Celular"),
        new(TipoContato.WhatsApp, "WhatsApp"),
        new(TipoContato.Telefone, "Telefone"),
        new(TipoContato.Email, "E-mail"),
        new(TipoContato.Outro, "Outro")
    ];

    public static Opcao<TipoDocumento>[] TiposDocumento { get; } =
    [
        new(TipoDocumento.Rg, "RG"),
        new(TipoDocumento.Cnh, "CNH"),
        new(TipoDocumento.Passaporte, "Passaporte"),
        new(TipoDocumento.DocumentoEstrangeiro, "Documento estrangeiro"),
        new(TipoDocumento.Outro, "Outro")
    ];

    /// <summary>Filtro da lista (nulo = todos os papéis).</summary>
    public static Opcao<TipoPapel?>[] FiltrosPapel { get; } =
    [
        new(null, "Todos"),
        new(TipoPapel.Cliente, "Clientes"),
        new(TipoPapel.Fornecedor, "Fornecedores"),
        new(TipoPapel.EmpresaDoGrupo, "Empresas do grupo"),
        new(TipoPapel.Transportadora, "Transportadoras"),
        new(TipoPapel.Vendedor, "Vendedores")
    ];

    /// <summary>Ordem em que os papéis aparecem na ficha.</summary>
    public static TipoPapel[] PapeisNaTela { get; } =
    [
        TipoPapel.Cliente, TipoPapel.Fornecedor, TipoPapel.Vendedor, TipoPapel.Transportadora,
        TipoPapel.Representante, TipoPapel.PrestadorServico, TipoPapel.Funcionario, TipoPapel.EmpresaDoGrupo
    ];

    public static Opcao<SexoRegistro>[] Sexos { get; } =
        [.. Enum.GetValues<SexoRegistro>().Select(v => new Opcao<SexoRegistro>(v, NomesPessoa.Sexo(v)))];

    public static Opcao<IdentidadeGenero>[] Generos { get; } =
        [.. Enum.GetValues<IdentidadeGenero>().Select(v => new Opcao<IdentidadeGenero>(v, NomesPessoa.Genero(v)))];

    public static Opcao<CorRaca>[] CoresRacas { get; } =
        [.. Enum.GetValues<CorRaca>().Select(v => new Opcao<CorRaca>(v, NomesPessoa.CorRaca(v)))];

    public static Opcao<EstadoCivil>[] EstadosCivis { get; } =
        [.. Enum.GetValues<EstadoCivil>().Select(v => new Opcao<EstadoCivil>(v, NomesPessoa.EstadoCivil(v)))];

    public static Opcao<Escolaridade>[] Escolaridades { get; } =
        [.. Enum.GetValues<Escolaridade>().Select(v => new Opcao<Escolaridade>(v, NomesPessoa.Escolaridade(v)))];

    /// <summary>Como a pessoa chegou até a empresa. Um valor antigo fora da lista é acrescentado ao abrir a ficha.</summary>
    public static string[] Origens { get; } =
        ["Não informada", "Indicação", "Site", "Redes sociais", "Loja física", "Evento ou feira", "Anúncio", "Vendedor externo", "Outra"];

    public static CanalComunicacao[] Canais { get; } = Enum.GetValues<CanalComunicacao>();
}
