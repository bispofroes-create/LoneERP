using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comercial;
using Lone.Contracts.Comum;

namespace Lone.Cliente.Api;

/// <summary>Opções da aba "Cliente" e os cadastros comerciais (perfis, condições, tipos de carteira). A API confere as permissões.</summary>
public sealed class ComercialApi
{
    private readonly ClienteApi _api;

    public ComercialApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<ComercialOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default) =>
        _api.GetAsync<ComercialOpcoesDto>(Rotas.Comercial.Opcoes, ct);

    public Task<List<T>> ListarAsync<T>(string grupo, bool incluirInativos, CancellationToken ct = default) =>
        _api.GetAsync<List<T>>(Rotas.Estrutura.Listar(grupo, incluirInativos), ct);

    public Task<T?> ObterAsync<T>(string grupo, Guid id, CancellationToken ct = default) where T : class =>
        _api.GetOuNuloAsync<T>(Rotas.Estrutura.PorId(grupo, id), ct);

    public Task<T> SalvarAsync<T>(string grupo, Guid id, T item, CancellationToken ct = default) where T : notnull =>
        _api.PutAsync<T>(Rotas.Estrutura.PorId(grupo, id), item, ct);

    public Task<T> DesativarAsync<T>(string grupo, Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<T>(Rotas.Estrutura.Desativar(grupo, id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<T> ReativarAsync<T>(string grupo, Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<T>(Rotas.Estrutura.Reativar(grupo, id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    // ---- Motor Comercial, Fase 1c ----

    public Task<ParametrosComerciaisDto> ObterParametrosAsync(CancellationToken ct = default) =>
        _api.GetAsync<ParametrosComerciaisDto>(Rotas.Comercial.Parametros, ct);

    public Task<ParametrosComerciaisDto> SalvarParametrosAsync(ParametrosComerciaisDto dto, CancellationToken ct = default) =>
        _api.PutAsync<ParametrosComerciaisDto>(Rotas.Comercial.Parametros, dto, ct);

    public Task<List<CoberturaDto>> ListarCoberturasAsync(bool incluirEncerradas, CancellationToken ct = default) =>
        _api.GetAsync<List<CoberturaDto>>(Rotas.Comercial.ListarCoberturas(incluirEncerradas), ct);

    public Task<CoberturaDto?> ObterCoberturaAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<CoberturaDto>(Rotas.Comercial.CoberturaPorId(id), ct);

    public Task<CoberturaOpcoesDto> ListarOpcoesCoberturaAsync(CancellationToken ct = default) =>
        _api.GetAsync<CoberturaOpcoesDto>(Rotas.Comercial.CoberturasOpcoes, ct);

    public Task<CoberturaDto> SalvarCoberturaAsync(CoberturaDto dto, CancellationToken ct = default) =>
        _api.PutAsync<CoberturaDto>(Rotas.Comercial.CoberturaPorId(dto.Id), dto, ct);

    public Task<CoberturaDto> CancelarCoberturaAsync(Guid id, byte[]? versao, string motivo, CancellationToken ct = default) =>
        _api.PostAsync<CoberturaDto>(Rotas.Comercial.CancelarCobertura(id), new CancelarCoberturaRequisicao { Versao = versao, Motivo = motivo }, ct: ct);

    public Task<CarteiraVencendoDto> CarteiraVencendoAsync(int? dias, CancellationToken ct = default) =>
        _api.GetAsync<CarteiraVencendoDto>(dias is { } d ? $"{Rotas.Comercial.CarteiraVencendo}?dias={d}" : Rotas.Comercial.CarteiraVencendo, ct);

    // ---- Motor Comercial, Fase 1d ----

    public Task<TransferenciaOpcoesDto> ListarOpcoesTransferenciaAsync(CancellationToken ct = default) =>
        _api.GetAsync<TransferenciaOpcoesDto>(Rotas.Comercial.TransferenciasOpcoes, ct);

    public Task<List<TransferenciaDto>> ListarTransferenciasAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<TransferenciaDto>>(Rotas.Comercial.Transferencias, ct);

    public Task<TransferenciaDto?> ObterTransferenciaAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<TransferenciaDto>(Rotas.Comercial.TransferenciaPorId(id), ct);

    /// <summary>O que aconteceria, cliente a cliente (nada é gravado).</summary>
    public Task<PreviaTransferenciaDto> PreviaTransferenciaAsync(TransferenciaRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<PreviaTransferenciaDto>(Rotas.Comercial.TransferenciasPrevia, requisicao, ct: ct);

    public Task<TransferenciaDto> TransferirAsync(TransferenciaRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<TransferenciaDto>(Rotas.Comercial.Transferencias, requisicao, ct: ct);

    public Task<CarteiraEmDataDto> CarteiraEmDataAsync(Guid? clienteId, Guid? pessoaId, DateOnly data, CancellationToken ct = default) =>
        _api.GetAsync<CarteiraEmDataDto>(Rotas.Comercial.ConsultarCarteiraEmData(clienteId, pessoaId, data), ct);
}
