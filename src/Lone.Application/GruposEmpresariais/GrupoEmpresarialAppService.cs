using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.GruposEmpresariais;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.GruposEmpresariais;
using Lone.Domain.Validacao;

namespace Lone.Application.GruposEmpresariais;

public interface IGrupoEmpresarialAppService
{
    Task<List<GrupoEmpresarialDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<GrupoEmpresarialDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<List<EmpresaDoGrupoEmpresarialDto>> ListarEmpresasAsync(Guid id, CancellationToken ct = default);
    Task<GrupoEmpresarialDto> SalvarAsync(GrupoEmpresarialDto dto, CancellationToken ct = default);
    Task<GrupoEmpresarialDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<GrupoEmpresarialDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>
/// Cadastro de grupos empresariais: permissão → normalização → regras → gravação (com eventos no histórico).
/// Quem só consulta pessoas pode listar (para ver o grupo na ficha); criar e alterar exige o cadastro de grupos.
/// Participar de um grupo é decidido na ficha da pessoa jurídica (permissão de estrutura empresarial).
/// </summary>
public sealed class GrupoEmpresarialAppService : IGrupoEmpresarialAppService
{
    private readonly IGrupoEmpresarialRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;
    private readonly IMotivoDaOperacao _motivo;

    public GrupoEmpresarialAppService(IGrupoEmpresarialRepositorio repositorio, IAutorizacao autorizacao, IMotivoDaOperacao motivo)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
        _motivo = motivo;
    }

    public async Task<List<GrupoEmpresarialDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        if (!_autorizacao.Possui(Permissoes.Cadastros.GruposEmpresariais))
            _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);

        var todos = await _repositorio.ListarAsync(ct);
        var empresas = await _repositorio.ContarEmpresasAsync(null, ct);
        return todos.Where(g => incluirInativos || g.Ativo)
            .OrderBy(g => g.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => ParaDto(g, empresas.GetValueOrDefault(g.Id)))
            .ToList();
    }

    public async Task<GrupoEmpresarialDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        if (!_autorizacao.Possui(Permissoes.Cadastros.GruposEmpresariais))
            _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return await ReleOuNuloAsync(id, ct);
    }

    public async Task<List<EmpresaDoGrupoEmpresarialDto>> ListarEmpresasAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return await _repositorio.ListarEmpresasAsync(id, ct);
    }

    public async Task<GrupoEmpresarialDto> SalvarAsync(GrupoEmpresarialDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.GruposEmpresariais);

        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(g => g.Id == dto.Id);
        var dados = new GrupoEmpresarial
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = dto.Nome,
            Descricao = dto.Descricao,
            Ativo = anterior?.Ativo ?? true // desativar/reativar são ações próprias
        };
        RegrasGrupoEmpresarial.Normalizar(dados);

        var erros = RegrasGrupoEmpresarial.Validar(dados);
        if (todos.Where(g => g.Id != dados.Id).Any(g => TextoBusca.Normalizar(g.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe o grupo empresarial \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");
        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        if (anterior is null)
            dados.RegistrarEvento($"Grupo empresarial '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Grupo empresarial '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleOuNuloAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<GrupoEmpresarialDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, g => g.Desativar(), ct);

    public Task<GrupoEmpresarialDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, g => g.Reativar(), ct);

    /// <summary>Desativar não tira as empresas do grupo: só deixa de ser oferecido para empresas novas.</summary>
    private async Task<GrupoEmpresarialDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<GrupoEmpresarial> acao,
                                                                 CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.GruposEmpresariais);
        _motivo.Motivo = requisicao.Motivo;
        var item = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        item.Versao = requisicao.Versao ?? item.Versao;
        acao(item);
        await _repositorio.SalvarAsync(item, novo: false, ct);
        return await ReleOuNuloAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<GrupoEmpresarialDto?> ReleOuNuloAsync(Guid id, CancellationToken ct)
    {
        var item = await _repositorio.ObterAsync(id, ct);
        if (item is null) return null;
        var empresas = await _repositorio.ContarEmpresasAsync(id, ct);
        return ParaDto(item, empresas.GetValueOrDefault(id));
    }

    public static GrupoEmpresarialDto ParaDto(GrupoEmpresarial g, int empresas) => new()
    {
        Id = g.Id,
        Versao = g.Versao,
        Nome = g.Nome,
        Descricao = g.Descricao,
        Ativo = g.Ativo,
        QuantidadeEmpresas = empresas
    };
}
