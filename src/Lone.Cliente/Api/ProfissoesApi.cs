using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Profissoes;

namespace Lone.Cliente.Api;

/// <summary>Cadastro de profissões e tabela oficial da CBO. A API confere as permissões.</summary>
public sealed class ProfissoesApi
{
    private readonly ClienteApi _api;

    public ProfissoesApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<List<ProfissaoDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default) =>
        _api.GetAsync<List<ProfissaoDto>>(Rotas.Profissoes.Listar(incluirInativas), ct);

    public Task<ProfissaoDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<ProfissaoDto>(Rotas.Profissoes.PorId(id), ct);

    public Task<ProfissaoDto> SalvarAsync(ProfissaoDto profissao, CancellationToken ct = default) =>
        _api.PutAsync<ProfissaoDto>(Rotas.Profissoes.PorId(profissao.Id), profissao, ct);

    public Task<ProfissaoDto> DesativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<ProfissaoDto>(Rotas.Profissoes.Desativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<ProfissaoDto> ReativarAsync(Guid id, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<ProfissaoDto>(Rotas.Profissoes.Reativar(id), new AlterarSituacaoRequisicao { Versao = versao }, ct: ct);

    public Task<ResultadoMesclarProfissao> MesclarAsync(Guid origemId, Guid destinoId, byte[]? versao, CancellationToken ct = default) =>
        _api.PostAsync<ResultadoMesclarProfissao>(Rotas.Profissoes.Mesclar(origemId),
            new MesclarProfissaoRequisicao { DestinoId = destinoId, Versao = versao }, ct: ct);

    public Task<List<OcupacaoCboDto>> ListarCboAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<OcupacaoCboDto>>(Rotas.Profissoes.Cbo, ct);

    public Task<SituacaoCbo> ObterSituacaoCboAsync(CancellationToken ct = default) =>
        _api.GetAsync<SituacaoCbo>(Rotas.Profissoes.CboSituacao, ct);

    public Task<ResultadoImportacaoCbo> ImportarCboAsync(string nomeArquivo, byte[] arquivo, CancellationToken ct = default) =>
        _api.PostAsync<ResultadoImportacaoCbo>(Rotas.Profissoes.CboImportar,
            new ImportarCboRequisicao { NomeArquivo = nomeArquivo, Arquivo = arquivo }, ct: ct);
}
