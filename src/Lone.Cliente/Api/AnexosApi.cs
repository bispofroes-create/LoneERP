using Lone.Contracts.Comum;
using Lone.Contracts.Documentos;

namespace Lone.Cliente.Api;

/// <summary>Anexos dos documentos das pessoas. A API confere permissões, formato e tamanho.</summary>
public sealed class AnexosApi
{
    private readonly ClienteApi _api;

    public AnexosApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<AnexoDto> EnviarAsync(Guid pessoaId, Guid documentoId, string nomeArquivo, byte[] conteudo, CancellationToken ct = default) =>
        _api.PostAsync<AnexoDto>(Rotas.Anexos.Enviar(pessoaId, documentoId),
            new EnviarAnexoRequisicao { NomeArquivo = nomeArquivo, Conteudo = conteudo }, ct: ct);

    public Task<AnexoConteudoDto> BaixarAsync(Guid id, CancellationToken ct = default) =>
        _api.GetAsync<AnexoConteudoDto>(Rotas.Anexos.Conteudo(id), ct);

    public Task<AnexoDto> DesativarAsync(Guid id, CancellationToken ct = default) =>
        _api.PostAsync<AnexoDto>(Rotas.Anexos.Desativar(id), new object(), ct: ct);

    public Task<AnexoDto> ReativarAsync(Guid id, CancellationToken ct = default) =>
        _api.PostAsync<AnexoDto>(Rotas.Anexos.Reativar(id), new object(), ct: ct);
}
