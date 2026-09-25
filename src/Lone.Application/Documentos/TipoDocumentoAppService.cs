using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Documentos;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Documentos;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Documentos;

public interface ITipoDocumentoAppService
{
    Task<List<TipoDocumentoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<TipoDocumentoDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<TipoDocumentoDto> SalvarAsync(TipoDocumentoDto dto, CancellationToken ct = default);
    Task<TipoDocumentoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<TipoDocumentoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Tipos de documento: permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class TipoDocumentoAppService : ITipoDocumentoAppService
{
    private readonly ITipoDocumentoRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public TipoDocumentoAppService(ITipoDocumentoRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<TipoDocumentoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.Tipos);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);

        var tipos = await _repositorio.ListarAsync(incluirInativos, ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return tipos.Select(t => ParaDto(t, usos.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<TipoDocumentoDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Tipos);
        var tipo = await _repositorio.ObterAsync(id, ct);
        return tipo is null ? null : await ParaDtoComUsoAsync(tipo, ct);
    }

    public async Task<TipoDocumentoDto> SalvarAsync(TipoDocumentoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Tipos);

        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        var dados = new TipoDocumentoCadastro
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = dto.Nome,
            Ordem = dto.Ordem,
            Ativo = anterior?.Ativo ?? true,
            // O vínculo com o enum não muda pelo aplicativo: só os de sistema, criados pela migração, o têm.
            TipoSistema = anterior?.TipoSistema,
            ExigeValidade = dto.ExigeValidade,
            DiasAvisoVencimento = dto.DiasAvisoVencimento
        };
        if (anterior is null && dados.Ordem <= 0) dados.Ordem = await _repositorio.ProximaOrdemAsync(ct);

        RegrasDocumento.Normalizar(dados);
        var erros = RegrasDocumento.Validar(dados);
        if (dados.Nome.Length > 0 && await _repositorio.NomeEmUsoAsync(dados.Nome, dados.Id, ct))
            erros.Add($"Já existe o tipo de documento \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");
        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        if (anterior is null)
            dados.RegistrarEvento($"Tipo de documento '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Tipo de documento '{anterior.Nome}' renomeado para '{dados.Nome}'.");
        if (anterior is not null && anterior.ExigeValidade != dados.ExigeValidade)
            dados.RegistrarEvento(dados.ExigeValidade
                ? $"Tipo de documento '{dados.Nome}' passou a exigir validade."
                : $"Tipo de documento '{dados.Nome}' deixou de exigir validade.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleAsync(dados.Id, ct);
    }

    public Task<TipoDocumentoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, t => t.Desativar(), ct);

    public Task<TipoDocumentoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, t => t.Reativar(), ct);

    private async Task<TipoDocumentoDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<TipoDocumentoCadastro> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Tipos);
        var tipo = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este tipo não existe mais."]);
        tipo.Versao = requisicao.Versao ?? tipo.Versao;
        acao(tipo);
        await _repositorio.SalvarAsync(tipo, novo: false, ct);
        return await ReleAsync(id, ct);
    }

    private async Task<TipoDocumentoDto> ReleAsync(Guid id, CancellationToken ct)
    {
        var gravado = await _repositorio.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        return await ParaDtoComUsoAsync(gravado, ct);
    }

    private async Task<TipoDocumentoDto> ParaDtoComUsoAsync(TipoDocumentoCadastro tipo, CancellationToken ct) =>
        ParaDto(tipo, (await _repositorio.ContarUsosAsync(tipo.Id, ct)).GetValueOrDefault(tipo.Id));

    private static TipoDocumentoDto ParaDto(TipoDocumentoCadastro t, int usos) => new()
    {
        Id = t.Id,
        Versao = t.Versao,
        Nome = t.Nome,
        Ordem = t.Ordem,
        Ativo = t.Ativo,
        TipoSistema = t.TipoSistema,
        ExigeValidade = t.ExigeValidade,
        DiasAvisoVencimento = t.DiasAvisoVencimento,
        QuantidadeUsos = usos
    };
}
