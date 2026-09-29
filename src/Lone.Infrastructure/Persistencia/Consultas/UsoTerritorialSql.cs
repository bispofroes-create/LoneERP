using Lone.Application.Territorios;

namespace Lone.Infrastructure.Persistencia.Consultas;

/// <summary>
/// Uso operacional dos territórios (decisões T14/T18). Uso = regra do território publicada ou atribuição de cliente
/// (vigente ou histórica). As tabelas das regras e das atribuições nascem com o motor (Fase 2b-1b, migration própria):
/// até lá nenhum território nem mapa tem uso, e esta é a resposta verdadeira, não um atalho. Na 2b-1b esta classe passa a
/// consultar <c>RegrasTerritorio</c> e <c>AtribuicoesTerritorio</c>; nenhuma regra, serviço ou tela precisa mudar.
/// </summary>
public sealed class UsoTerritorialSql : IUsoTerritorial
{
    private static readonly IReadOnlySet<Guid> Nenhum = new HashSet<Guid>();

    public Task<IReadOnlySet<Guid>> TerritoriosComUsoAsync(Guid mapaId, CancellationToken ct) => Task.FromResult(Nenhum);

    public Task<IReadOnlySet<Guid>> MapasEmUsoAsync(CancellationToken ct) => Task.FromResult(Nenhum);
}
