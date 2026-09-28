using Lone.Domain.Enums;

namespace Lone.Contracts.Pessoas;

/// <summary>
/// Critérios da consulta avançada de pessoas. Tudo é tipado: o servidor monta a consulta com LINQ (sem SQL dinâmico
/// nem fórmula livre). Critério vazio/nulo não filtra. Os critérios de listas combinam com "qualquer um".
/// </summary>
public sealed class CriteriosPessoas
{
    /// <summary>Mesmo texto da busca rápida (nome, código, CPF/CNPJ, telefone, e-mail, campos pesquisáveis).</summary>
    public string? Texto { get; set; }

    /// <summary>Colunas e ordenação da lista (visões salvas). Nulo = a visão não mexe nas colunas.</summary>
    public LayoutListaPessoas? Layout { get; set; }

    public List<NaturezaPessoa> Naturezas { get; set; } = new();

    /// <summary>Vazio = ativos e em análise (como a lista).</summary>
    public List<SituacaoPessoa> Situacoes { get; set; } = new();

    /// <summary>Com algum destes papéis ativos (ou todos, com <see cref="TodosOsPapeis"/>).</summary>
    public List<Guid> PapeisIds { get; set; } = new();
    public bool TodosOsPapeis { get; set; }

    public List<Guid> EtiquetasIds { get; set; } = new();

    // ---- Endereço ----
    public string? Uf { get; set; }
    public int? MunicipioId { get; set; }

    // ---- Fiscal ----
    /// <summary>Código CNAE (7 dígitos) ou começo dele (ex.: "47" = comércio varejista).</summary>
    public string? Cnae { get; set; }
    public bool SomenteCnaePrincipal { get; set; }
    public bool? ProdutorRural { get; set; }
    public RegimeTributario? Regime { get; set; }

    // ---- Comercial ----
    /// <summary>Na carteira (vigente hoje) deste vendedor/representante.</summary>
    public Guid? VendedorId { get; set; }

    /// <summary>Clientes sem ninguém na carteira hoje.</summary>
    public bool SemCarteira { get; set; }

    // ---- Relacionamento e situação ----
    public SituacaoRelacionamento? Relacionamento { get; set; }

    /// <summary>Sem interação registrada nos últimos N dias (inclui quem nunca teve).</summary>
    public int? SemInteracaoDias { get; set; }

    /// <summary>Com bloqueio ativo (verdadeiro) ou sem nenhum (falso).</summary>
    public bool? Bloqueado { get; set; }

    // ---- Documentos ----
    public bool DocumentosVencidos { get; set; }

    /// <summary>Com documento ativo vencendo nos próximos N dias (sem contar os já vencidos).</summary>
    public int? DocumentosVencendoDias { get; set; }

    // ---- Campo personalizado (da pessoa) ----
    public Guid? CampoId { get; set; }

    /// <summary>Começo do valor (sem diferenciar maiúsculas/acentos, pelo índice do campo).</summary>
    public string? CampoValor { get; set; }

    // ---- Cadastro ----
    public DateOnly? CadastradoDe { get; set; }
    public DateOnly? CadastradoAte { get; set; }

    /// <summary>
    /// Condições do catálogo de campos (painel de filtros da tela de Pessoas). Somam-se aos critérios acima, que são o
    /// formato da consulta avançada antiga e dos filtros salvos por ela: o servidor os traduz para condições.
    /// </summary>
    public List<CondicaoFiltro> Condicoes { get; set; } = new();
}

/// <summary>Uma página da consulta: ordem por nome e Id; a próxima começa depois da última linha (paginação por chave).</summary>
public sealed class ConsultaPessoasRequisicao
{
    public const int LimitePadrao = 100;
    public const int LimiteMaximo = 500;

    public CriteriosPessoas Criterios { get; set; } = new();
    public string? AposNome { get; set; }
    public Guid? AposId { get; set; }
    public int Limite { get; set; } = LimitePadrao;

    /// <summary>Conta o total (só na primeira página: é a parte mais cara em bases grandes).</summary>
    public bool ContarTotal { get; set; }
}

public sealed class PaginaPessoas
{
    public List<PessoaResumo> Itens { get; set; } = new();

    /// <summary>Chave para a próxima página (nula = acabou).</summary>
    public string? ProximoNome { get; set; }
    public Guid? ProximoId { get; set; }
    public int? Total { get; set; }
}

/// <summary>Resultado exportado (CSV separado por ponto e vírgula, UTF-8 com BOM para o Excel).</summary>
public sealed class ArquivoExportado
{
    public string NomeArquivo { get; set; } = string.Empty;
    public string Conteudo { get; set; } = string.Empty;
    public int Linhas { get; set; }
}

/// <summary>Filtro salvo com nome. Do usuário; compartilhado aparece para todos (só o autor altera).</summary>
public sealed class FiltroSalvoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Compartilhado { get; set; }
    public CriteriosPessoas Criterios { get; set; } = new();

    /// <summary>Somente leitura: quem criou e se é do usuário atual.</summary>
    public string? Autor { get; set; }
    public bool Proprio { get; set; }

    /// <summary>Texto nas listas de escolha.</summary>
    public override string ToString() => Proprio ? (Compartilhado ? Nome + " (compartilhado)" : Nome) : $"{Nome} (de {Autor})";
}

public sealed record OpcaoConsultaDto(Guid Id, string Nome);

/// <summary>Escolhas da tela de consulta, numa chamada só.</summary>
public sealed class OpcoesConsultaPessoasDto
{
    public List<OpcaoConsultaDto> Papeis { get; set; } = new();
    public List<OpcaoConsultaDto> Etiquetas { get; set; } = new();
    public List<OpcaoConsultaDto> Vendedores { get; set; } = new();
    public List<OpcaoConsultaDto> CamposPesquisaveis { get; set; } = new();
    public List<FiltroSalvoDto> Filtros { get; set; } = new();
}
