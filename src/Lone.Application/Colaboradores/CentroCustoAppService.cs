using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Seguranca;
using Lone.Domain.Colaboradores;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Colaboradores;

public interface ICentroCustoAppService
{
    Task<List<CentroCustoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<CentroCustoDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<CentroCustoDto> SalvarAsync(CentroCustoDto dto, CancellationToken ct = default);
    Task<CentroCustoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<CentroCustoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Centro de custos (estrutura organizacional): permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class CentroCustoAppService : ICentroCustoAppService
{
    private readonly ICentroCustoRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public CentroCustoAppService(ICentroCustoRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<CentroCustoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.EstruturaOrganizacional);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);

        var todos = await _repositorio.ListarAsync(ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return await ParaDtosAsync(todos.Where(x => incluirInativos || x.Ativo).OrderBy(x => x.Codigo, StringComparer.Ordinal), todos, usos, ct);
    }

    public async Task<CentroCustoDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);
        return await ReleOuNuloAsync(id, ct);
    }

    public async Task<CentroCustoDto> SalvarAsync(CentroCustoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);

        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(x => x.Id == dto.Id);
        var dados = new CentroCusto
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Codigo = RegrasEstrutura.CodigoCentroCusto(dto.Codigo),
            Nome = RegrasEstrutura.Texto(dto.Nome),
            PaiId = dto.PaiId == Guid.Empty ? null : dto.PaiId,
            Analitico = dto.Analitico,
            Ativo = anterior?.Ativo ?? true
        };

        var erros = RegrasEstrutura.ValidarCentroCusto(dados, todos);
        if (dados.Analitico != (anterior?.Analitico ?? dados.Analitico) && !dados.Analitico && (await _repositorio.ContarUsosAsync(dados.Id, ct)).Count > 0)
            erros.Add("O centro de custo tem lotações em aberto: ele precisa continuar analítico.");
        if (todos.Where(t => t.Id != dados.Id).Any(t => t.Codigo == dados.Codigo))
            erros.Add($"Já existe o centro de custo de código {dados.Codigo} (ativo ou desativado).");
        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        if (anterior is null)
            dados.RegistrarEvento($"Centro de custo '{dados.Descricao}' criado.");
        else if (!string.Equals(anterior.Descricao, dados.Descricao, StringComparison.Ordinal))
            dados.RegistrarEvento($"Centro de custo '{anterior.Descricao}' renomeado para '{dados.Descricao}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleOuNuloAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<CentroCustoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Desativar(), ct);

    public Task<CentroCustoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Reativar(), ct);

    private async Task<CentroCustoDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<CentroCusto> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);
        var item = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        item.Versao = requisicao.Versao ?? item.Versao;
        acao(item);
        await _repositorio.SalvarAsync(item, novo: false, ct);
        return await ReleOuNuloAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<CentroCustoDto?> ReleOuNuloAsync(Guid id, CancellationToken ct)
    {
        var todos = await _repositorio.ListarAsync(ct);
        var item = todos.FirstOrDefault(x => x.Id == id);
        if (item is null) return null;
        return (await ParaDtosAsync([item], todos, await _repositorio.ContarUsosAsync(id, ct), ct))[0];
    }

    private static Task<List<CentroCustoDto>> ParaDtosAsync(IEnumerable<CentroCusto> itens, IReadOnlyCollection<CentroCusto> todos, IReadOnlyDictionary<Guid, int> usos, CancellationToken ct)
    {
        var porId = todos.ToDictionary(c => c.Id);
        int Nivel(CentroCusto c)
        {
            var nivel = 0;
            var visitados = new HashSet<Guid> { c.Id };
            for (var pai = c.PaiId; pai is { } id && porId.TryGetValue(id, out var p) && visitados.Add(id); pai = p.PaiId) nivel++;
            return nivel;
        }
        return Task.FromResult(itens.Select(x => new CentroCustoDto
        {
            Id = x.Id,
            Versao = x.Versao,
        Nome = x.Nome,
        Codigo = x.Codigo,
        PaiId = x.PaiId,
        Analitico = x.Analitico,
            Nivel = Nivel(x),
            Ativo = x.Ativo,
            QuantidadeUsos = usos.GetValueOrDefault(x.Id)
        }).ToList());
    }
}
