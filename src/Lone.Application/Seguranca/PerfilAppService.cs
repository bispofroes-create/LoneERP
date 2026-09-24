using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Seguranca;

public interface IPerfilAppService
{
    Task<List<PerfilResumo>> ListarAsync(CancellationToken ct = default);
    Task<PerfilDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<PerfilDto> SalvarAsync(PerfilDto dados, CancellationToken ct = default);

    /// <summary>Catálogo de permissões, para montar a tela de perfis.</summary>
    IReadOnlyList<DefinicaoPermissao> ListarPermissoes();
}

public sealed class PerfilAppService : IPerfilAppService
{
    private readonly IPerfilRepositorio _perfis;
    private readonly IAutorizacao _autorizacao;

    public PerfilAppService(IPerfilRepositorio perfis, IAutorizacao autorizacao)
    {
        _perfis = perfis;
        _autorizacao = autorizacao;
    }

    public Task<List<PerfilResumo>> ListarAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarPerfis);
        return _perfis.ListarAsync(ct);
    }

    public async Task<PerfilDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarPerfis);
        var perfil = await _perfis.ObterAsync(id, ct);
        return perfil is null ? null : ParaDto(perfil);
    }

    public IReadOnlyList<DefinicaoPermissao> ListarPermissoes()
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarPerfis);
        return Permissoes.Todas;
    }

    public async Task<PerfilDto> SalvarAsync(PerfilDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarPerfis);

        var anterior = dto.Id == Guid.Empty ? null : await _perfis.ObterAsync(dto.Id, ct);
        var novo = anterior is null;
        var id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id;

        // Só códigos do catálogo; administrador não precisa de lista (tem tudo).
        var validos = Permissoes.Todas.Select(p => p.Codigo).ToHashSet();
        var dados = new Perfil
        {
            Id = id,
            Versao = dto.Versao,
            Nome = (dto.Nome ?? string.Empty).Trim(),
            Descricao = string.IsNullOrWhiteSpace(dto.Descricao) ? null : dto.Descricao.Trim(),
            Administrador = dto.Administrador,
            Ativo = dto.Ativo,
            Permissoes = dto.Administrador
                ? new List<PerfilPermissao>()
                : dto.Permissoes.Where(validos.Contains).Distinct()
                    .Select(codigo => new PerfilPermissao { PerfilId = id, Codigo = codigo }).ToList()
        };

        var erros = new List<string>();
        if (dados.Nome.Length == 0) erros.Add("Informe o nome do perfil.");
        else if (await _perfis.NomeEmUsoAsync(dados.Nome, id, ct)) erros.Add("Já existe um perfil com este nome.");
        if (!dados.Administrador && dados.Permissoes.Count == 0) erros.Add("Escolha ao menos uma permissão.");

        if (anterior is { Administrador: true, Ativo: true } && !(dados.Administrador && dados.Ativo))
        {
            var outros = (await _perfis.IdsAdministradoresAtivosAsync(ct)).Where(outro => outro != anterior.Id);
            if (!outros.Any())
                erros.Add("Este é o único perfil administrador ativo; o sistema não pode ficar sem ele.");
        }

        if (erros.Count > 0) throw new ValidacaoException(erros);

        await _perfis.SalvarAsync(dados, novo, ct);
        var salvo = await _perfis.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        return ParaDto(salvo);
    }

    private static PerfilDto ParaDto(Perfil p) => new()
    {
        Id = p.Id,
        Versao = p.Versao,
        Nome = p.Nome,
        Descricao = p.Descricao,
        Administrador = p.Administrador,
        Ativo = p.Ativo,
        Permissoes = p.Permissoes.Select(x => x.Codigo).Order().ToList()
    };
}
