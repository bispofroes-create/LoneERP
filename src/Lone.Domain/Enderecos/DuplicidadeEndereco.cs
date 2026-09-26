using System.Globalization;
using System.Text;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Enderecos;

/// <summary>Resultado da comparação de dois endereços físicos.</summary>
public enum SemelhancaEndereco
{
    /// <summary>Com certeza outro lugar.</summary>
    Diferente,
    /// <summary>Pode ser o mesmo (falta algum dado para ter certeza): o usuário confirma.</summary>
    Possivel,
    /// <summary>O mesmo endereço físico (depois de normalizar): não se cadastra de novo.</summary>
    Igual
}

/// <summary>
/// Compara endereços físicos (nunca a finalidade). Normaliza sem ser agressivo: maiúsculas, acentos, pontuação e
/// espaços não contam; CEP só dígitos; "S/N", "SN" e vazio são "sem número"; abreviações só no começo do logradouro
/// e só as inequívocas (R, AV, TV, PC/PCA, ROD). Número e complemento diferentes = endereços diferentes.
/// </summary>
public static class DuplicidadeEndereco
{
    /// <summary>
    /// Só abreviações de tipo de logradouro sem outro sentido comum no começo do nome. Ficam de fora, de propósito:
    /// "PR" e "AL" (também são siglas de UF usadas em rodovias estaduais, ex.: "PR 445", "AL 101") e "EST"
    /// (Estrada, Estância, Estação). Sem expandir, "Al. Santos" e "Alameda Santos" viram só "possível" ou
    /// "diferente" — nunca um falso "igual".
    /// </summary>
    private static readonly Dictionary<string, string> Abreviacoes = new()
    {
        ["R"] = "RUA", ["AV"] = "AVENIDA", ["AVN"] = "AVENIDA", ["TV"] = "TRAVESSA", ["TRAV"] = "TRAVESSA",
        ["PC"] = "PRACA", ["PCA"] = "PRACA", ["ROD"] = "RODOVIA"
    };

    /// <summary>Maiúsculas, sem acentos, só letras/dígitos separados por um espaço.</summary>
    public static string Texto(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sem = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(sem.Length);
        foreach (var c in sem)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string Logradouro(string? s)
    {
        var partes = Texto(s).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (partes.Count > 1 && Abreviacoes.TryGetValue(partes[0], out var completa)) partes[0] = completa;
        return string.Join(' ', partes);
    }

    public static string Numero(string? s)
    {
        var n = Texto((s ?? string.Empty).Replace("º", string.Empty).Replace("°", string.Empty)).Replace(" ", string.Empty);
        return n is "SN" or "SNO" or "SEMNUMERO" or "SNUMERO" ? string.Empty : n;
    }

    public static string Cep(string? s) => new((s ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    /// <summary>Compara dois endereços (ativos ou não; quem chama escolhe quais comparar).</summary>
    public static SemelhancaEndereco Comparar(PessoaEndereco a, PessoaEndereco b)
    {
        // Brasil × exterior, ou países diferentes: lugares diferentes.
        if (!string.Equals(Cep(a.CodigoPais), Cep(b.CodigoPais), StringComparison.Ordinal)) return SemelhancaEndereco.Diferente;

        if (Logradouro(a.Logradouro) != Logradouro(b.Logradouro) || Logradouro(a.Logradouro).Length == 0)
            return SemelhancaEndereco.Diferente;
        if (Numero(a.Numero) != Numero(b.Numero)) return SemelhancaEndereco.Diferente;
        if (Texto(a.Complemento) != Texto(b.Complemento)) return SemelhancaEndereco.Diferente;

        // Cidade: pelo código do IBGE quando os dois têm; senão pelo nome + UF.
        if (a.MunicipioId is { } ma && b.MunicipioId is { } mb)
        {
            if (ma != mb) return SemelhancaEndereco.Diferente;
        }
        else if (Texto(a.Cidade) != Texto(b.Cidade) || Texto(a.Uf) != Texto(b.Uf))
            return SemelhancaEndereco.Diferente;

        // CEP e bairro: diferentes = outro lugar; faltando de um lado = não dá para ter certeza.
        var cepA = Cep(a.Cep);
        var cepB = Cep(b.Cep);
        var bairroA = Texto(a.Bairro);
        var bairroB = Texto(b.Bairro);
        if (cepA.Length > 0 && cepB.Length > 0 && cepA != cepB) return SemelhancaEndereco.Diferente;
        if (bairroA.Length > 0 && bairroB.Length > 0 && bairroA != bairroB) return SemelhancaEndereco.Diferente;
        if (cepA.Length == 0 || cepB.Length == 0 || bairroA.Length == 0 || bairroB.Length == 0) return SemelhancaEndereco.Possivel;
        return SemelhancaEndereco.Igual;
    }

    /// <summary>Pares de endereços ativos iguais (Possível só entra com <paramref name="incluirPossiveis"/>).</summary>
    public static List<(PessoaEndereco A, PessoaEndereco B, SemelhancaEndereco Semelhanca)> Pares(
        IEnumerable<PessoaEndereco> enderecos, bool incluirPossiveis)
    {
        var ativos = enderecos.Where(e => e.Ativo).ToList();
        var pares = new List<(PessoaEndereco, PessoaEndereco, SemelhancaEndereco)>();
        for (var i = 0; i < ativos.Count; i++)
            for (var j = i + 1; j < ativos.Count; j++)
            {
                var s = Comparar(ativos[i], ativos[j]);
                if (s == SemelhancaEndereco.Igual || (incluirPossiveis && s == SemelhancaEndereco.Possivel))
                    pares.Add((ativos[i], ativos[j], s));
            }
        return pares;
    }

    /// <summary>
    /// Endereços repetidos que a gravação não aceita: um par igual em que pelo menos um lado é novo, foi reativado ou
    /// teve o local alterado. Pares antigos que já estavam gravados assim passam (são só apontados para consolidação).
    /// </summary>
    public static List<string> ErrosDeNovos(IReadOnlyList<PessoaEndereco> atuais, IReadOnlyList<PessoaEndereco> anteriores)
    {
        var antes = anteriores.ToDictionary(e => e.Id);
        bool Novo(PessoaEndereco e) =>
            !antes.TryGetValue(e.Id, out var a) || !a.Ativo || Chave(a) != Chave(e);
        // Endereço incluído agora ou com o local alterado agora (reativar um registro existente não é cadastrar outro).
        bool CadastradoAgora(PessoaEndereco e) => !antes.TryGetValue(e.Id, out var a) || Chave(a) != Chave(e);

        var erros = Pares(atuais, incluirPossiveis: false)
            .Where(p => Novo(p.A) || Novo(p.B))
            .Select(p => $"Este endereço já está cadastrado para esta pessoa: {Resumo(p.A)}. Use o endereço existente e acrescente a finalidade.")
            .ToList();

        // Igual a um endereço INATIVO (histórico): não se cria outra linha física; reativa-se o existente.
        // O consolidado em outro fica de fora (o igual ativo dele já é conferido acima).
        var inativos = atuais.Where(e => !e.Ativo && e.MescladoEmId is null).ToList();
        foreach (var novo in atuais.Where(e => e.Ativo && CadastradoAgora(e)))
            foreach (var inativo in inativos.Where(i => i.Id != novo.Id && Comparar(novo, i) == SemelhancaEndereco.Igual))
                erros.Add($"Já existe um endereço igual cadastrado para esta pessoa, porém ele está inativo: {Resumo(inativo)}. " +
                          "Reative o endereço existente em vez de cadastrar outro.");

        return erros.Distinct().ToList();
    }

    /// <summary>Todos os campos físicos normalizados (para saber se o local de um endereço gravado mudou).</summary>
    public static string Chave(PessoaEndereco e) => string.Join("|",
        Cep(e.CodigoPais), Logradouro(e.Logradouro), Numero(e.Numero), Texto(e.Complemento), Texto(e.Bairro),
        e.MunicipioId?.ToString() ?? Texto(e.Cidade), Texto(e.Uf), Cep(e.Cep));

    /// <summary>"Rua A, 100 - Centro - Curvelo/MG" (para mensagens).</summary>
    public static string Resumo(PessoaEndereco e)
    {
        var linha = string.Join(", ", new[] { e.Logradouro, e.Numero, e.Complemento }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var local = string.IsNullOrWhiteSpace(e.Uf) || e.Uf == "EX" ? e.Cidade : $"{e.Cidade}/{e.Uf}";
        return string.Join(" - ", new[] { linha, e.Bairro, local }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
