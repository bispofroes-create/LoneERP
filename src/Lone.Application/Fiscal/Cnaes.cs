using Lone.Application.Seguranca;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;

namespace Lone.Application.Fiscal;

/// <summary>Subclasse da lista oficial do IBGE (código de 7 dígitos e descrição).</summary>
public sealed record CnaeOficial(int Codigo, string Descricao);

/// <summary>Lista oficial de subclasses CNAE (IBGE).</summary>
public interface ICnaesOficiais
{
    Task<IReadOnlyList<CnaeOficial>> ListarAsync(CancellationToken ct);
}

/// <summary>Tabela de CNAEs. Nunca apaga: o que sai da lista oficial é desativado.</summary>
public interface ICnaeRepositorio
{
    Task<SituacaoCnaes> ObterSituacaoAsync(CancellationToken ct);
    Task<Dictionary<int, Cnae>> ObterVariosAsync(IReadOnlyCollection<int> codigos, CancellationToken ct);

    /// <summary>Por código (começo) ou por palavra da descrição.</summary>
    Task<List<Cnae>> BuscarAsync(string texto, int limite, CancellationToken ct);

    Task<(int Incluidos, int Alterados, int Desativados)> SincronizarAsync(IReadOnlyList<Cnae> oficiais, DateTime agoraUtc, CancellationToken ct);
}

/// <summary>Carga da tabela pelo IBGE (sem permissão: usado pela inicialização da API e pelo CnaeAppService).</summary>
public sealed class ServicoCnaes
{
    /// <summary>A CNAE 2.3 tem cerca de 1.300 subclasses: menos que isto é resposta incompleta.</summary>
    public const int MinimoSubclasses = 1000;

    private readonly ICnaeRepositorio _repositorio;
    private readonly ICnaesOficiais _oficiais;
    private readonly TimeProvider _relogio;

    public ServicoCnaes(ICnaeRepositorio repositorio, ICnaesOficiais oficiais, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _oficiais = oficiais;
        _relogio = relogio;
    }

    public async Task<bool> PrecisaCarregarAsync(CancellationToken ct) => (await _repositorio.ObterSituacaoAsync(ct)).Quantidade == 0;

    public async Task<ResultadoAtualizacaoCnaes> AtualizarAsync(CancellationToken ct)
    {
        var agora = _relogio.GetUtcNow().UtcDateTime;
        var oficiais = (await _oficiais.ListarAsync(ct))
            .Where(o => o.Codigo is > 0 and < 10_000_000 && !string.IsNullOrWhiteSpace(o.Descricao))
            .GroupBy(o => o.Codigo).Select(g => g.First())
            .Select(o => new Cnae { Id = o.Codigo, Descricao = Capitalizar(o.Descricao.Trim()), Ativo = true, AtualizadoEm = agora })
            .ToList();
        if (oficiais.Count < MinimoSubclasses)
            throw new Integracoes.ServicoExternoException($"A lista de CNAEs do IBGE veio incompleta ({oficiais.Count} subclasses). Nada foi alterado.");

        var (incluidos, alterados, desativados) = await _repositorio.SincronizarAsync(oficiais, agora, ct);
        return new ResultadoAtualizacaoCnaes { Incluidos = incluidos, Alterados = alterados, Desativados = desativados };
    }

    /// <summary>O IBGE manda em maiúsculas: "CULTIVO DE ARROZ" → "Cultivo de arroz".</summary>
    internal static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..].ToLower(TextoTela.Brasil);
}

internal static class TextoTela
{
    public static readonly System.Globalization.CultureInfo Brasil = new("pt-BR");
}

public interface ICnaeAppService
{
    Task<List<CnaeDto>> BuscarAsync(string? texto, CancellationToken ct = default);
    Task<SituacaoCnaes> ObterSituacaoAsync(CancellationToken ct = default);
    Task<ResultadoAtualizacaoCnaes> AtualizarAsync(CancellationToken ct = default);
}

public sealed class CnaeAppService : ICnaeAppService
{
    private readonly ICnaeRepositorio _repositorio;
    private readonly ServicoCnaes _servico;
    private readonly IAutorizacao _autorizacao;

    public CnaeAppService(ICnaeRepositorio repositorio, ServicoCnaes servico, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _servico = servico;
        _autorizacao = autorizacao;
    }

    public async Task<List<CnaeDto>> BuscarAsync(string? texto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 2) return [];
        return (await _repositorio.BuscarAsync(texto.Trim(), 50, ct))
            .Select(c => new CnaeDto(c.Id, Cnae.Formatar(c.Id), c.Descricao, c.Ativo)).ToList();
    }

    public Task<SituacaoCnaes> ObterSituacaoAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return _repositorio.ObterSituacaoAsync(ct);
    }

    public Task<ResultadoAtualizacaoCnaes> AtualizarAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.TabelasOficiais);
        return _servico.AtualizarAsync(ct);
    }
}
