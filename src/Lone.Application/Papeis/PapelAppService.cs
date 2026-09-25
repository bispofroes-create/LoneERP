using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Papeis;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Papeis;
using Lone.Domain.Validacao;

namespace Lone.Application.Papeis;

public interface IPapelAppService
{
    /// <summary>Quem vê o cadastro de pessoas lê os papéis (para marcar e filtrar); só quem gerencia recebe a quantidade de cadastros.</summary>
    Task<List<PapelCadastroDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<PapelCadastroDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<PapelCadastroDto> SalvarAsync(PapelCadastroDto dto, CancellationToken ct = default);
    Task<PapelCadastroDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<PapelCadastroDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Cadastro de papéis: permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class PapelAppService : IPapelAppService
{
    private readonly IPapelRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public PapelAppService(IPapelRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<PapelCadastroDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.Papeis);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);

        var papeis = await _repositorio.ListarAsync(incluirInativos, ct);
        var uso = gerencia ? await _repositorio.ContarPessoasAsync(null, ct) : new Dictionary<Guid, int>();
        return papeis.Select(p => ParaDto(p, uso.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<PapelCadastroDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Papeis);
        var papel = await _repositorio.ObterAsync(id, ct);
        return papel is null ? null : await ParaDtoComUsoAsync(papel, ct);
    }

    public async Task<PapelCadastroDto> SalvarAsync(PapelCadastroDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Papeis);

        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        var dados = new Papel
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = dto.Nome,
            Descricao = dto.Descricao,
            Ordem = dto.Ordem,
            // Código não muda depois de criado; papel de sistema e situação não vêm do aplicativo.
            Codigo = anterior?.Codigo ?? dto.Codigo,
            PapelSistema = anterior?.PapelSistema,
            Ativo = anterior?.Ativo ?? true
        };
        if (anterior is null && dados.Ordem <= 0) dados.Ordem = await _repositorio.ProximaOrdemAsync(ct);

        RegrasPapel.Normalizar(dados);
        var erros = RegrasPapel.Validar(dados);
        if (erros.Count == 0)
        {
            var (nome, codigo) = await _repositorio.EmUsoAsync(dados.Nome, dados.Codigo, dados.Id, ct);
            if (nome) erros.Add($"Já existe o papel \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");
            if (codigo) erros.Add($"O código \"{dados.Codigo}\" já é de outro papel.");
        }
        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        if (anterior is null)
            dados.RegistrarEvento($"Papel '{dados.Nome}' criado (código {dados.Codigo}).");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Papel '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleAsync(dados.Id, ct);
    }

    public Task<PapelCadastroDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, p => p.Desativar(), ct);

    public Task<PapelCadastroDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, p => p.Reativar(), ct);

    private async Task<PapelCadastroDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<Papel> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Papeis);
        var papel = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este papel não existe mais."]);
        papel.Versao = requisicao.Versao ?? papel.Versao;
        acao(papel);
        await _repositorio.SalvarAsync(papel, novo: false, ct);
        return await ReleAsync(id, ct);
    }

    private async Task<PapelCadastroDto> ReleAsync(Guid id, CancellationToken ct)
    {
        var gravado = await _repositorio.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        return await ParaDtoComUsoAsync(gravado, ct);
    }

    private async Task<PapelCadastroDto> ParaDtoComUsoAsync(Papel papel, CancellationToken ct) =>
        ParaDto(papel, (await _repositorio.ContarPessoasAsync(papel.Id, ct)).GetValueOrDefault(papel.Id));

    private static PapelCadastroDto ParaDto(Papel p, int quantidadePessoas) => new()
    {
        Id = p.Id,
        Versao = p.Versao,
        Codigo = p.Codigo,
        Nome = p.Nome,
        Descricao = p.Descricao,
        Ordem = p.Ordem,
        Ativo = p.Ativo,
        PapelSistema = p.PapelSistema,
        Obrigatorio = p.PapelSistema is { } sistema && PapeisSistema.TemRegra(sistema),
        QuantidadePessoas = quantidadePessoas
    };
}
