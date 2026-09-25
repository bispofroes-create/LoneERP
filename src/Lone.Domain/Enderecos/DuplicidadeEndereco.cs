using System.Globalization;
using System.Text;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Enderecos;

/// <summary>
/// As finalidades iniciais (de sistema): nascem com a base, com Ids estáveis (a migração dos dados antigos depende
/// deles). O resto do sistema usa o Id vindo do cadastro; regra que precisa de uma finalidade específica usa o Código.
/// O bit legado liga cada uma à coluna antiga PessoaEnderecos.Finalidades (só compatibilidade e migração).
/// </summary>
public static class FinalidadesEnderecoIniciais
{
    public const string Comercial = "COMERCIAL";
    public const string Residencial = "RESIDENCIAL";
    public const string Fiscal = "FISCAL";
    public const string Entrega = "ENTREGA";
    public const string Cobranca = "COBRANCA";
    public const string Correspondencia = "CORRESPONDENCIA";

    public static IReadOnlyList<(Guid Id, string Codigo, string Nome, int Ordem, FinalidadeEndereco BitLegado)> Todas { get; } =
    [
        (new Guid("7a9e1c07-0000-0000-0000-000000000001"), Comercial, "Comercial", 1, FinalidadeEndereco.Comercial),
        (new Guid("7a9e1c07-0000-0000-0000-000000000002"), Residencial, "Residencial", 2, FinalidadeEndereco.Residencial),
        (new Guid("7a9e1c07-0000-0000-0000-000000000003"), Fiscal, "Fiscal", 3, FinalidadeEndereco.Fiscal),
        (new Guid("7a9e1c07-0000-0000-0000-000000000004"), Entrega, "Entrega", 4, FinalidadeEndereco.Entrega),
        (new Guid("7a9e1c07-0000-0000-0000-000000000005"), Cobranca, "Cobrança", 5, FinalidadeEndereco.Cobranca),
        (new Guid("7a9e1c07-0000-0000-0000-000000000006"), Correspondencia, "Correspondência", 6, FinalidadeEndereco.Correspondencia)
    ];

    /// <summary>Id estável de uma finalidade de sistema pelo código.</summary>
    public static Guid Id(string codigo) => Todas.First(t => t.Codigo == codigo).Id;

    /// <summary>Bit da coluna legada para a finalidade (Nenhuma para as criadas pelo usuário).</summary>
    public static FinalidadeEndereco BitLegado(Guid finalidadeId) =>
        Todas.Where(t => t.Id == finalidadeId).Select(t => t.BitLegado).FirstOrDefault();
}

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
/// e só as inequívocas (R, AV, AL, TV, PC/PCA, ROD, EST). Número e complemento diferentes = endereços diferentes.
/// </summary>
public static class DuplicidadeEndereco
{
    private static readonly Dictionary<string, string> Abreviacoes = new()
    {
        ["R"] = "RUA", ["AV"] = "AVENIDA", ["AVN"] = "AVENIDA", ["AL"] = "ALAMEDA", ["TV"] = "TRAVESSA", ["TRAV"] = "TRAVESSA",
        ["PC"] = "PRACA", ["PCA"] = "PRACA", ["PR"] = "PRACA", ["ROD"] = "RODOVIA", ["EST"] = "ESTRADA"
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

        return Pares(atuais, incluirPossiveis: false)
            .Where(p => Novo(p.A) || Novo(p.B))
            .Select(p => $"Este endereço já está cadastrado para esta pessoa: {Resumo(p.A)}. Use o endereço existente e acrescente a finalidade.")
            .Distinct()
            .ToList();
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
