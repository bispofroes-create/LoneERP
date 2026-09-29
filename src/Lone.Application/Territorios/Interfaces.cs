using Lone.Application.Seguranca;
using Lone.Contracts.Seguranca;
using Lone.Domain.Entidades;

namespace Lone.Application.Territorios;

/// <summary>Tipos de território (nunca apagados: desativados).</summary>
public interface ITipoTerritorioRepositorio
{
    /// <summary>Todos (ativos e desativados), sem rastreamento.</summary>
    Task<List<TipoTerritorio>> ListarAsync(CancellationToken ct);
    Task<TipoTerritorio?> ObterAsync(Guid id, CancellationToken ct);
    Task SalvarAsync(TipoTerritorio tipo, bool novo, CancellationToken ct);

    /// <summary>Quantos territórios (ativos e encerrados) usam cada tipo.</summary>
    Task<Dictionary<Guid, int>> ContarUsosAsync(CancellationToken ct);
}

/// <summary>Mapas territoriais com o universo (classificações nunca apagadas: desmarcar desativa).</summary>
public interface IMapaTerritorialRepositorio
{
    /// <summary>Todos (ativos e desativados), com o universo, sem rastreamento.</summary>
    Task<List<MapaTerritorial>> ListarAsync(CancellationToken ct);
    Task<MapaTerritorial?> ObterAsync(Guid id, CancellationToken ct);
    Task SalvarAsync(MapaTerritorial mapa, bool novo, CancellationToken ct);

    /// <summary>Territórios ativos por mapa.</summary>
    Task<Dictionary<Guid, int>> ContarTerritoriosAtivosAsync(CancellationToken ct);
}

/// <summary>Territórios com posições e responsáveis (nada é apagado).</summary>
public interface ITerritorioRepositorio
{
    /// <summary>Todos os territórios do mapa (ativos e encerrados), com posições e responsáveis, sem rastreamento.</summary>
    Task<List<Territorio>> ListarDoMapaAsync(Guid mapaId, CancellationToken ct);

    Task<Territorio?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Versão atual da trava da árvore do mapa (D1 = B). A tela recebe junto com a árvore e devolve em toda mudança de
    /// estrutura. Nula se o mapa não existe.
    /// </summary>
    Task<byte[]?> ObterVersaoArvoreAsync(Guid mapaId, CancellationToken ct);

    /// <summary>
    /// Inclui ou altera o território, as posições e os responsáveis (nenhum é apagado). Com <paramref name="versaoArvoreVista"/>
    /// (mudança de estrutura: criar, mover, encerrar, reativar, mudar o início), a mesma transação troca a versão da árvore
    /// exigindo que ela ainda seja a que a tela mostrava: se a árvore mudou no meio, nada é gravado e sai conflito de edição
    /// (é o que impede A→B e B→A simultâneos de formarem um ciclo e avisa quem decidiu olhando uma árvore velha).
    /// </summary>
    Task SalvarAsync(Territorio territorio, bool novo, byte[]? versaoArvoreVista, CancellationToken ct);
}

/// <summary>
/// Uso operacional dos territórios (decisões T14/T18): território com regra publicada ou com atribuição (vigente ou
/// histórica) só muda de estrutura por operação territorial, e o mapa em uso trava empresa, exclusividade, universo e
/// endereço de referência. As regras e as atribuições chegam com o motor (2b-1b); até lá nenhum território tem uso.
/// </summary>
public interface IUsoTerritorial
{
    /// <summary>Territórios do mapa com regra publicada ou atribuição.</summary>
    Task<IReadOnlySet<Guid>> TerritoriosComUsoAsync(Guid mapaId, CancellationToken ct);

    /// <summary>Mapas com algum território em uso.</summary>
    Task<IReadOnlySet<Guid>> MapasEmUsoAsync(CancellationToken ct);
}

/// <summary>Permissão de leitura dos territórios: quem configura também enxerga (não faz sentido configurar sem ver a árvore).</summary>
internal static class AcessoTerritorios
{
    public static void ExigirLeitura(IAutorizacao autorizacao)
    {
        if (!autorizacao.Possui(Permissoes.Territorios.Configurar)) autorizacao.Exigir(Permissoes.Territorios.Visualizar);
    }
}
