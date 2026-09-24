using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Municipios;

/// <summary>
/// Liga textos antigos de município ("Curvelo", "CURVELO", "Curvelo - MG") à tabela do IBGE, sem adivinhar:
/// primeiro pelo código IBGE já gravado; depois por nome + UF; sem UF, só se o nome existir num único estado.
/// </summary>
public sealed class IndiceMunicipios
{
    private readonly Dictionary<int, Municipio> _porCodigo;
    private readonly Dictionary<(string Nome, string Uf), Municipio> _porNomeUf;
    private readonly ILookup<string, Municipio> _porNome;

    public IndiceMunicipios(IEnumerable<Municipio> municipios)
    {
        var lista = municipios.ToList();
        _porCodigo = lista.ToDictionary(m => m.Id);
        _porNomeUf = lista.GroupBy(m => (m.NomeBusca, m.Uf)).ToDictionary(g => g.Key, g => g.First());
        _porNome = lista.ToLookup(m => m.NomeBusca);
    }

    public int Quantidade => _porCodigo.Count;

    public ResultadoConciliacao Resolver(string? texto, string? uf, string? codigoIbge = null)
    {
        if (int.TryParse(codigoIbge, out var codigo) && _porCodigo.TryGetValue(codigo, out var peloCodigo))
            return new(peloCodigo, null);

        string? observacao = null;
        foreach (var leitura in InterpretacaoMunicipio.Ler(texto, uf))
        {
            if (leitura.Uf is { } sigla)
            {
                if (_porNomeUf.TryGetValue((leitura.NomeBusca, sigla), out var municipio))
                    return new(municipio, null);
                observacao ??= $"não existe município com este nome em {sigla}";
                continue;
            }

            var candidatos = _porNome[leitura.NomeBusca].ToList();
            if (candidatos.Count == 1)
                return new(candidatos[0], null);
            if (candidatos.Count > 1)
                observacao = $"existe em {candidatos.Count} estados ({string.Join(", ", candidatos.Select(c => c.Uf).Order())}): escolha a UF";
        }

        return new(null, observacao ?? (TextoBusca.Normalizar(texto).Length == 0
            ? "texto vazio"
            : "não encontrado na tabela do IBGE"));
    }
}
