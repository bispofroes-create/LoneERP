using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Metas;

namespace Lone.Cliente.Api;

/// <summary>Equipes, indicadores e metas. A API confere as permissões e as mudanças de situação.</summary>
public sealed class MetasApi
{
    private readonly ClienteApi _api;

    public MetasApi(ClienteApi api)
    {
        _api = api;
    }

    // ---- Equipes e indicadores (mesmo formato dos outros cadastros) ----

    public Task<List<EquipeDto>> ListarEquipesAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<EquipeDto>>(Rotas.Estrutura.Listar(Rotas.Metas.Equipes, true), ct);

    public Task<EquipeDto?> ObterEquipeAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<EquipeDto>(Rotas.Estrutura.PorId(Rotas.Metas.Equipes, id), ct);

    public Task<EquipeDto> SalvarEquipeAsync(EquipeDto item, CancellationToken ct = default) =>
        _api.PutAsync<EquipeDto>(Rotas.Estrutura.PorId(Rotas.Metas.Equipes, item.Id), item, ct);

    public Task<List<IndicadorDto>> ListarIndicadoresAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<IndicadorDto>>(Rotas.Estrutura.Listar(Rotas.Metas.Indicadores, true), ct);

    public Task<IndicadorDto?> ObterIndicadorAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<IndicadorDto>(Rotas.Estrutura.PorId(Rotas.Metas.Indicadores, id), ct);

    public Task<IndicadorDto> SalvarIndicadorAsync(IndicadorDto item, CancellationToken ct = default) =>
        _api.PutAsync<IndicadorDto>(Rotas.Estrutura.PorId(Rotas.Metas.Indicadores, item.Id), item, ct);

    /// <summary>Desativa/reativa equipe ou indicador (<paramref name="grupo"/> = Rotas.Metas.Equipes/Indicadores).</summary>
    public Task<T> AlterarAtivoAsync<T>(string grupo, Guid id, bool ativar, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<T>(ativar ? Rotas.Estrutura.Reativar(grupo, id) : Rotas.Estrutura.Desativar(grupo, id),
                          new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    // ---- Metas ----

    public Task<List<MetaResumoDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default) =>
        _api.GetAsync<List<MetaResumoDto>>(incluirInativas ? Rotas.Metas.Grupo + "?incluirInativas=true" : Rotas.Metas.Grupo, ct);

    public Task<MetaOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default) =>
        _api.GetAsync<MetaOpcoesDto>(Rotas.Metas.Opcoes, ct);

    public Task<MetaDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<MetaDto>(Rotas.Metas.PorId(id), ct);

    public Task<MetaDto> SalvarAsync(MetaDto meta, CancellationToken ct = default) =>
        _api.PutAsync<MetaDto>(Rotas.Metas.PorId(meta.Id), meta, ct);

    public Task<MetaDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoMetaRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<MetaDto>(Rotas.Metas.Situacao(id), requisicao, ct: ct);

    public Task<MetaDto> LancarRealizadoAsync(Guid id, LancarRealizadoRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<MetaDto>(Rotas.Metas.Realizado(id), requisicao, ct: ct);

    public Task<ResultadoImportacaoRealizadoDto> ImportarRealizadoAsync(Guid id, ImportarRealizadoRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<ResultadoImportacaoRealizadoDto>(Rotas.Metas.Importar(id), requisicao, ct: ct);

    public Task<ApuracaoDto> ApurarAsync(Guid id, CancellationToken ct = default) =>
        _api.GetAsync<ApuracaoDto>(Rotas.Metas.Apuracao(id), ct);

    public Task<MetaDto> CancelarAsync(Guid id, CancellationToken ct = default) =>
        _api.PostAsync<MetaDto>(Rotas.Metas.Desativar(id), new { }, ct: ct);
}
