using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comercial;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Comercial;

public interface ITipoAusenciaAppService
{
    Task<List<TipoAusenciaDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<TipoAusenciaDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<TipoAusenciaDto> SalvarAsync(TipoAusenciaDto dto, CancellationToken ct = default);
    Task<TipoAusenciaDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<TipoAusenciaDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Tipo de ausência (Motor Comercial, Fase 1c): permissão → normalização → regras → gravação.</summary>
public sealed class TipoAusenciaAppService : ITipoAusenciaAppService
{
    private readonly ITipoAusenciaRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public TipoAusenciaAppService(ITipoAusenciaRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<TipoAusenciaDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.Comercial);
        if (!gerencia && !_autorizacao.Possui(Permissoes.Comercial.Coberturas)) _autorizacao.Exigir(Permissoes.Comercial.Visualizar);
        var todos = await _repositorio.ListarAsync(ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return todos.Where(x => incluirInativos || x.Ativo).OrderBy(x => x.Ordem).ThenBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => ParaDto(x, usos.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<TipoAusenciaDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        return await ReleAsync(id, ct);
    }

    public async Task<TipoAusenciaDto> SalvarAsync(TipoAusenciaDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(x => x.Id == dto.Id);
        var dados = new TipoAusencia
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = Texto(dto.Nome),
            Ordem = dto.Ordem,
            Ativo = anterior?.Ativo ?? true
        };

        var erros = TiposAusenciaIniciais.Validar(dados, todos);
        if (erros.Count > 0) throw new ValidacaoException(erros);

        if (anterior is null) dados.RegistrarEvento($"Tipo de ausência '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Tipo de ausência '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<TipoAusenciaDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Desativar(), ct);

    public Task<TipoAusenciaDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Reativar(), ct);

    private async Task<TipoAusenciaDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<TipoAusencia> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        var item = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        item.Versao = requisicao.Versao ?? item.Versao;
        acao(item);
        await _repositorio.SalvarAsync(item, novo: false, ct);
        return await ReleAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<TipoAusenciaDto?> ReleAsync(Guid id, CancellationToken ct) =>
        await _repositorio.ObterAsync(id, ct) is { } item ? ParaDto(item, (await _repositorio.ContarUsosAsync(id, ct)).GetValueOrDefault(id)) : null;

    private static string Texto(string? s) =>
        string.IsNullOrWhiteSpace(s) ? string.Empty : string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static TipoAusenciaDto ParaDto(TipoAusencia x, int usos) => new()
    {
        Id = x.Id,
        Versao = x.Versao,
        Nome = x.Nome,
        Ordem = x.Ordem,
        Ativo = x.Ativo,
        QuantidadeUsos = usos
    };
}

public interface IParametrosComerciaisAppService
{
    Task<ParametrosComerciaisDto> ObterAsync(CancellationToken ct = default);
    Task<ParametrosComerciaisDto> SalvarAsync(ParametrosComerciaisDto dto, CancellationToken ct = default);
}

/// <summary>Parâmetros do módulo Comercial: todos que usam o módulo leem; só quem gerencia os cadastros comerciais altera.</summary>
public sealed class ParametrosComerciaisAppService : IParametrosComerciaisAppService
{
    private readonly IParametrosComerciaisRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public ParametrosComerciaisAppService(IParametrosComerciaisRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<ParametrosComerciaisDto> ObterAsync(CancellationToken ct = default)
    {
        if (!_autorizacao.Possui(Permissoes.Cadastros.Comercial) && !_autorizacao.Possui(Permissoes.Comercial.Visualizar) &&
            !_autorizacao.Possui(Permissoes.Comercial.Coberturas))
            _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return ParaDto(await _repositorio.ObterAsync(ct));
    }

    public async Task<ParametrosComerciaisDto> SalvarAsync(ParametrosComerciaisDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        var atual = await _repositorio.ObterAsync(ct);
        var dados = new ParametrosComerciais
        {
            Id = ParametrosComerciais.IdUnico,
            Versao = dto.Versao,
            DiasAvisoFimVinculo = dto.DiasAvisoFimVinculo,
            DiasRetroativosMaximo = dto.DiasRetroativosMaximo,
            CreditoNaAusencia = dto.CreditoNaAusencia,
            PercentualSubstitutoPadrao = dto.PercentualSubstitutoPadrao,
            CriadoEm = atual.CriadoEm
        };
        var erros = RegrasParametrosComerciais.Validar(dados);
        if (erros.Count > 0) throw new ValidacaoException(erros);
        await _repositorio.SalvarAsync(dados, ct);
        return ParaDto(await _repositorio.ObterAsync(ct));
    }

    public static ParametrosComerciaisDto ParaDto(ParametrosComerciais p) => new()
    {
        Versao = p.Versao,
        DiasAvisoFimVinculo = p.DiasAvisoFimVinculo,
        DiasRetroativosMaximo = p.DiasRetroativosMaximo,
        CreditoNaAusencia = p.CreditoNaAusencia,
        PercentualSubstitutoPadrao = p.PercentualSubstitutoPadrao
    };
}
