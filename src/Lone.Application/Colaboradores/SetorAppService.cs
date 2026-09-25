using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Seguranca;
using Lone.Domain.Colaboradores;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Colaboradores;

public interface ISetorAppService
{
    Task<List<SetorDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<SetorDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<SetorDto> SalvarAsync(SetorDto dto, CancellationToken ct = default);
    Task<SetorDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<SetorDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Setors (estrutura organizacional): permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class SetorAppService : ISetorAppService
{
    private readonly ISetorRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;
    private readonly IDepartamentoRepositorio _departamentos;

    public SetorAppService(ISetorRepositorio repositorio, IAutorizacao autorizacao, IDepartamentoRepositorio departamentos)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
        _departamentos = departamentos;
    }

    public async Task<List<SetorDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.EstruturaOrganizacional);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);

        var todos = await _repositorio.ListarAsync(ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return await ParaDtosAsync(todos.Where(x => incluirInativos || x.Ativo).OrderBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase), todos, usos, ct);
    }

    public async Task<SetorDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);
        return await ReleOuNuloAsync(id, ct);
    }

    public async Task<SetorDto> SalvarAsync(SetorDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);

        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(x => x.Id == dto.Id);
        var dados = new Setor
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = RegrasEstrutura.Texto(dto.Nome),
            DepartamentoId = dto.DepartamentoId,
            Ativo = anterior?.Ativo ?? true
        };

        var erros = RegrasEstrutura.ValidarNome(dados.Nome, Setor.TamanhoMaximoNome, "setor");
        var departamento = dados.DepartamentoId == Guid.Empty ? null : await _departamentos.ObterAsync(dados.DepartamentoId, ct);
        if (departamento is null) erros.Add("Escolha o departamento do setor.");
        else if (!departamento.Ativo && anterior?.DepartamentoId != departamento.Id)
            erros.Add($"O departamento \"{departamento.Nome}\" está desativado.");
        if (anterior is not null && anterior.DepartamentoId != dados.DepartamentoId && (await _repositorio.ContarUsosAsync(anterior.Id, ct)).Count > 0)
            erros.Add("O setor tem lotações em aberto: ele não pode mudar de departamento. Crie outro setor no departamento novo.");
        if (todos.Where(t => t.Id != dados.Id).Any(t => t.DepartamentoId == dados.DepartamentoId && TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe o setor \"{dados.Nome}\" neste departamento (ativo ou desativado).");
        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        if (anterior is null)
            dados.RegistrarEvento($"Setor '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Setor '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleOuNuloAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<SetorDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Desativar(), ct);

    public Task<SetorDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Reativar(), ct);

    private async Task<SetorDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<Setor> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.EstruturaOrganizacional);
        var item = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        item.Versao = requisicao.Versao ?? item.Versao;
        acao(item);
        await _repositorio.SalvarAsync(item, novo: false, ct);
        return await ReleOuNuloAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<SetorDto?> ReleOuNuloAsync(Guid id, CancellationToken ct)
    {
        var todos = await _repositorio.ListarAsync(ct);
        var item = todos.FirstOrDefault(x => x.Id == id);
        if (item is null) return null;
        return (await ParaDtosAsync([item], todos, await _repositorio.ContarUsosAsync(id, ct), ct))[0];
    }

    private async Task<List<SetorDto>> ParaDtosAsync(IEnumerable<Setor> itens, IReadOnlyCollection<Setor> todos, IReadOnlyDictionary<Guid, int> usos, CancellationToken ct)
    {
        var departamentos = (await _departamentos.ListarAsync(ct)).ToDictionary(d => d.Id, d => d.Nome);
        return itens.Select(x => new SetorDto
        {
            Id = x.Id,
            Versao = x.Versao,
        Nome = x.Nome,
        DepartamentoId = x.DepartamentoId,
            Departamento = departamentos.GetValueOrDefault(x.DepartamentoId),
            Ativo = x.Ativo,
            QuantidadeUsos = usos.GetValueOrDefault(x.Id)
        }).ToList();
    }
}
