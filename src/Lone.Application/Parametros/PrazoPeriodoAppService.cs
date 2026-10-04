using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Parametros;

/// <summary>Persistência: prazos de período. Nada é apagado: é desativado.</summary>
public interface IPrazoPeriodoRepositorio
{
    Task<List<PrazoPeriodo>> ListarAsync(CancellationToken ct);
    Task<PrazoPeriodo?> ObterAsync(Guid id, CancellationToken ct);
    Task SalvarAsync(PrazoPeriodo item, bool novo, CancellationToken ct);
}

public interface IPrazoPeriodoAppService
{
    Task<List<PrazoPeriodoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<PrazoPeriodoDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<PrazoPeriodoDto> SalvarAsync(PrazoPeriodoDto dto, CancellationToken ct = default);
    Task<PrazoPeriodoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<PrazoPeriodoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>
/// Prazos de período (03/10/2026): os prazos prontos do campo Prazo em todas as telas com período. Todo usuário logado lê
/// os ativos (o campo Prazo usa); ver os desativados, criar e alterar exige a permissão de parâmetros.
/// </summary>
public sealed class PrazoPeriodoAppService : IPrazoPeriodoAppService
{
    private readonly IPrazoPeriodoRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public PrazoPeriodoAppService(IPrazoPeriodoRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<PrazoPeriodoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        if (incluirInativos) _autorizacao.Exigir(Permissoes.Cadastros.Parametros);
        var todos = await _repositorio.ListarAsync(ct);
        return Ordenar(todos.Where(x => incluirInativos || x.Ativo)).Select(ParaDto).ToList();
    }

    public async Task<PrazoPeriodoDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Parametros);
        return await _repositorio.ObterAsync(id, ct) is { } item ? ParaDto(item) : null;
    }

    public async Task<PrazoPeriodoDto> SalvarAsync(PrazoPeriodoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Parametros);
        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(x => x.Id == dto.Id);
        var dados = new PrazoPeriodo
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Quantidade = dto.Quantidade,
            Unidade = dto.Unidade,
            Ativo = anterior?.Ativo ?? true
        };

        var erros = RegrasPrazoPeriodo.Validar(dados, todos);
        if (erros.Count > 0) throw new ValidacaoException(erros);

        if (anterior is null) dados.RegistrarEvento($"Prazo de período '{dados.Nome}' criado.");
        else if (anterior.Nome != dados.Nome) dados.RegistrarEvento($"Prazo de período '{anterior.Nome}' alterado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await _repositorio.ObterAsync(dados.Id, ct) is { } salvo ? ParaDto(salvo) : throw new ConflitoDeEdicaoException();
    }

    public Task<PrazoPeriodoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Desativar(), ct);

    public Task<PrazoPeriodoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Reativar(), ct);

    private async Task<PrazoPeriodoDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<PrazoPeriodo> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Parametros);
        var item = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        item.Versao = requisicao.Versao ?? item.Versao;
        acao(item);
        await _repositorio.SalvarAsync(item, novo: false, ct);
        return await _repositorio.ObterAsync(id, ct) is { } salvo ? ParaDto(salvo) : throw new ConflitoDeEdicaoException();
    }

    /// <summary>Do menor para o maior (30 dias antes de 2 meses); empate: dias, meses, anos.</summary>
    public static IEnumerable<PrazoPeriodo> Ordenar(IEnumerable<PrazoPeriodo> itens) =>
        itens.OrderBy(x => RegrasPrazoPeriodo.DiasAproximados(x.Quantidade, x.Unidade)).ThenBy(x => x.Unidade);

    public static PrazoPeriodoDto ParaDto(PrazoPeriodo x) => new()
    {
        Id = x.Id,
        Versao = x.Versao,
        Quantidade = x.Quantidade,
        Unidade = x.Unidade,
        Ativo = x.Ativo,
        Nome = x.Nome
    };
}
