using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Territorios;

namespace Lone.Cliente.Api;

/// <summary>Tipos de território, mapas territoriais e a árvore (Fase 2b-1a). A API confere permissões e regras.</summary>
public sealed class TerritoriosApi
{
    private readonly ClienteApi _api;

    public TerritoriosApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<TerritoriosOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default) =>
        _api.GetAsync<TerritoriosOpcoesDto>(Rotas.Territorios.Opcoes, ct);

    // ---- Tipos ----

    public Task<List<TipoTerritorioDto>> ListarTiposAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<TipoTerritorioDto>>(Rotas.Estrutura.Listar(Rotas.Territorios.Tipos, true), ct);

    public Task<TipoTerritorioDto?> ObterTipoAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<TipoTerritorioDto>(Rotas.Estrutura.PorId(Rotas.Territorios.Tipos, id), ct);

    public Task<TipoTerritorioDto> SalvarTipoAsync(TipoTerritorioDto item, CancellationToken ct = default) =>
        _api.PutAsync<TipoTerritorioDto>(Rotas.Estrutura.PorId(Rotas.Territorios.Tipos, item.Id), item, ct);

    // ---- Mapas ----

    public Task<List<MapaTerritorialDto>> ListarMapasAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<MapaTerritorialDto>>(Rotas.Estrutura.Listar(Rotas.Territorios.Mapas, true), ct);

    public Task<MapaTerritorialDto?> ObterMapaAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<MapaTerritorialDto>(Rotas.Estrutura.PorId(Rotas.Territorios.Mapas, id), ct);

    public Task<MapaTerritorialDto> SalvarMapaAsync(MapaTerritorialDto item, CancellationToken ct = default) =>
        _api.PutAsync<MapaTerritorialDto>(Rotas.Estrutura.PorId(Rotas.Territorios.Mapas, item.Id), item, ct);

    /// <summary>Desativa/reativa tipo ou mapa (<paramref name="grupo"/> = Rotas.Territorios.Tipos/Mapas).</summary>
    public Task<T> AlterarAtivoAsync<T>(string grupo, Guid id, bool ativar, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<T>(ativar ? Rotas.Estrutura.Reativar(grupo, id) : Rotas.Estrutura.Desativar(grupo, id),
                          new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    // ---- Territórios ----

    public Task<ArvoreTerritorialDto> ListarDoMapaAsync(Guid mapaId, CancellationToken ct = default) =>
        _api.GetAsync<ArvoreTerritorialDto>(Rotas.Territorios.DoMapa(mapaId), ct);

    public Task<TerritorioDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<TerritorioDto>(Rotas.Territorios.PorId(id), ct);

    public Task<TerritorioDto> SalvarAsync(TerritorioDto item, CancellationToken ct = default) =>
        _api.PutAsync<TerritorioDto>(Rotas.Territorios.PorId(item.Id), item, ct);

    public Task<TerritorioDto> EncerrarAsync(Guid id, byte[]? versao, byte[]? versaoArvore, string? motivo, CancellationToken ct = default) =>
        _api.PostAsync<TerritorioDto>(Rotas.Territorios.Encerrar(id),
            new AlterarSituacaoTerritorioRequisicao { Versao = versao, VersaoArvore = versaoArvore, Motivo = motivo }, ct: ct);

    public Task<TerritorioDto> ReativarAsync(Guid id, byte[]? versao, byte[]? versaoArvore, CancellationToken ct = default) =>
        _api.PostAsync<TerritorioDto>(Rotas.Territorios.Reativar(id),
            new AlterarSituacaoTerritorioRequisicao { Versao = versao, VersaoArvore = versaoArvore }, ct: ct);

    // ---- Motor (Fase 2b-1b): regras, operações TE-, simulação ----

    public Task<TerritorioMotorDto> MotorDoTerritorioAsync(Guid territorioId, CancellationToken ct = default) =>
        _api.GetAsync<TerritorioMotorDto>(Rotas.Territorios.Motor(territorioId), ct);

    public Task<TerritoriosDoClienteDto> DoClienteAsync(Guid pessoaId, DateOnly data, CancellationToken ct = default) =>
        _api.GetAsync<TerritoriosDoClienteDto>(Rotas.Territorios.DoCliente(pessoaId, data), ct);

    public Task<ParametrosTerritoriaisDto> ObterParametrosAsync(CancellationToken ct = default) =>
        _api.GetAsync<ParametrosTerritoriaisDto>(Rotas.Territorios.Parametros, ct);

    public Task<ParametrosTerritoriaisDto> SalvarParametrosAsync(ParametrosTerritoriaisDto dto, CancellationToken ct = default) =>
        _api.PutAsync<ParametrosTerritoriaisDto>(Rotas.Territorios.Parametros, dto, ct);

    public Task<OperacoesTerritoriaisOpcoesDto> OpcoesOperacoesAsync(CancellationToken ct = default) =>
        _api.GetAsync<OperacoesTerritoriaisOpcoesDto>(Rotas.Territorios.OperacoesOpcoes, ct);

    public Task<List<OperacaoTerritorialResumoDto>> ListarOperacoesAsync(Guid? mapaId, CancellationToken ct = default) =>
        _api.GetAsync<List<OperacaoTerritorialResumoDto>>(Rotas.Territorios.OperacoesDoMapa(mapaId), ct);

    public Task<OperacaoTerritorialDto?> ObterOperacaoAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<OperacaoTerritorialDto>(Rotas.Territorios.Operacao(id), ct);

    public Task<OperacaoTerritorialDto> CriarOperacaoAsync(CriarOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<OperacaoTerritorialDto>(Rotas.Territorios.Operacoes, requisicao, ct: ct);

    public Task<OperacaoTerritorialDto> AlterarOperacaoAsync(Guid id, AlterarOperacaoTerritorialRequisicao requisicao, CancellationToken ct = default) =>
        _api.PutAsync<OperacaoTerritorialDto>(Rotas.Territorios.Operacao(id), requisicao, ct);

    public Task<OperacaoTerritorialDto> IncluirMudancaAsync(Guid id, IncluirMudancaTerritorialRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<OperacaoTerritorialDto>(Rotas.Territorios.OperacaoMudancas(id), requisicao, ct: ct);

    public Task<OperacaoTerritorialDto> RetirarMudancaAsync(Guid id, Guid mudancaId, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<OperacaoTerritorialDto>(Rotas.Territorios.OperacaoRetirarMudanca(id, mudancaId), new VersaoOperacaoTerritorialRequisicao { Versao = versao }, ct: ct);

    public Task<OperacaoTerritorialDto> MoverClienteAsync(Guid id, MoverClienteTerritorialRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<OperacaoTerritorialDto>(Rotas.Territorios.OperacaoMoverCliente(id), requisicao, ct: ct);

    public Task<OperacaoTerritorialDto> SimularAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<OperacaoTerritorialDto>(Rotas.Territorios.OperacaoSimular(id), new VersaoOperacaoTerritorialRequisicao { Versao = versao }, ct: ct);

    public Task<List<SimulacaoTerritorialResumoDto>> SimulacoesAsync(Guid id, CancellationToken ct = default) =>
        _api.GetAsync<List<SimulacaoTerritorialResumoDto>>(Rotas.Territorios.OperacaoSimulacoes(id), ct);

    public Task<PaginaItensOperacaoTerritorialDto> ItensSimulacaoAsync(Guid simulacaoId, FiltroItensOperacaoTerritorialDto filtro, CancellationToken ct = default) =>
        _api.PostAsync<PaginaItensOperacaoTerritorialDto>(Rotas.Territorios.SimulacaoItens(simulacaoId), filtro, ct: ct);

    public Task<PaginaItensOperacaoTerritorialDto> ItensAplicadosAsync(Guid id, FiltroItensOperacaoTerritorialDto filtro, CancellationToken ct = default) =>
        _api.PostAsync<PaginaItensOperacaoTerritorialDto>(Rotas.Territorios.OperacaoItens(id), filtro, ct: ct);

    public Task<DivergenciasTerritoriaisDto> DivergenciasAsync(Guid mapaId, FiltroItensOperacaoTerritorialDto filtro, CancellationToken ct = default) =>
        _api.PostAsync<DivergenciasTerritoriaisDto>(Rotas.Territorios.Divergencias(mapaId), filtro, ct: ct);

    public Task<OperacaoTerritorialDto> AplicarAsync(Guid id, byte[]? versao, Guid simulacaoId, CancellationToken ct = default) =>
        _api.PostAsync<OperacaoTerritorialDto>(Rotas.Territorios.OperacaoAplicar(id),
            new AplicarOperacaoTerritorialRequisicao { Versao = versao, SimulacaoId = simulacaoId }, ct: ct);

    public Task<OperacaoTerritorialDto> CancelarAsync(Guid id, byte[]? versao, string motivo, CancellationToken ct = default) =>
        _api.PostAsync<OperacaoTerritorialDto>(Rotas.Territorios.OperacaoCancelar(id), new MotivoOperacaoTerritorialRequisicao { Versao = versao, Motivo = motivo }, ct: ct);

    public Task<OperacaoTerritorialDto> DesfazerAsync(Guid id, byte[]? versao, string motivo, CancellationToken ct = default) =>
        _api.PostAsync<OperacaoTerritorialDto>(Rotas.Territorios.OperacaoDesfazer(id), new MotivoOperacaoTerritorialRequisicao { Versao = versao, Motivo = motivo }, ct: ct);
}
