using Lone.Domain.Comum;

namespace Lone.Domain.Validacao;

/// <summary>Nome (já normalizado para busca) e UF lidos de um texto livre de município.</summary>
public sealed record MunicipioInformado(string NomeBusca, string? Uf);

/// <summary>
/// Lê textos antigos de município, como "Curvelo", "CURVELO", "Curvelo - MG", "Curvelo/MG" ou "Curvelo (MG)".
/// Devolve as leituras possíveis, da mais provável para a menos: quem usa confere cada uma na tabela do IBGE.
/// </summary>
public static class InterpretacaoMunicipio
{
    public static IReadOnlyList<MunicipioInformado> Ler(string? texto, string? ufInformada)
    {
        var uf = Ufs.Valida(ufInformada?.Trim().ToUpperInvariant()) ? ufInformada!.Trim().ToUpperInvariant() : null;
        var nome = TextoBusca.Normalizar(texto);
        if (nome.Length == 0) return [];

        var leituras = new List<MunicipioInformado>();

        // A UF pode vir grudada no fim do texto: "CURVELO MG" (depois da normalização, separadores viram espaço).
        var ultimoEspaco = nome.LastIndexOf(' ');
        if (ultimoEspaco > 0)
        {
            var final = nome[(ultimoEspaco + 1)..];
            if (Ufs.Valida(final) && (uf is null || uf == final))
                leituras.Add(new MunicipioInformado(nome[..ultimoEspaco], final));
        }

        leituras.Add(new MunicipioInformado(nome, uf));
        return leituras;
    }
}
