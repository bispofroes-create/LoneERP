using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Seguranca;
using Lone.Domain.Colaboradores;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Colaboradores;

public interface IDepartamentoAppService
{
    Task<List<DepartamentoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<DepartamentoDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<DepartamentoDto> SalvarAsync(DepartamentoDto dto, CancellationToken ct = default);
    Task<DepartamentoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<DepartamentoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Departamentos (estrutura organizacional): permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class DepartamentoAppService : IDepartamentoAppService
{
    private readonly IDepartamentoRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public DepartamentoAppService(IDepartamentoRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<DepartamentoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.EstruturaOrganizacional);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);

        var todos = await _repositorio.ListarAsync(ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return await ParaDtosAsync(todos.Where(x => incluirInativos || x.Ativo).OrderBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase), todos, usos, ct);
    }

    public async Task<DepartamentoDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);
        return await ReleOuNuloAsync(id, ct);
    }

    public async Task<DepartamentoDto> SalvarAsync(DepartamentoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);

        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(x => x.Id == dto.Id);
        var dados = new Departamento
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = RegrasEstrutura.Texto(dto.Nome),
            Ativo = anterior?.Ativo ?? true
        };

        var erros = RegrasEstrutura.ValidarNome(dados.Nome, Departamento.TamanhoMaximoNome, "departamento");
        if (todos.Where(t => t.Id != dados.Id).Any(t => TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe o departamento \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");
        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        if (anterior is null)
            dados.RegistrarEvento($"Departamento '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Departamento '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleOuNuloAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<DepartamentoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Desativar(), ct);

    public Task<DepartamentoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Reativar(), ct);

    private async Task<DepartamentoDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<Departamento> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);
        var item = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        item.Versao = requisicao.Versao ?? item.Versao;
        acao(item);
        await _repositorio.SalvarAsync(item, novo: false, ct);
        return await ReleOuNuloAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<DepartamentoDto?> ReleOuNuloAsync(Guid id, CancellationToken ct)
    {
        var todos = await _repositorio.ListarAsync(ct);
        var item = todos.FirstOrDefault(x => x.Id == id);
        if (item is null) return null;
        return (await ParaDtosAsync([item], todos, await _repositorio.ContarUsosAsync(id, ct), ct))[0];
    }

    private static Task<List<DepartamentoDto>> ParaDtosAsync(IEnumerable<Departamento> itens, IReadOnlyCollection<Departamento> todos, IReadOnlyDictionary<Guid, int> usos, CancellationToken ct) =>
        Task.FromResult(itens.Select(x => new DepartamentoDto
        {
            Id = x.Id,
            Versao = x.Versao,
        Nome = x.Nome,
            Ativo = x.Ativo,
            QuantidadeUsos = usos.GetValueOrDefault(x.Id)
        }).ToList());
}
