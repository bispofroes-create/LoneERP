using Lone.Core.Entidades;
using Lone.Core.Validacao;

namespace Lone.Aplicacao.Seguranca;

public interface IPerfilAppService
{
    Task<List<PerfilResumo>> ListarAsync(CancellationToken ct = default);
    Task<Perfil?> ObterAsync(int id, CancellationToken ct = default);
    Task<Perfil> SalvarAsync(Perfil dados, CancellationToken ct = default);
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

    public Task<Perfil?> ObterAsync(int id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarPerfis);
        return _perfis.ObterAsync(id, ct);
    }

    public async Task<Perfil> SalvarAsync(Perfil dados, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Seguranca.GerenciarPerfis);

        var anterior = dados.Id == 0 ? null : await _perfis.ObterAsync(dados.Id, ct)
                       ?? throw new ValidacaoException(["Este perfil não existe mais."]);

        dados.Nome = dados.Nome.Trim();
        dados.Descricao = string.IsNullOrWhiteSpace(dados.Descricao) ? null : dados.Descricao.Trim();

        // Só códigos do catálogo; administrador não precisa de lista (tem tudo).
        var validos = Permissoes.Todas.Select(p => p.Codigo).ToHashSet();
        dados.Permissoes = dados.Administrador
            ? new List<PerfilPermissao>()
            : dados.Permissoes.Where(p => validos.Contains(p.Codigo)).DistinctBy(p => p.Codigo).ToList();

        var erros = new List<string>();
        if (dados.Nome.Length == 0) erros.Add("Informe o nome do perfil.");
        else if (await _perfis.NomeEmUsoAsync(dados.Nome, dados.Id, ct)) erros.Add("Já existe um perfil com este nome.");
        if (!dados.Administrador && dados.Permissoes.Count == 0) erros.Add("Escolha ao menos uma permissão.");

        if (anterior is { Administrador: true, Ativo: true } && !(dados.Administrador && dados.Ativo))
        {
            var outros = (await _perfis.IdsAdministradoresAtivosAsync(ct)).Where(id => id != anterior.Id);
            if (!outros.Any())
                erros.Add("Este é o único perfil administrador ativo; o sistema não pode ficar sem ele.");
        }

        if (erros.Count > 0) throw new ValidacaoException(erros);
        return await _perfis.SalvarAsync(dados, ct);
    }
}
