using Lone.Domain.Entidades;

namespace Lone.Domain.Validacao;

/// <summary>
/// Liga a pessoa à tabela de municípios: confere se os Ids escolhidos existem e grava no endereço as cópias
/// (nome, UF e código IBGE) exatamente como estão no IBGE. O texto digitado pelo aparelho nunca vale.
/// </summary>
public static class ReferenciasMunicipio
{
    /// <summary>Ids de município usados pela pessoa (naturalidade e endereços no Brasil).</summary>
    public static IReadOnlySet<int> Ids(Pessoa p)
    {
        var ids = p.Enderecos.Where(e => e.EhBrasil && e.MunicipioId is not null).Select(e => e.MunicipioId!.Value).ToHashSet();
        if (p.NaturalidadeMunicipioId is { } naturalidade) ids.Add(naturalidade);
        return ids;
    }

    public static List<string> Aplicar(Pessoa p, IReadOnlyDictionary<int, Municipio> municipios)
    {
        var erros = new List<string>();

        if (p.NaturalidadeMunicipioId is { } naturalidade && !municipios.ContainsKey(naturalidade))
            erros.Add("Naturalidade: município não encontrado na tabela do IBGE. Escolha na lista.");

        for (var i = 0; i < p.Enderecos.Count; i++)
        {
            var e = p.Enderecos[i];
            if (!e.EhBrasil || e.MunicipioId is not { } id) continue;

            if (!municipios.TryGetValue(id, out var municipio))
            {
                erros.Add($"Endereço {i + 1}: município não encontrado na tabela do IBGE. Escolha na lista.");
                continue;
            }

            e.Cidade = municipio.Nome;
            e.Uf = municipio.Uf;
            e.CodigoMunicipioIbge = municipio.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return erros;
    }
}
