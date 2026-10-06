using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Contracts.Integracoes;

/// <summary>Filtro da reconferência de CEPs (F6). Só endereços ativos, no Brasil, de cadastros não arquivados.</summary>
public sealed class FiltroReconferenciaCepDto
{
    public bool NaoConferidos { get; set; } = true;
    public bool Divergentes { get; set; } = true;
    public bool NaoEncontrados { get; set; } = true;

    /// <summary>"Conferido" há mais tempo que a política (180 dias). Antigo não é divergente: só merece nova conferência.</summary>
    public bool ConferidosAntigos { get; set; } = true;

    public string? Uf { get; set; }
    public int? MunicipioId { get; set; }
    public int Limite { get; set; } = 1000;
}

/// <summary>A seleção: total que atende, ids (até o limite, em ordem previsível) e os que ficaram de fora sem CEP válido.</summary>
public sealed class SelecaoReconferenciaCepDto
{
    public int Total { get; set; }
    public List<Guid> Enderecos { get; set; } = new();
    public int SemCepValido { get; set; }
    public bool Truncada { get; set; }

    /// <summary>Acima disto a tela pede confirmação antes de começar.</summary>
    public int ConfirmarAcimaDe { get; set; }

    /// <summary>Endereços por chamada de processamento.</summary>
    public int ItensPorChamada { get; set; }

    /// <summary>Quantas vezes um endereço "adiado" (tempo da chamada esgotado) é reenviado antes de ficar como não processado.</summary>
    public int MaximoAdiamentos { get; set; }
}

/// <summary>Um bloco a processar (até "ItensPorChamada" endereços).</summary>
public sealed class ProcessarReconferenciaCepRequisicao
{
    public List<Guid> Enderecos { get; set; } = new();
}

/// <summary>O resultado de um endereço. Nunca traz endereço alterado: só a situação e o que explica a revisão.</summary>
public sealed class ItemReconferenciaCepDto
{
    public Guid EnderecoId { get; set; }
    public Guid? PessoaId { get; set; }
    public int? PessoaCodigo { get; set; }
    public string? PessoaNome { get; set; }
    public string? Endereco { get; set; }
    public string? Cep { get; set; }
    public ResultadoItemReconferencia Resultado { get; set; }
    public string Detalhe { get; set; } = string.Empty;

    /// <summary>Componentes do CEP gravado (os divergentes explicam a revisão).</summary>
    public List<ComponenteCepDto> Componentes { get; set; } = new();

    /// <summary>CEPs compatíveis achados (CEP gravado inexistente): só para o usuário ver; nada é escolhido nem aplicado.</summary>
    public List<CandidatoCepDto> Candidatos { get; set; } = new();
}

public sealed class ResumoReconferenciaCepDto
{
    public List<ItemReconferenciaCepDto> Itens { get; set; } = new();
    public int DuracaoMs { get; set; }
}

/// <summary>Resultado da limpeza do histórico técnico de consultas de CEP.</summary>
public sealed class LimpezaHistoricoCepDto
{
    public int Removidos { get; set; }
    public int RetencaoDias { get; set; }
}

/// <summary>
/// Fim de uma execução da reconferência (concluída ou cancelada), para o evento de auditoria da execução. As quantidades são
/// as que a tela somou das respostas dos blocos (o servidor processa cada bloco sem guardar estado).
/// </summary>
public sealed class ConcluirReconferenciaCepRequisicao
{
    public Guid ExecucaoId { get; set; }
    public FiltroReconferenciaCepDto Filtro { get; set; } = new();
    public bool Cancelada { get; set; }
    public int Total { get; set; }
    public int Conferidos { get; set; }
    public int Divergentes { get; set; }
    public int NaoEncontrados { get; set; }
    public int Indisponiveis { get; set; }
    public int Alterados { get; set; }
    public int NaoProcessados { get; set; }
    public int DuracaoSegundos { get; set; }
}
