using Lone.Contracts.Comum;
using Lone.Contracts.Integracoes;

namespace Lone.Cliente.Api;

/// <summary>Consultas externas feitas pela API (o aparelho nunca fala direto com Receita ou Correios).</summary>
public sealed class ConsultasApi
{
    private readonly ClienteApi _api;

    public ConsultasApi(ClienteApi api)
    {
        _api = api;
    }

    /// <summary>Nulo quando o CEP não existe.</summary>
    public Task<DadosCep?> ConsultarCepAsync(string cep, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<DadosCep>(Rotas.Consultas.Cep(cep), ct);

    /// <summary>Conferência do CEP pelo motor (não grava nada). Fonte fora do ar volta como resultado, não erro.</summary>
    public Task<DecisaoCepDto> ConferirCepAsync(ConferirCepRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<DecisaoCepDto>(Rotas.Consultas.ConferirCep, requisicao, ct: ct);

    /// <summary>Busca de CEP pelo endereço sem CEP: candidatos para escolher (não grava nada; nunca escolhe).</summary>
    public Task<DecisaoCepDto> BuscarCepPorEnderecoAsync(BuscarCepPorEnderecoRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<DecisaoCepDto>(Rotas.Consultas.BuscarCepPorEndereco, requisicao, ct: ct);

    // ---- F6: reconferência de CEPs em lote (nunca altera endereço) e limpeza do histórico técnico ----

    public Task<SelecaoReconferenciaCepDto> SelecionarReconferenciaAsync(FiltroReconferenciaCepDto filtro, CancellationToken ct = default) =>
        _api.PostAsync<SelecaoReconferenciaCepDto>(Rotas.Consultas.ReconferenciaSelecionar, filtro, ct: ct);

    public Task<ResumoReconferenciaCepDto> ProcessarReconferenciaAsync(ProcessarReconferenciaCepRequisicao bloco, CancellationToken ct = default) =>
        _api.PostAsync<ResumoReconferenciaCepDto>(Rotas.Consultas.ReconferenciaProcessar, bloco, ct: ct);

    public Task ConcluirReconferenciaAsync(ConcluirReconferenciaCepRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync(Rotas.Consultas.ReconferenciaConcluir, requisicao, ct: ct);

    public Task<SegundaOpiniaoCepDto> ConsultarOutraFonteAsync(string cep, CancellationToken ct = default) =>
        _api.PostAsync<SegundaOpiniaoCepDto>(Rotas.Consultas.SegundaOpiniaoCep, new SegundaOpiniaoCepRequisicao { Cep = cep }, ct: ct);

    public Task<LimpezaHistoricoCepDto> LimparHistoricoCepAsync(CancellationToken ct = default) =>
        _api.PostAsync<LimpezaHistoricoCepDto>(Rotas.Consultas.HistoricoCepLimpar, new { }, ct: ct);

    /// <summary>Nulo quando o CNPJ não existe.</summary>
    public Task<DadosCnpj?> ConsultarCnpjAsync(string cnpj, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<DadosCnpj>(Rotas.Consultas.Cnpj(cnpj), ct);
}
