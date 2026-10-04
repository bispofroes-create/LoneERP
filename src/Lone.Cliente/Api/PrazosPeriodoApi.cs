using Lone.Cliente.Formularios;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Domain.Entidades;

namespace Lone.Cliente.Api;

/// <summary>Cadastro "Prazos de período" (menu do usuário › Administração). A API confere as permissões.</summary>
public sealed class PrazosPeriodoApi
{
    private readonly ClienteApi _api;

    public PrazosPeriodoApi(ClienteApi api) => _api = api;

    public Task<List<PrazoPeriodoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default) =>
        _api.GetAsync<List<PrazoPeriodoDto>>(Rotas.Estrutura.Listar(Rotas.PrazosPeriodo.Grupo, incluirInativos), ct);

    public Task<PrazoPeriodoDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<PrazoPeriodoDto>(Rotas.Estrutura.PorId(Rotas.PrazosPeriodo.Grupo, id), ct);

    public Task<PrazoPeriodoDto> SalvarAsync(PrazoPeriodoDto item, CancellationToken ct = default) =>
        _api.PutAsync<PrazoPeriodoDto>(Rotas.Estrutura.PorId(Rotas.PrazosPeriodo.Grupo, item.Id), item, ct);

    public Task<PrazoPeriodoDto> DesativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<PrazoPeriodoDto>(Rotas.Estrutura.Desativar(Rotas.PrazosPeriodo.Grupo, id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<PrazoPeriodoDto> ReativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<PrazoPeriodoDto>(Rotas.Estrutura.Reativar(Rotas.PrazosPeriodo.Grupo, id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);
}

/// <summary>
/// Os prazos prontos do campo Prazo (▾), lidos do cadastro na primeira vez que a lista abre e guardados por alguns
/// minutos. Sem resposta da API, valem os de antes do cadastro (<see cref="CalculoPrazo.Padrao"/>). Gravar no cadastro
/// chama <see cref="Invalidar"/>: a próxima abertura relê.
/// </summary>
public sealed class PrazosProntos
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);
    private readonly PrazosPeriodoApi _api;
    private IReadOnlyList<PrazoPronto>? _lista;
    private DateTime _lidoEm;

    public PrazosProntos(PrazosPeriodoApi api) => _api = api;

    public async Task<IReadOnlyList<PrazoPronto>> ObterAsync(CancellationToken ct = default)
    {
        if (_lista is { } lista && DateTime.UtcNow - _lidoEm < Validade) return lista;
        try
        {
            var itens = await _api.ListarAsync(incluirInativos: false, ct);
            _lista = itens.Count > 0 ? itens.Select(Converter).ToList() : CalculoPrazo.Padrao;
            _lidoEm = DateTime.UtcNow;
            return _lista;
        }
        catch (Exception)
        {
            return _lista ?? CalculoPrazo.Padrao;
        }
    }

    public void Invalidar() => _lista = null;

    public static PrazoPronto Converter(PrazoPeriodoDto p) => p.Unidade switch
    {
        UnidadePrazo.Anos => new PrazoPronto(p.Nome, Anos: p.Quantidade),
        UnidadePrazo.Meses => new PrazoPronto(p.Nome, Meses: p.Quantidade),
        _ => new PrazoPronto(p.Nome, Dias: p.Quantidade)
    };
}
