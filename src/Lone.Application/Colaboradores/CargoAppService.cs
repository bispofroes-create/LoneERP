using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Seguranca;
using Lone.Domain.Colaboradores;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Colaboradores;

public interface ICargoAppService
{
    Task<List<CargoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<CargoDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<CargoDto> SalvarAsync(CargoDto dto, CancellationToken ct = default);
    Task<CargoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<CargoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Cargos (estrutura organizacional): permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class CargoAppService : ICargoAppService
{
    private readonly ICargoRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;
    private readonly IOcupacoesDoCargo _ocupacoes;

    public CargoAppService(ICargoRepositorio repositorio, IAutorizacao autorizacao, IOcupacoesDoCargo ocupacoes)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
        _ocupacoes = ocupacoes;
    }

    public async Task<List<CargoDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.EstruturaOrganizacional);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);

        var todos = await _repositorio.ListarAsync(ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return await ParaDtosAsync(todos.Where(x => incluirInativos || x.Ativo).OrderBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase), todos, usos, ct);
    }

    public async Task<CargoDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);
        return await ReleOuNuloAsync(id, ct);
    }

    public async Task<CargoDto> SalvarAsync(CargoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);

        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(x => x.Id == dto.Id);
        var dados = new Cargo
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = RegrasEstrutura.Texto(dto.Nome),
            OcupacaoCboId = dto.OcupacaoCboId,
            Ativo = anterior?.Ativo ?? true
        };

        var erros = RegrasEstrutura.ValidarNome(dados.Nome, Cargo.TamanhoMaximoNome, "cargo");
        if (dados.OcupacaoCboId is { } cbo && (await _ocupacoes.TitulosAsync([cbo], ct)).Count == 0)
            erros.Add("A ocupação CBO escolhida não está na tabela oficial.");
        if (todos.Where(t => t.Id != dados.Id).Any(t => TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe o cargo \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");
        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        if (anterior is null)
            dados.RegistrarEvento($"Cargo '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Cargo '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleOuNuloAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<CargoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Desativar(), ct);

    public Task<CargoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Reativar(), ct);

    private async Task<CargoDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<Cargo> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);
        var item = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        item.Versao = requisicao.Versao ?? item.Versao;
        acao(item);
        await _repositorio.SalvarAsync(item, novo: false, ct);
        return await ReleOuNuloAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<CargoDto?> ReleOuNuloAsync(Guid id, CancellationToken ct)
    {
        var todos = await _repositorio.ListarAsync(ct);
        var item = todos.FirstOrDefault(x => x.Id == id);
        if (item is null) return null;
        return (await ParaDtosAsync([item], todos, await _repositorio.ContarUsosAsync(id, ct), ct))[0];
    }

    private async Task<List<CargoDto>> ParaDtosAsync(IEnumerable<Cargo> itens, IReadOnlyCollection<Cargo> todos, IReadOnlyDictionary<Guid, int> usos, CancellationToken ct)
    {
        var lista = itens.ToList();
        var titulos = await _ocupacoes.TitulosAsync(lista.Select(x => x.OcupacaoCboId).OfType<int>().Distinct().ToList(), ct);
        return lista.Select(x => new CargoDto
        {
            Id = x.Id,
            Versao = x.Versao,
        Nome = x.Nome,
        OcupacaoCboId = x.OcupacaoCboId,
            Ocupacao = x.OcupacaoCboId is { } cbo ? $"{OcupacaoCbo.Formatar(cbo)} · {titulos.GetValueOrDefault(cbo, "(fora da tabela)")}" : null,
            Ativo = x.Ativo,
            QuantidadeUsos = usos.GetValueOrDefault(x.Id)
        }).ToList();
    }
}

/// <summary>Títulos das ocupações da CBO (para mostrar ao lado do cargo).</summary>
public interface IOcupacoesDoCargo
{
    Task<Dictionary<int, string>> TitulosAsync(IReadOnlyCollection<int> codigos, CancellationToken ct);
}
