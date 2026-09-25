using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comercial;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Comercial;

public interface ICondicaoPagamentoAppService
{
    Task<List<CondicaoPagamentoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<CondicaoPagamentoDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<CondicaoPagamentoDto> SalvarAsync(CondicaoPagamentoDto dto, CancellationToken ct = default);
    Task<CondicaoPagamentoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<CondicaoPagamentoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Condição de pagamento: permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class CondicaoPagamentoAppService : ICondicaoPagamentoAppService
{
    private readonly ICondicaoPagamentoRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public CondicaoPagamentoAppService(ICondicaoPagamentoRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<CondicaoPagamentoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.Comercial);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var todos = await _repositorio.ListarAsync(ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return todos.Where(x => incluirInativos || x.Ativo).OrderBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase).Select(x => ParaDto(x, usos.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<CondicaoPagamentoDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        return await ReleAsync(id, ct);
    }

    public async Task<CondicaoPagamentoDto> SalvarAsync(CondicaoPagamentoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(x => x.Id == dto.Id);
        var dados = new CondicaoPagamento
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = Texto(dto.Nome),
            Parcelas = RegrasComercial.NormalizarParcelas(dto.Parcelas) ?? dto.Parcelas ?? string.Empty,
            AcrescimoPercentual = dto.AcrescimoPercentual,
            Ativo = anterior?.Ativo ?? true
        };

        var erros = RegrasComercial.Validar(dados, dto.Parcelas);
        if (dados.Nome.Length > 0 && todos.Any(t => t.Id != dados.Id && TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        if (anterior is null) dados.RegistrarEvento($"Condição de pagamento '{dados.Nome}' criada.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Condição de pagamento '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<CondicaoPagamentoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Desativar(), ct);

    public Task<CondicaoPagamentoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Reativar(), ct);

    private async Task<CondicaoPagamentoDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<CondicaoPagamento> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        var item = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        item.Versao = requisicao.Versao ?? item.Versao;
        acao(item);
        await _repositorio.SalvarAsync(item, novo: false, ct);
        return await ReleAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<CondicaoPagamentoDto?> ReleAsync(Guid id, CancellationToken ct) =>
        await _repositorio.ObterAsync(id, ct) is { } item ? ParaDto(item, (await _repositorio.ContarUsosAsync(id, ct)).GetValueOrDefault(id)) : null;

    private static string Texto(string? s) =>
        string.IsNullOrWhiteSpace(s) ? string.Empty : string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static CondicaoPagamentoDto ParaDto(CondicaoPagamento x, int usos) => new()
    {
        Id = x.Id,
        Versao = x.Versao,
        Nome = x.Nome,
        Parcelas = x.Parcelas,
        AcrescimoPercentual = x.AcrescimoPercentual,
        PrazoMedio = RegrasComercial.PrazoMedio(x.Parcelas),
        Ativo = x.Ativo,
        QuantidadeUsos = usos
    };
}
