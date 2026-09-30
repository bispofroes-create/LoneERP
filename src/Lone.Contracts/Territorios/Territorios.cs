using Lone.Domain.Enums;

namespace Lone.Contracts.Territorios;

/// <summary>Tipo de território (classificação livre: Geográfico, Segmento, Estratégico...).</summary>
public sealed class TipoTerritorioDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }

    /// <summary>Informado ao criar; depois não muda (vazio = mantém o gravado; diferente = recusado).</summary>
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: nasceu com a base (pode ser renomeado ou desativado; o código não muda).</summary>
    public bool DoSistema { get; set; }

    /// <summary>Somente leitura: territórios que usam o tipo.</summary>
    public int QuantidadeUsos { get; set; }
}

/// <summary>Mapa territorial: dimensão independente de atribuição (decisão T1).</summary>
public sealed class MapaTerritorialDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    /// <summary>Empresa do grupo (nula = o grupo todo).</summary>
    public Guid? EmpresaId { get; set; }

    /// <summary>Somente leitura: nome da empresa.</summary>
    public string? Empresa { get; set; }
    public bool Exclusivo { get; set; } = true;

    /// <summary>Finalidade do endereço que as regras de endereço consideram (o principal dessa finalidade).</summary>
    public Guid FinalidadeEnderecoReferenciaId { get; set; }

    /// <summary>Somente leitura: nome da finalidade.</summary>
    public string? FinalidadeEnderecoReferencia { get; set; }

    /// <summary>
    /// DN-15: o território deste mapa é copiado para os documentos (pedido, venda, comissão...) quando eles existirem. Nesta
    /// fase só prepara o contrato; não muda nenhum comportamento.
    /// </summary>
    public bool RegistrarNosDocumentos { get; set; }

    /// <summary>Universo: classificações de pessoa que podem ser atribuídas (as ativas).</summary>
    public List<Guid> Classificacoes { get; set; } = new();
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: o mapa tem regras publicadas ou atribuições (empresa, exclusividade, universo e endereço travam).</summary>
    public bool EmUso { get; set; }

    /// <summary>Somente leitura: territórios ativos no mapa.</summary>
    public int TerritoriosAtivos { get; set; }
}

/// <summary>A árvore do mapa e a versão dela (a tela devolve a versão em toda mudança de estrutura).</summary>
public sealed class ArvoreTerritorialDto
{
    /// <summary>Versão da árvore quando foi lida (lida antes dos territórios: se mudar no meio, a gravação é recusada).</summary>
    public byte[]? VersaoArvore { get; set; }
    public List<TerritorioResumoDto> Territorios { get; set; } = new();
}

/// <summary>Encerrar ou reativar um território (mudança de estrutura: exige a versão da árvore que a tela mostrava).</summary>
public sealed class AlterarSituacaoTerritorioRequisicao
{
    public byte[]? Versao { get; set; }
    public byte[]? VersaoArvore { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>Linha da árvore (a tela monta a hierarquia pelo PaiId).</summary>
public sealed class TerritorioResumoDto
{
    public Guid Id { get; set; }
    public Guid MapaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public Guid TipoId { get; set; }
    public string? Tipo { get; set; }
    public Guid? PaiId { get; set; }
    public SituacaoTerritorio Situacao { get; set; }
    public DateOnly? InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }

    /// <summary>Responsáveis vigentes hoje, em texto ("Vendedor: João · Supervisor: Equipe Sul").</summary>
    public string? ResponsaveisHoje { get; set; }

    /// <summary>Tem regra publicada ou atribuição (mudanças de estrutura só por operação territorial).</summary>
    public bool ComUso { get; set; }
}

/// <summary>Ficha do território.</summary>
public sealed class TerritorioDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }

    /// <summary>
    /// Enviado: a versão da árvore que a tela mostrava (<see cref="ArvoreTerritorialDto.VersaoArvore"/>). Exigida quando a
    /// gravação muda a estrutura (criar, mover, mudar o início); a árvore mudou depois = conflito, nada é gravado.
    /// </summary>
    public byte[]? VersaoArvore { get; set; }
    public Guid MapaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public Guid TipoId { get; set; }
    public Guid? PaiId { get; set; }
    public string? Descricao { get; set; }

    /// <summary>Desde quando o território existe (padrão: hoje). Sem uso pode ser corrigido; não pode ser futuro.</summary>
    public DateOnly? InicioEm { get; set; }

    /// <summary>Somente leitura.</summary>
    public SituacaoTerritorio Situacao { get; set; }

    /// <summary>Somente leitura: último dia (encerrado).</summary>
    public DateOnly? FimEm { get; set; }

    /// <summary>Somente leitura: "Brasil › Sudeste" (os territórios acima, da raiz até o pai).</summary>
    public string? Caminho { get; set; }

    /// <summary>Somente leitura.</summary>
    public bool ComUso { get; set; }

    /// <summary>Somente leitura: onde o território esteve na árvore, do mais recente para o mais antigo.</summary>
    public List<TerritorioPosicaoDto> Posicoes { get; set; } = new();
    public List<TerritorioResponsavelDto> Responsaveis { get; set; } = new();
}

public sealed class TerritorioPosicaoDto
{
    public Guid? PaiId { get; set; }

    /// <summary>Nome do território acima (nulo = primeiro nível).</summary>
    public string? Pai { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public bool Ativo { get; set; } = true;
}

public sealed class TerritorioResponsavelDto
{
    public Guid Id { get; set; }

    /// <summary>Uma pessoa ou uma equipe (uma das duas).</summary>
    public Guid? PessoaId { get; set; }
    public Guid? EquipeId { get; set; }

    /// <summary>Somente leitura: nome da pessoa ou da equipe.</summary>
    public string? Nome { get; set; }

    /// <summary>Função: o papel comercial.</summary>
    public Guid TipoCarteiraId { get; set; }

    /// <summary>Somente leitura: nome da função.</summary>
    public string? Funcao { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public string? Observacao { get; set; }

    /// <summary>Falso = anulado antes de começar (incluído por engano). Quem já começou não é anulado: encerra pelo fim.</summary>
    public bool Ativo { get; set; } = true;
}

/// <summary>Opções das telas de territórios numa chamada só.</summary>
public sealed class TerritoriosOpcoesDto
{
    public List<TipoTerritorioDto> Tipos { get; set; } = new();
    public List<MapaTerritorialDto> Mapas { get; set; } = new();

    /// <summary>Funções possíveis dos responsáveis: os papéis comerciais (com "Quem pode ser").</summary>
    public List<Lone.Contracts.Comercial.TipoCarteiraDto> Funcoes { get; set; } = new();

    /// <summary>Pessoas ativas que podem ocupar algum papel comercial, com as classificações delas.</summary>
    public List<Lone.Contracts.Comercial.AtendenteOpcaoDto> Pessoas { get; set; } = new();

    /// <summary>Equipes ativas.</summary>
    public List<Lone.Contracts.Colaboradores.PessoaOpcaoDto> Equipes { get; set; } = new();
    public List<Lone.Contracts.Empresas.EmpresaResumo> Empresas { get; set; } = new();

    /// <summary>Classificações de pessoa (universo do mapa), com as desativadas.</summary>
    public List<Lone.Contracts.Comercial.ClassificacaoOpcaoDto> Classificacoes { get; set; } = new();

    /// <summary>Finalidades de endereço (endereço de referência do mapa), com as desativadas.</summary>
    public List<Lone.Contracts.Enderecos.FinalidadeEnderecoDto> Finalidades { get; set; } = new();
}
