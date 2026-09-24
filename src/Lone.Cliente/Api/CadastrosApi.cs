using System.Collections.Concurrent;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Municipios;
using Lone.Domain.Enums;

namespace Lone.Cliente.Api;

/// <summary>
/// Municípios do IBGE. A lista de cada UF é lida uma vez e guardada no aparelho enquanto o app está aberto:
/// o autocompletar filtra localmente, sem uma chamada por tecla.
/// </summary>
public sealed class MunicipiosApi
{
    private readonly ClienteApi _api;
    private readonly ConcurrentDictionary<string, Task<List<MunicipioDto>>> _porUf = new();

    public MunicipiosApi(ClienteApi api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<MunicipioDto>> ListarDaUfAsync(string uf, CancellationToken ct = default)
    {
        var sigla = uf.Trim().ToUpperInvariant();
        var tarefa = _porUf.GetOrAdd(sigla, s => _api.GetAsync<List<MunicipioDto>>(Rotas.Municipios.DaUf(s), CancellationToken.None));
        try
        {
            var lista = await tarefa.WaitAsync(ct);
            if (lista.Count == 0) _porUf.TryRemove(sigla, out _); // tabela ainda sendo carregada: tenta de novo depois
            return lista;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            _porUf.TryRemove(sigla, out _); // falhou: a próxima tentativa busca de novo
            throw;
        }
    }

    public Task<SituacaoMunicipios> ObterSituacaoAsync(CancellationToken ct = default) =>
        _api.GetAsync<SituacaoMunicipios>(Rotas.Municipios.Situacao, ct);

    public async Task<ResultadoAtualizacaoMunicipios> AtualizarAsync(CancellationToken ct = default)
    {
        var resultado = await _api.PostAsync<ResultadoAtualizacaoMunicipios>(Rotas.Municipios.Atualizar, new { }, ct: ct);
        _porUf.Clear();
        return resultado;
    }
}

/// <summary>Campos personalizados (definições). A API confere as permissões.</summary>
public sealed class CamposPersonalizadosApi
{
    private readonly ClienteApi _api;

    public CamposPersonalizadosApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<List<CampoPersonalizadoDto>> ListarAsync(EntidadePersonalizavel entidade, bool incluirInativos, CancellationToken ct = default) =>
        _api.GetAsync<List<CampoPersonalizadoDto>>(Rotas.CamposPersonalizados.Listar(entidade, incluirInativos), ct);

    public Task<CampoPersonalizadoDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<CampoPersonalizadoDto>(Rotas.CamposPersonalizados.PorId(id), ct);

    public Task<CampoPersonalizadoDto> SalvarAsync(CampoPersonalizadoDto campo, CancellationToken ct = default) =>
        _api.PutAsync<CampoPersonalizadoDto>(Rotas.CamposPersonalizados.PorId(campo.Id), campo, ct);

    public Task<CampoPersonalizadoDto> DesativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<CampoPersonalizadoDto>(Rotas.CamposPersonalizados.Desativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<CampoPersonalizadoDto> ReativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<CampoPersonalizadoDto>(Rotas.CamposPersonalizados.Reativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task ReordenarAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) =>
        _api.PutSemRespostaAsync(Rotas.CamposPersonalizados.Ordem, ids, ct);
}
