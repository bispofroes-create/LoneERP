using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Etiquetas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Etiquetas;
using Lone.Domain.Validacao;

namespace Lone.Application.Etiquetas;

public interface IEtiquetaAppService
{
    /// <summary>
    /// Quem vê o cadastro de pessoas lê as etiquetas (para marcar e filtrar); só quem gerencia etiquetas recebe a
    /// quantidade de cadastros de cada uma.
    /// </summary>
    Task<List<EtiquetaDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default);
    Task<EtiquetaDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<EtiquetaDto> SalvarAsync(EtiquetaDto dto, CancellationToken ct = default);
    Task<EtiquetaDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<EtiquetaDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<ResultadoMesclarEtiqueta> MesclarAsync(Guid origemId, MesclarEtiquetaRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Cadastro de etiquetas: permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class EtiquetaAppService : IEtiquetaAppService
{
    private readonly IEtiquetaRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public EtiquetaAppService(IEtiquetaRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<EtiquetaDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.Etiquetas);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);

        var etiquetas = await _repositorio.ListarAsync(incluirInativas, ct);
        var uso = gerencia ? await _repositorio.ContarPessoasAsync(null, ct) : new Dictionary<Guid, int>();
        return etiquetas.Select(e => ParaDto(e, uso.GetValueOrDefault(e.Id))).ToList();
    }

    public async Task<EtiquetaDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Etiquetas);
        var etiqueta = await _repositorio.ObterAsync(id, ct);
        return etiqueta is null ? null : await ParaDtoComUsoAsync(etiqueta, ct);
    }

    public async Task<EtiquetaDto> SalvarAsync(EtiquetaDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Etiquetas);

        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        var nova = anterior is null;
        var dados = new Etiqueta
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = dto.Nome,
            Descricao = dto.Descricao,
            // Ativar/desativar têm ações próprias.
            Ativo = anterior?.Ativo ?? true
        };

        RegrasEtiqueta.Normalizar(dados);
        var erros = RegrasEtiqueta.Validar(dados);
        if (dados.Nome.Length > 0 && await _repositorio.NomeEmUsoAsync(dados.Nome, dados.Id, ct))
            erros.Add($"Já existe a etiqueta \"{dados.Nome}\" (ativa ou desativada; maiúsculas e acentos não contam). Use a existente ou reative-a.");
        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        if (anterior is null)
            dados.RegistrarEvento($"Etiqueta '{dados.Nome}' criada.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Etiqueta '{anterior.Nome}' renomeada para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, nova, ct);
        return await ReleAsync(dados.Id, ct);
    }

    public Task<EtiquetaDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, e => e.Desativar(), ct);

    public Task<EtiquetaDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, e => e.Reativar(), ct);

    public async Task<ResultadoMesclarEtiqueta> MesclarAsync(Guid origemId, MesclarEtiquetaRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Etiquetas);

        if (origemId == requisicao.DestinoId)
            throw new ValidacaoException(["Escolha outra etiqueta para receber os cadastros."]);
        var origem = await _repositorio.ObterAsync(origemId, ct) ?? throw new ValidacaoException(["Esta etiqueta não existe mais."]);
        var destino = await _repositorio.ObterAsync(requisicao.DestinoId, ct)
                      ?? throw new ValidacaoException(["A etiqueta escolhida para receber os cadastros não existe mais."]);
        if (!destino.Ativo)
            throw new ValidacaoException([$"A etiqueta \"{destino.Nome}\" está desativada: reative-a antes de mesclar nela."]);

        origem.Versao = requisicao.Versao ?? origem.Versao; // a versão que o usuário via: se mudou, dá conflito
        origem.Desativar();
        origem.RegistrarEvento($"Etiqueta '{origem.Nome}' mesclada em '{destino.Nome}': os cadastros passaram para '{destino.Nome}'.");
        destino.RegistrarEvento($"Etiqueta '{destino.Nome}' recebeu os cadastros da etiqueta '{origem.Nome}' (mesclagem).");

        var cadastros = await _repositorio.MesclarAsync(origem, destino, ct);
        return new ResultadoMesclarEtiqueta
        {
            Origem = await ReleAsync(origem.Id, ct),
            Destino = await ReleAsync(destino.Id, ct),
            CadastrosAlterados = cadastros
        };
    }

    private async Task<EtiquetaDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<Etiqueta> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Etiquetas);
        var etiqueta = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Esta etiqueta não existe mais."]);
        etiqueta.Versao = requisicao.Versao ?? etiqueta.Versao;
        acao(etiqueta);
        await _repositorio.SalvarAsync(etiqueta, nova: false, ct);
        return await ReleAsync(id, ct);
    }

    private async Task<EtiquetaDto> ReleAsync(Guid id, CancellationToken ct)
    {
        var gravada = await _repositorio.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        return await ParaDtoComUsoAsync(gravada, ct);
    }

    private async Task<EtiquetaDto> ParaDtoComUsoAsync(Etiqueta etiqueta, CancellationToken ct) =>
        ParaDto(etiqueta, (await _repositorio.ContarPessoasAsync(etiqueta.Id, ct)).GetValueOrDefault(etiqueta.Id));

    internal static EtiquetaDto ParaDto(Etiqueta e, int quantidadePessoas) => new()
    {
        Id = e.Id,
        Versao = e.Versao,
        Nome = e.Nome,
        Descricao = e.Descricao,
        Ativo = e.Ativo,
        QuantidadePessoas = quantidadePessoas
    };
}
