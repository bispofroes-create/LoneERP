using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Contatos;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Contatos;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Contatos;

public interface ITipoMeioContatoAppService
{
    Task<List<TipoMeioContatoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<TipoMeioContatoDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<TipoMeioContatoDto> SalvarAsync(TipoMeioContatoDto dto, CancellationToken ct = default);
    Task<TipoMeioContatoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<TipoMeioContatoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Tipos de telefone/e-mail: permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class TipoMeioContatoAppService : ITipoMeioContatoAppService
{
    private readonly ITipoMeioContatoRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public TipoMeioContatoAppService(ITipoMeioContatoRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<TipoMeioContatoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.Tipos);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);

        var tipos = await _repositorio.ListarAsync(incluirInativos, ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return tipos.Select(t => ParaDto(t, usos.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<TipoMeioContatoDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Tipos);
        var tipo = await _repositorio.ObterAsync(id, ct);
        return tipo is null ? null : await ParaDtoComUsoAsync(tipo, ct);
    }

    public async Task<TipoMeioContatoDto> SalvarAsync(TipoMeioContatoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Tipos);

        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        var dados = new TipoMeioContato
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = dto.Nome,
            Categoria = anterior?.Categoria ?? dto.Categoria, // a categoria não muda depois de criado
            Ordem = dto.Ordem,
            Ativo = anterior?.Ativo ?? true
        };
        if (anterior is null && dados.Ordem <= 0) dados.Ordem = await _repositorio.ProximaOrdemAsync(dados.Categoria, ct);

        RegrasMeioContato.Normalizar(dados);
        var erros = RegrasMeioContato.Validar(dados);
        if (dados.Nome.Length > 0 && await _repositorio.NomeEmUsoAsync(dados.Categoria, dados.Nome, dados.Id, ct))
            erros.Add($"Já existe o tipo \"{dados.Nome}\" nesta categoria (ativo ou desativado; maiúsculas e acentos não contam).");
        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        if (anterior is null)
            dados.RegistrarEvento($"Tipo '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Tipo '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleAsync(dados.Id, ct);
    }

    public Task<TipoMeioContatoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, t => t.Desativar(), ct);

    public Task<TipoMeioContatoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, t => t.Reativar(), ct);

    private async Task<TipoMeioContatoDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<TipoMeioContato> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Tipos);
        var tipo = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este tipo não existe mais."]);
        tipo.Versao = requisicao.Versao ?? tipo.Versao;
        acao(tipo);
        await _repositorio.SalvarAsync(tipo, novo: false, ct);
        return await ReleAsync(id, ct);
    }

    private async Task<TipoMeioContatoDto> ReleAsync(Guid id, CancellationToken ct)
    {
        var gravado = await _repositorio.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        return await ParaDtoComUsoAsync(gravado, ct);
    }

    private async Task<TipoMeioContatoDto> ParaDtoComUsoAsync(TipoMeioContato tipo, CancellationToken ct) =>
        ParaDto(tipo, (await _repositorio.ContarUsosAsync(tipo.Id, ct)).GetValueOrDefault(tipo.Id));

    private static TipoMeioContatoDto ParaDto(TipoMeioContato t, int usos) => new()
    {
        Id = t.Id,
        Versao = t.Versao,
        Nome = t.Nome,
        Categoria = t.Categoria,
        Ordem = t.Ordem,
        Ativo = t.Ativo,
        QuantidadeUsos = usos
    };
}
