using Lone.Contracts.Comum;
using Lone.Contracts.Pessoas;

namespace Lone.Cliente.Api;

/// <summary>Consulta avançada de pessoas, exportação e filtros salvos. A API confere as permissões.</summary>
public sealed class ConsultaPessoasApi
{
    private readonly ClienteApi _api;

    public ConsultaPessoasApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<OpcoesConsultaPessoasDto> OpcoesAsync(CancellationToken ct = default) =>
        _api.GetAsync<OpcoesConsultaPessoasDto>(Rotas.Pessoas.OpcoesConsulta, ct);

    public Task<PaginaPessoas> ConsultarAsync(ConsultaPessoasRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<PaginaPessoas>(Rotas.Pessoas.Consulta, requisicao, ct: ct);

    public Task<ArquivoExportado> ExportarAsync(CriteriosPessoas criterios, CancellationToken ct = default) =>
        _api.PostAsync<ArquivoExportado>(Rotas.Pessoas.Exportar, criterios, ct: ct);

    public Task<FiltroSalvoDto> SalvarFiltroAsync(FiltroSalvoDto filtro, CancellationToken ct = default) =>
        _api.PutAsync<FiltroSalvoDto>(Rotas.Pessoas.FiltroSalvo(filtro.Id), filtro, ct);

    public Task DesativarFiltroAsync(Guid id, CancellationToken ct = default) =>
        _api.PostAsync(Rotas.Pessoas.DesativarFiltro(id), ct);
}
