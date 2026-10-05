using System.Globalization;

namespace Lone.Domain.Enderecos.ConferenciaCep;

/// <summary>Lado da rua que uma faixa de numeração cobre.</summary>
public enum LadoFaixa
{
    Ambos = 0,
    Par = 1,
    Impar = 2
}

/// <summary>Se um número de endereço está numa faixa.</summary>
public enum PertinenciaFaixa
{
    Dentro = 1,
    Fora = 2,
    /// <summary>Sem número legível ("S/N", "KM 23", vazio): não dá para dizer. Nunca elimina um CEP.</summary>
    Indeterminado = 3
}

/// <summary>
/// Faixa de numeração de um CEP (arquitetura §4.1): início e fim (cada um pode faltar = faixa aberta) e lado (par, ímpar ou
/// ambos). Lê os textos que acompanham o CEP ("até 999", "até 999/1000", "de 1000 ao fim", "de 801/802 ao fim",
/// "de 1 a 99 - lado ímpar", "lado par"). Só trabalha com o texto recebido: não consulta nada.
/// </summary>
public sealed record FaixaNumeracao
{
    private FaixaNumeracao(int? inicio, int? fim, LadoFaixa lado)
    {
        Inicio = inicio;
        Fim = fim;
        Lado = lado;
    }

    /// <summary>Primeiro número da faixa; nulo = desde o começo da rua.</summary>
    public int? Inicio { get; }

    /// <summary>Último número da faixa; nulo = até o fim da rua.</summary>
    public int? Fim { get; }

    public LadoFaixa Lado { get; }

    /// <summary>Sem restrição: todos os números, dos dois lados (CEP sem texto de faixa).</summary>
    public static FaixaNumeracao Todas { get; } = new(null, null, LadoFaixa.Ambos);

    /// <summary>Monta uma faixa. Início e fim, quando os dois existem, precisam estar em ordem; números a partir de 1.</summary>
    public static FaixaNumeracao Criar(int? inicio, int? fim, LadoFaixa lado = LadoFaixa.Ambos)
    {
        if (inicio is < 1) throw new ArgumentOutOfRangeException(nameof(inicio), "O início da faixa precisa ser pelo menos 1.");
        if (fim is < 1) throw new ArgumentOutOfRangeException(nameof(fim), "O fim da faixa precisa ser pelo menos 1.");
        if (inicio is { } i && fim is { } f && i > f)
            throw new ArgumentException("O início da faixa não pode ser maior que o fim.", nameof(inicio));
        if (!Enum.IsDefined(lado)) throw new ArgumentOutOfRangeException(nameof(lado));
        return new FaixaNumeracao(inicio, fim, lado);
    }

    /// <summary>
    /// Lê o texto da faixa. Vazio = <see cref="Todas"/>. Texto fora dos formatos conhecidos = nulo (não interpretada; quem
    /// usa trata como indeterminado e não elimina o CEP). Um par "999/1000" é o último ímpar e o último par da faixa: vale
    /// como o maior (no fim) ou o menor (no começo); só se aceita par de números vizinhos.
    /// </summary>
    public static FaixaNumeracao? Interpretar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return Todas;

        // "/" vira uma palavra própria, porque a normalização comum troca pontuação por espaço.
        var tokens = DuplicidadeEndereco.Texto(texto.Replace("/", " BARRA ", StringComparison.Ordinal))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (tokens.Count == 0) return null;

        var lado = LadoFaixa.Ambos;
        var posLado = tokens.IndexOf("LADO");
        if (posLado >= 0)
        {
            // "LADO PAR" / "LADO IMPAR" só no fim do texto.
            if (posLado != tokens.Count - 2) return null;
            if (tokens[posLado + 1] == "PAR") lado = LadoFaixa.Par;
            else if (tokens[posLado + 1] == "IMPAR") lado = LadoFaixa.Impar;
            else return null;
            tokens.RemoveRange(posLado, 2);
        }

        var pos = 0;
        int? inicio = null, fim = null;
        if (tokens.Count == 0)
        {
            // Só o lado ("lado par"): a rua toda daquele lado.
        }
        else if (tokens[0] == "ATE")
        {
            // ATE N
            pos = 1;
            if (!LerNumero(tokens, ref pos, usarMaior: true, out var n)) return null;
            fim = n;
        }
        else if (tokens[0] == "DE")
        {
            // DE N AO FIM | DE N A N
            pos = 1;
            if (!LerNumero(tokens, ref pos, usarMaior: false, out var n)) return null;
            inicio = n;
            if (pos + 1 < tokens.Count && tokens[pos] == "AO" && tokens[pos + 1] == "FIM")
                pos += 2;
            else if (pos < tokens.Count && tokens[pos] == "A")
            {
                pos++;
                if (!LerNumero(tokens, ref pos, usarMaior: true, out var f)) return null;
                fim = f;
            }
            else return null;
        }
        else return null;

        if (pos != tokens.Count) return null;
        if (inicio is { } i && fim is { } ff && i > ff) return null;
        return new FaixaNumeracao(inicio, fim, lado);
    }

    private static bool LerNumero(List<string> tokens, ref int pos, bool usarMaior, out int numero)
    {
        numero = 0;
        if (pos >= tokens.Count || !Inteiro(tokens[pos], out var a)) return false;
        pos++;
        if (pos < tokens.Count && tokens[pos] == "BARRA")
        {
            if (pos + 1 >= tokens.Count || !Inteiro(tokens[pos + 1], out var b) || Math.Abs(a - b) != 1) return false;
            pos += 2;
            numero = usarMaior ? Math.Max(a, b) : Math.Min(a, b);
            return true;
        }
        numero = a;
        return true;
    }

    private static bool Inteiro(string token, out int valor)
    {
        if (token.All(char.IsAsciiDigit) && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out valor) && valor >= 1)
            return true;
        valor = 0;
        return false;
    }

    /// <summary>
    /// O número do endereço está na faixa? Usa a parte numérica do começo ("100A" = 100). "S/N", vazio, zero ou texto que não
    /// começa por algarismo ("KM 23", "Lote 5") = <see cref="PertinenciaFaixa.Indeterminado"/>.
    /// </summary>
    public PertinenciaFaixa Contem(string? numero)
    {
        var n = ParteNumerica(numero);
        return n is null ? PertinenciaFaixa.Indeterminado : Contem(n.Value);
    }

    /// <summary>O número está na faixa (limites incluídos; lado conferido pela paridade)?</summary>
    public PertinenciaFaixa Contem(int numero)
    {
        if (numero < 1) return PertinenciaFaixa.Indeterminado;
        if (Lado == LadoFaixa.Par && numero % 2 != 0) return PertinenciaFaixa.Fora;
        if (Lado == LadoFaixa.Impar && numero % 2 == 0) return PertinenciaFaixa.Fora;
        if (Inicio is { } i && numero < i) return PertinenciaFaixa.Fora;
        if (Fim is { } f && numero > f) return PertinenciaFaixa.Fora;
        return PertinenciaFaixa.Dentro;
    }

    /// <summary>Os algarismos do começo do número ("100A" → 100); nulo quando não há ("S/N", "KM 23", vazio, "0").</summary>
    public static int? ParteNumerica(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero) || RegrasEndereco.EhSemNumero(numero)) return null;
        var digitos = new string(numero.Trim().TakeWhile(char.IsAsciiDigit).ToArray());
        return digitos.Length > 0 && int.TryParse(digitos, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1
            ? n
            : null;
    }
}
