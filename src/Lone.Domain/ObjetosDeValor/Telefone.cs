using Lone.Domain.Validacao;

namespace Lone.Domain.ObjetosDeValor;

/// <summary>
/// Telefone. Brasileiro: DDD + número (10 dígitos fixo, 11 celular, ou 0800/0300/0500/0900 com 11).
/// Internacional: começa com "+" e código de país diferente de 55, de 8 a 15 dígitos.
/// </summary>
public sealed record Telefone
{
    private Telefone(string valor, bool internacional)
    {
        Valor = valor;
        Internacional = internacional;
    }

    /// <summary>Só dígitos (brasileiro sem o 55; internacional com o código do país).</summary>
    public string Valor { get; }
    public bool Internacional { get; }

    /// <summary>Forma gravada no banco: só dígitos, com "+" na frente se for internacional.</summary>
    public string Normalizado => Internacional ? "+" + Valor : Valor;

    public string Formatado => Internacional
        ? "+" + Valor
        : Valor.StartsWith('0')
            ? $"{Valor[..4]} {Valor[4..7]} {Valor[7..]}"
            : Valor.Length == 11
                ? $"({Valor[..2]}) {Valor[2..7]}-{Valor[7..]}"
                : $"({Valor[..2]}) {Valor[2..6]}-{Valor[6..]}";

    public static bool EhValido(string? texto) => TentarCriar(texto, out _);

    public static bool TentarCriar(string? texto, out Telefone? telefone)
    {
        telefone = null;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var digitos = Documento.SomenteDigitos(texto);
        var comMais = texto.TrimStart().StartsWith('+');

        if (comMais && !digitos.StartsWith("55"))
        {
            if (digitos.Length is >= 8 and <= 15)
                telefone = new Telefone(digitos, internacional: true);
            return telefone is not null;
        }

        // Remove o código do Brasil (55) e o zero de operadora/tronco à esquerda.
        if (digitos.StartsWith("55") && digitos.Length is 12 or 13)
            digitos = digitos[2..];
        else if (digitos.StartsWith('0') && digitos.Length is 11 or 12 && !EhNumeroEspecial(digitos))
            digitos = digitos[1..];

        if (EhNumeroEspecial(digitos) && digitos.Length == 11)
        {
            telefone = new Telefone(digitos, internacional: false);
            return true;
        }

        var dddValido = digitos.Length >= 2 && digitos[0] != '0' && digitos[1] != '0';
        var valido = dddValido && (digitos.Length == 10 || (digitos.Length == 11 && digitos[2] == '9'));
        if (valido)
            telefone = new Telefone(digitos, internacional: false);
        return valido;
    }

    private static bool EhNumeroEspecial(string digitos) =>
        digitos.StartsWith("0800") || digitos.StartsWith("0300") || digitos.StartsWith("0500") || digitos.StartsWith("0900");

    public override string ToString() => Formatado;
}
