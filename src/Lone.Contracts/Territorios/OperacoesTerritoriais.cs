using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Contracts.Territorios;

// Fase 2b-1b — operações territoriais TE-, regras, exceções, atribuições e simulação (plano da 2b-1b, seções G a O).

/// <summary>Parâmetros do motor territorial (DN-08): um registro só.</summary>
public sealed class ParametrosTerritoriaisDto
{
    public byte[]? Versao { get; set; }

    /// <summary>Até quantos dias antes de hoje uma operação pode ter efeito (padrão 30).</summary>
    public int DiasRetroativosMaximo { get; set; }

    /// <summary>Somente leitura: o valor inicial e o teto aceito.</summary>
    public int Padrao { get; set; }
    public int Maximo { get; set; }
}

/// <summary>Um grupo de condições de uma regra (E entre as condições, como no filtro de Pessoas).</summary>
public sealed class GrupoCondicoesTerritorioDto
{
    public List<CondicaoFiltro> Condicoes { get; set; } = new();
}

/// <summary>
/// Os critérios de uma versão da regra (T6): candidatos = união dos grupos de inclusão menos a união dos grupos de exclusão.
/// Gravado em JSON na versão (ids), junto com o texto congelado.
/// </summary>
public sealed class GruposRegraTerritorioDto
{
    public List<GrupoCondicoesTerritorioDto> Inclusao { get; set; } = new();
    public List<GrupoCondicoesTerritorioDto> Exclusao { get; set; } = new();
}

/// <summary>Uma versão da regra do território.</summary>
public sealed class RegraTerritorioDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public Guid TerritorioId { get; set; }
    public int Numero { get; set; }
    public GruposRegraTerritorioDto Grupos { get; set; } = new();

    /// <summary>Os critérios como estavam escritos na publicação (nomes do dia).</summary>
    public string Criterios { get; set; } = string.Empty;
    public int? Prioridade { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public bool Ativo { get; set; }
    public Guid OperacaoId { get; set; }
    public string? Operacao { get; set; }

    /// <summary>Somente leitura: vigente hoje.</summary>
    public bool Vigente { get; set; }

    /// <summary>Somente leitura: há condição que o usuário não pode ver (financeiro): o texto mostra "condição restrita".</summary>
    public bool ComCondicaoRestrita { get; set; }
}

/// <summary>Uma exceção (Fixar/Retirar) de um cliente num território.</summary>
public sealed class ExcecaoTerritorioDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public Guid TerritorioId { get; set; }
    public string? Territorio { get; set; }
    public Guid PessoaId { get; set; }
    public string? Pessoa { get; set; }
    public TipoExcecaoTerritorio Tipo { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public OrigemExcecaoTerritorio Origem { get; set; }
    public bool Ativo { get; set; }
    public Guid OperacaoId { get; set; }
    public string? Operacao { get; set; }
    public bool Vigente { get; set; }
}

/// <summary>Uma atribuição (o resultado gravado do motor).</summary>
public sealed class AtribuicaoTerritorioDto
{
    public Guid Id { get; set; }
    public Guid MapaId { get; set; }
    public string? Mapa { get; set; }
    public Guid TerritorioId { get; set; }
    public string? TerritorioCodigo { get; set; }
    public string? Territorio { get; set; }

    /// <summary>"Brasil › Sudeste › MG" (a árvore na data consultada).</summary>
    public string? Caminho { get; set; }
    public Guid PessoaId { get; set; }
    public string? Pessoa { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public OrigemAtribuicaoTerritorio Origem { get; set; }

    /// <summary>"Regra v3" ou "Exceção: Fixar".</summary>
    public string? OrigemDescricao { get; set; }
    public bool Ativo { get; set; }
    public Guid OperacaoId { get; set; }
    public string? Operacao { get; set; }

    /// <summary>O mapa entra nos documentos (DN-15: contrato para os documentos futuros).</summary>
    public bool RegistrarNosDocumentos { get; set; }
}

/// <summary>A aba "Regras e clientes" da ficha do território.</summary>
public sealed class TerritorioMotorDto
{
    public Guid TerritorioId { get; set; }
    public List<RegraTerritorioDto> Regras { get; set; } = new();
    public List<ExcecaoTerritorioDto> Excecoes { get; set; } = new();

    /// <summary>Clientes atribuídos hoje (no alcance do usuário), até <see cref="LimiteClientes"/>.</summary>
    public List<AtribuicaoTerritorioDto> Clientes { get; set; } = new();
    public int TotalClientes { get; set; }
    public const int LimiteClientes = 500;

    /// <summary>Operações em aberto que mexem neste território.</summary>
    public List<OperacaoTerritorialResumoDto> OperacoesAbertas { get; set; } = new();
}

/// <summary>Uma mudança planejada, com o que é hoje (antes) e o proposto.</summary>
public sealed class MudancaTerritorialDto
{
    public Guid Id { get; set; }
    public int Ordem { get; set; }
    public TipoMudancaTerritorial Tipo { get; set; }
    public string TipoNome { get; set; } = string.Empty;
    public Guid? TerritorioId { get; set; }
    public string? Territorio { get; set; }
    public Guid? PessoaId { get; set; }
    public string? Pessoa { get; set; }
    public Guid? RegraBaseId { get; set; }
    public Guid? ExcecaoBaseId { get; set; }
    public Guid? PosicaoBaseId { get; set; }

    public GruposRegraTerritorioDto? Grupos { get; set; }
    public string? Criterios { get; set; }
    public int? Prioridade { get; set; }
    public string? Motivo { get; set; }
    public DateOnly? FimEm { get; set; }
    public Guid? NovoPaiId { get; set; }
    public string? NovoPai { get; set; }

    /// <summary>"Nova versão da regra de Curvelo", "Fixar João em MG Norte"...</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>A foto da base quando a mudança foi incluída, em texto.</summary>
    public string? Antes { get; set; }

    /// <summary>Preenchido quando a base não é mais a vigente: a mudança precisa ser revista (seção G, L5).</summary>
    public string? BaseVelha { get; set; }
}

/// <summary>Uma operação na lista.</summary>
public class OperacaoTerritorialResumoDto
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public Guid MapaId { get; set; }
    public string? Mapa { get; set; }
    public DateOnly EfeitoEm { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public SituacaoOperacaoTerritorial Situacao { get; set; }

    /// <summary>"Rascunho", "Simulada", "Agendada", "Em vigor", "Cancelada", "Desfeita" (indicadores da DN-04 incluídos).</summary>
    public string SituacaoNome { get; set; } = string.Empty;
    public bool Agendada { get; set; }
    public bool EmVigor { get; set; }
    public string CriadaPor { get; set; } = string.Empty;
    public DateTime CriadaEm { get; set; }
    public string? AplicadaPor { get; set; }
    public DateTime? AplicadaEm { get; set; }
    public int QuantidadeMudancas { get; set; }
    public int Entraram { get; set; }
    public int Sairam { get; set; }
    public int Mudaram { get; set; }
}

/// <summary>A operação aberta na tela: cabeçalho, mudanças, simulação atual, indicadores e o que o usuário pode fazer.</summary>
public sealed class OperacaoTerritorialDto : OperacaoTerritorialResumoDto
{
    public byte[]? Versao { get; set; }
    public string? Observacao { get; set; }
    public bool MapaExclusivo { get; set; }
    public string? CanceladaPor { get; set; }
    public DateTime? CanceladaEm { get; set; }
    public string? CanceladaMotivo { get; set; }
    public string? DesfeitaPor { get; set; }
    public DateTime? DesfeitaEm { get; set; }
    public string? DesfeitaMotivo { get; set; }
    public int OrigemAtualizada { get; set; }

    public List<MudancaTerritorialDto> Mudancas { get; set; } = new();
    public SimulacaoTerritorialResumoDto? SimulacaoAtual { get; set; }

    /// <summary>Indicador "simulação desatualizada" (DN-04) e os motivos.</summary>
    public bool Desatualizada { get; set; }
    public List<string> MotivosDesatualizada { get; set; } = new();

    /// <summary>Avisos (outra operação aberta no mesmo território/cliente; efeito futuro bloqueará as anteriores...).</summary>
    public List<string> Avisos { get; set; } = new();

    /// <summary>RT-1: a operação com efeito futuro que impede esta de ser aplicada (e se o usuário pode desfazê-la agora).</summary>
    public Guid? BloqueadaPorId { get; set; }
    public string? BloqueadaPor { get; set; }
    public bool PodeDesfazerBloqueio { get; set; }

    /// <summary>Última edição do rascunho (data, motivo, observação, mudança incluída ou retirada): quem e quando.</summary>
    public string? UltimaEdicaoPor { get; set; }
    public DateTime? UltimaEdicaoEm { get; set; }

    /// <summary>Quem, além de quem criou, editou o rascunho (qualquer PLANEJAR pode editar; a tela destaca).</summary>
    public List<string> EditadaTambemPor { get; set; } = new();

    /// <summary>
    /// Linha do tempo, mais recente primeiro: eventos (criada, mudança incluída/retirada — com "X alterou a operação criada
    /// por Y" quando for outra pessoa —, simulada, tentativa recusada, aplicada, cancelada, desfeita) e o antes → depois de
    /// data de efeito, motivo e observação.
    /// </summary>
    public List<Lone.Contracts.Auditoria.RegistroHistorico> Historico { get; set; } = new();

    public bool PodeEditar { get; set; }
    public bool PodeSimular { get; set; }
    public bool PodeAplicar { get; set; }
    public bool PodeCancelar { get; set; }
    public bool PodeDesfazer { get; set; }
}

/// <summary>Uma simulação guardada (DN-03).</summary>
public sealed class SimulacaoTerritorialResumoDto
{
    public Guid Id { get; set; }
    public DateTime SimuladaEm { get; set; }
    public string SimuladaPor { get; set; } = string.Empty;
    public DateOnly EfeitoEm { get; set; }
    public DateTime AtributosAvaliadosEm { get; set; }
    public bool Atual { get; set; }
    public int Entram { get; set; }
    public int Saem { get; set; }
    public int Mudam { get; set; }
    public int OrigemAtualizada { get; set; }
    public int EmConflito { get; set; }
    public int Inconsistencias { get; set; }
    public int DaOperacao { get; set; }
    public int Divergencias { get; set; }
}

/// <summary>Um cliente numa simulação ou no resultado aplicado.</summary>
public sealed class ItemOperacaoTerritorialDto
{
    public Guid PessoaId { get; set; }
    public long? Codigo { get; set; }
    public string? Pessoa { get; set; }
    public ResultadoAtribuicao Resultado { get; set; }
    public string ResultadoNome { get; set; } = string.Empty;
    public EfeitoNoCliente Efeito { get; set; }
    public string EfeitoNome { get; set; } = string.Empty;

    /// <summary>Simulação: desta operação ou divergência que já existia (DN-14). Nulo no resultado aplicado.</summary>
    public OrigemEfeitoSimulado? OrigemEfeito { get; set; }
    public Guid? TerritorioAtualId { get; set; }
    public string? TerritorioAtual { get; set; }
    public Guid? TerritorioPropostoId { get; set; }
    public string? TerritorioProposto { get; set; }

    /// <summary>O passo que decidiu, em texto ("Prioridade: a regra de MG Norte tem prioridade 1").</summary>
    public string? Motivo { get; set; }

    /// <summary>A explicação congelada (JSON): candidatos, perdedores e por quê, atributos lidos.</summary>
    public string Explicacao { get; set; } = "{}";
}

public sealed class PaginaItensOperacaoTerritorialDto
{
    public int Total { get; set; }
    public List<ItemOperacaoTerritorialDto> Itens { get; set; } = new();

    /// <summary>Com alcance restrito, só os clientes do alcance aparecem.</summary>
    public bool FiltradoPeloAlcance { get; set; }
}

/// <summary>Filtro dos itens (a tela mostra primeiro o que muda).</summary>
public sealed class FiltroItensOperacaoTerritorialDto
{
    public EfeitoNoCliente? Efeito { get; set; }
    public OrigemEfeitoSimulado? Origem { get; set; }
    public bool SoProblemas { get; set; }
    public string? Texto { get; set; }
    public int Pular { get; set; }
    public int Quantidade { get; set; } = 200;
    public const int QuantidadeMaxima = 1000;
}

public sealed class CriarOperacaoTerritorialRequisicao
{
    public Guid MapaId { get; set; }
    public DateOnly EfeitoEm { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? Observacao { get; set; }
}

public sealed class AlterarOperacaoTerritorialRequisicao
{
    public byte[]? Versao { get; set; }
    public DateOnly EfeitoEm { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? Observacao { get; set; }
}

/// <summary>Incluir uma mudança na operação (o alvo e o proposto; a base é a vigente no momento da inclusão).</summary>
public sealed class IncluirMudancaTerritorialRequisicao
{
    public byte[]? Versao { get; set; }
    public TipoMudancaTerritorial Tipo { get; set; }
    public Guid? TerritorioId { get; set; }
    public Guid? PessoaId { get; set; }
    public Guid? ExcecaoId { get; set; }
    public GruposRegraTerritorioDto? Grupos { get; set; }
    public int? Prioridade { get; set; }
    public string? Motivo { get; set; }
    public DateOnly? FimEm { get; set; }
    public Guid? NovoPaiId { get; set; }
}

/// <summary>Atalho "Mover cliente" (seção G): gera Retirar de A + Fixar em B.</summary>
public sealed class MoverClienteTerritorialRequisicao
{
    public byte[]? Versao { get; set; }
    public Guid PessoaId { get; set; }
    public Guid DeTerritorioId { get; set; }
    public Guid ParaTerritorioId { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateOnly? FimEm { get; set; }
}

public sealed class VersaoOperacaoTerritorialRequisicao
{
    public byte[]? Versao { get; set; }
}

public sealed class AplicarOperacaoTerritorialRequisicao
{
    public byte[]? Versao { get; set; }

    /// <summary>A simulação que o usuário conferiu (tem de ser a atual).</summary>
    public Guid SimulacaoId { get; set; }
}

public sealed class MotivoOperacaoTerritorialRequisicao
{
    public byte[]? Versao { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Opções da tela de operações.</summary>
public sealed class OperacoesTerritoriaisOpcoesDto
{
    public List<MapaTerritorialDto> Mapas { get; set; } = new();

    /// <summary>Os campos aceitos em regra (DN-07), com as escolhas; os restritos ao usuário vêm marcados.</summary>
    public List<CampoFiltroDto> CamposRegra { get; set; } = new();

    /// <summary>Campos que o usuário não pode incluir numa regra (falta a permissão): a tela mostra "condição restrita".</summary>
    public List<string> CamposRestritos { get; set; } = new();
    public ParametrosTerritoriaisDto Parametros { get; set; } = new();
    public bool PodePlanejar { get; set; }
    public bool PodeAplicar { get; set; }
}

/// <summary>Divergências de um mapa hoje (DN-14): o que o motor faria diferente do que está gravado.</summary>
public sealed class DivergenciasTerritoriaisDto
{
    public Guid MapaId { get; set; }
    public DateOnly Data { get; set; }
    public int Entram { get; set; }
    public int Saem { get; set; }
    public int Mudam { get; set; }
    public int OrigemAtualizada { get; set; }
    public int EmConflito { get; set; }
    public int Inconsistencias { get; set; }
    public PaginaItensOperacaoTerritorialDto Itens { get; set; } = new();
}

/// <summary>"Territórios do cliente X na data D" (contrato para documentos futuros, seção L).</summary>
public sealed class TerritoriosDoClienteDto
{
    public Guid PessoaId { get; set; }
    public DateOnly Data { get; set; }
    public List<AtribuicaoTerritorioDto> Territorios { get; set; } = new();
}
