using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Seguranca;
using Lone.Contracts.Territorios;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Territorios;
using Lone.Domain.Validacao;

namespace Lone.Application.Territorios;

public interface ITipoTerritorioAppService
{
    Task<List<TipoTerritorioDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<TipoTerritorioDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<TipoTerritorioDto> SalvarAsync(TipoTerritorioDto dto, CancellationToken ct = default);
    Task<TipoTerritorioDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<TipoTerritorioDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Tipos de território (Fase 2b-1a): permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class TipoTerritorioAppService : ITipoTerritorioAppService
{
    private readonly ITipoTerritorioRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public TipoTerritorioAppService(ITipoTerritorioRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<TipoTerritorioDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeitura(_autorizacao);
        var usos = await _repositorio.ContarUsosAsync(ct);
        return (await _repositorio.ListarAsync(ct)).Where(t => incluirInativos || t.Ativo)
            .OrderBy(t => t.Ordem).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => ParaDto(t, usos.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<TipoTerritorioDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeitura(_autorizacao);
        return await _repositorio.ObterAsync(id, ct) is { } t ? ParaDto(t, (await _repositorio.ContarUsosAsync(ct)).GetValueOrDefault(id)) : null;
    }

    public async Task<TipoTerritorioDto> SalvarAsync(TipoTerritorioDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Territorios.Configurar);
        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(t => t.Id == dto.Id);
        // Código vazio num tipo gravado = "não mexi" (a tela não deixa editar); diferente do gravado, o domínio recusa.
        var codigo = RegrasCadastroTerritorial.NormalizarCodigo(dto.Codigo);
        var dados = new TipoTerritorio
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Codigo = codigo.Length == 0 && anterior is not null ? anterior.Codigo : codigo,
            Nome = RegrasCadastroTerritorial.Texto(dto.Nome),
            Descricao = RegrasCadastroTerritorial.TextoOpcional(dto.Descricao),
            // Ordem vazia (0) = no fim, como nos outros tipos do sistema; num tipo gravado, mantém a que tinha.
            Ordem = dto.Ordem > 0 ? dto.Ordem : anterior?.Ordem ?? (todos.Count == 0 ? 1 : todos.Max(t => t.Ordem) + 1),
            Ativo = anterior?.Ativo ?? true
        };

        var erros = RegrasCadastroTerritorial.ValidarTipo(dados, todos, anterior);
        if (erros.Count > 0) throw new ValidacaoException(erros);

        if (anterior is null) dados.RegistrarEvento($"Tipo de território '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Tipo de território '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ObterAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<TipoTerritorioDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarAsync(id, requisicao, t => t.Desativar(), ct);

    public Task<TipoTerritorioDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarAsync(id, requisicao, t => t.Reativar(), ct);

    /// <summary>Desativar não mexe nos territórios que já usam o tipo: só deixa de ser oferecido para escolhas novas.</summary>
    private async Task<TipoTerritorioDto> AlterarAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<TipoTerritorio> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Territorios.Configurar);
        var tipo = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este tipo de território não existe mais."]);
        tipo.Versao = requisicao.Versao ?? tipo.Versao;
        acao(tipo);
        await _repositorio.SalvarAsync(tipo, novo: false, ct);
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public static TipoTerritorioDto ParaDto(TipoTerritorio t, int usos) => new()
    {
        Id = t.Id, Versao = t.Versao, Codigo = t.Codigo, Nome = t.Nome, Descricao = t.Descricao, Ordem = t.Ordem, Ativo = t.Ativo,
        DoSistema = TiposTerritorioIniciais.EhDoSistema(t.Id), QuantidadeUsos = usos
    };
}
