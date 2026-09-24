using Lone.Contracts.Municipios;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Municipios;

/// <summary>
/// Carga da tabela pelo IBGE e conciliação dos textos antigos. Sem verificação de permissão: é usado pela
/// inicialização da API e pelo MunicipioAppService (que confere a permissão antes).
/// </summary>
public sealed class ServicoMunicipios
{
    private readonly IMunicipioRepositorio _repositorio;
    private readonly IMunicipiosOficiais _oficiais;
    private readonly TimeProvider _relogio;

    public ServicoMunicipios(IMunicipioRepositorio repositorio, IMunicipiosOficiais oficiais, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _oficiais = oficiais;
        _relogio = relogio;
    }

    /// <summary>Tabela vazia (primeira vez)? Então precisa da carga do IBGE.</summary>
    public async Task<bool> PrecisaCarregarAsync(CancellationToken ct) =>
        (await _repositorio.ObterSituacaoAsync(ct)).Quantidade == 0;

    public async Task<ResultadoAtualizacaoMunicipios> AtualizarAsync(CancellationToken ct)
    {
        var agora = _relogio.GetUtcNow().UtcDateTime;
        var oficiais = await _oficiais.ListarAsync(ct);
        var municipios = Converter(oficiais, agora);

        // Proteção: resposta incompleta do IBGE desativaria municípios em uso. O Brasil tem mais de 5.500.
        if (municipios.Count < 5000)
            throw new Lone.Application.Integracoes.ServicoExternoException($"A lista do IBGE veio incompleta ({municipios.Count} municípios). Nada foi alterado.");

        var (incluidos, alterados, desativados) = await _repositorio.SincronizarAsync(municipios, agora, ct);
        var (resolvidas, abertas) = await ConciliarAsync(ct);
        return new ResultadoAtualizacaoMunicipios
        {
            Incluidos = incluidos,
            Alterados = alterados,
            Desativados = desativados,
            PendenciasResolvidas = resolvidas,
            PendenciasAbertas = abertas
        };
    }

    /// <summary>Tenta ligar os textos antigos ao município certo. Nada muda se a tabela estiver vazia.</summary>
    public async Task<(int Resolvidas, int Abertas)> ConciliarAsync(CancellationToken ct)
    {
        var todos = await _repositorio.ListarTodosAsync(ct);
        if (todos.Count == 0) return (0, (await _repositorio.ObterSituacaoAsync(ct)).PendenciasAbertas);

        var indice = new IndiceMunicipios(todos);
        return await _repositorio.ConciliarAsync(
            p => indice.Resolver(p.TextoOriginal, p.UfOriginal, p.CodigoIbgeOriginal),
            _relogio.GetUtcNow().UtcDateTime, ct);
    }

    /// <summary>Monta os municípios a partir da lista oficial, descartando códigos que não sejam de 7 dígitos.</summary>
    internal static List<Municipio> Converter(IEnumerable<MunicipioOficial> oficiais, DateTime agoraUtc) =>
        oficiais
            .Where(o => Ufs.DoMunicipio(o.Codigo) is not null && !string.IsNullOrWhiteSpace(o.Nome))
            .GroupBy(o => o.Codigo)
            .Select(g => g.First())
            .Select(o => new Municipio
            {
                Id = o.Codigo,
                Nome = o.Nome.Trim(),
                NomeBusca = TextoBusca.Normalizar(o.Nome),
                Uf = Ufs.DoMunicipio(o.Codigo)!,
                CodigoUf = (byte)(o.Codigo / 100000),
                Ativo = true,
                AtualizadoEm = agoraUtc
            })
            .ToList();
}
