using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comercial;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Comercial;

public interface ITipoCarteiraAppService
{
    Task<List<TipoCarteiraDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<TipoCarteiraDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<TipoCarteiraDto> SalvarAsync(TipoCarteiraDto dto, CancellationToken ct = default);
    Task<TipoCarteiraDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<TipoCarteiraDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Tipo de carteira: permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class TipoCarteiraAppService : ITipoCarteiraAppService
{
    private readonly ITipoCarteiraRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public TipoCarteiraAppService(ITipoCarteiraRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<TipoCarteiraDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.Comercial);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var todos = await _repositorio.ListarAsync(ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return todos.Where(x => incluirInativos || x.Ativo).OrderBy(x => x.Ordem).ThenBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase).Select(x => ParaDto(x, usos.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<TipoCarteiraDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        return await ReleAsync(id, ct);
    }

    public async Task<TipoCarteiraDto> SalvarAsync(TipoCarteiraDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(x => x.Id == dto.Id);
        var dados = new TipoCarteira
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = Texto(dto.Nome),
            Principal = dto.Principal,
            Ordem = dto.Ordem,
            Ativo = anterior?.Ativo ?? true
        };

        var erros = new List<string>();
        if (dados.Nome.Length == 0) erros.Add("Informe o nome do tipo.");
        else if (dados.Nome.Length > TipoCarteira.TamanhoMaximoNome) erros.Add($"O nome pode ter no máximo {TipoCarteira.TamanhoMaximoNome} caracteres.");
        if (dados.Principal && todos.Any(t => t.Principal && t.Id != dados.Id))
            erros.Add($"Já existe o tipo principal \"{todos.First(t => t.Principal && t.Id != dados.Id).Nome}\": desmarque-o antes (só um tipo define o vendedor padrão).");
        if (dados.Nome.Length > 0 && todos.Any(t => t.Id != dados.Id && TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        if (anterior is null) dados.RegistrarEvento($"Tipo de carteira '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Tipo de carteira '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<TipoCarteiraDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Desativar(), ct);

    public Task<TipoCarteiraDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Reativar(), ct);

    private async Task<TipoCarteiraDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<TipoCarteira> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        var item = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        item.Versao = requisicao.Versao ?? item.Versao;
        acao(item);
        await _repositorio.SalvarAsync(item, novo: false, ct);
        return await ReleAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<TipoCarteiraDto?> ReleAsync(Guid id, CancellationToken ct) =>
        await _repositorio.ObterAsync(id, ct) is { } item ? ParaDto(item, (await _repositorio.ContarUsosAsync(id, ct)).GetValueOrDefault(id)) : null;

    private static string Texto(string? s) =>
        string.IsNullOrWhiteSpace(s) ? string.Empty : string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static TipoCarteiraDto ParaDto(TipoCarteira x, int usos) => new()
    {
        Id = x.Id,
        Versao = x.Versao,
        Nome = x.Nome,
        Principal = x.Principal,
        Ordem = x.Ordem,
        Ativo = x.Ativo,
        QuantidadeUsos = usos
    };
}
