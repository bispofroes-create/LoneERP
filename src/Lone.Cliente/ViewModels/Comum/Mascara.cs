using System.Text;

namespace Lone.Cliente.ViewModels.Comum;

public enum TipoMascara
{
    Nenhuma,
    Data,
    Cpf,
    Cnpj,
    Cep,
    Telefone,
    Hora,
    DataHora,

    /// <summary>Sem formatação; só pede o teclado numérico.</summary>
    Numero,

    /// <summary>Sem formatação; só pede o teclado de e-mail.</summary>
    Email
}

/// <summary>
/// Formata enquanto o usuário digita (12345678 → 12/34/5678). O separador só entra quando já existe o
/// caractere seguinte, então apagar com Backspace funciona normalmente.
/// </summary>
public static class Mascara
{
    public static string Aplicar(TipoMascara tipo, string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        return tipo switch
        {
            TipoMascara.Data => Montar(Digitos(texto, 8), "##/##/####"),
            TipoMascara.Cpf => Montar(Digitos(texto, 11), "###.###.###-##"),
            TipoMascara.Cep => Montar(Digitos(texto, 8), "#####-###"),
            TipoMascara.Cnpj => Montar(Cnpj(texto), "##.###.###/####-##"),
            TipoMascara.Telefone => Telefone(texto),
            TipoMascara.Hora => Montar(Digitos(texto, 4), "##:##"),
            TipoMascara.DataHora => Montar(Digitos(texto, 12), "##/##/#### ##:##"),
            _ => texto
        };
    }

    /// <summary>
    /// Telefone brasileiro: (11) 3333-4444 ou (11) 98888-7777. Fica como digitado quando não é esse formato:
    /// exterior (+), 0800/0300, número com 0 de operadora ou com mais de 11 dígitos.
    /// </summary>
    private static string Telefone(string texto)
    {
        if (texto.TrimStart().StartsWith('+')) return texto;
        var todos = Digitos(texto, int.MaxValue);
        if (todos.StartsWith('0') || todos.Length > 11) return texto;
        var digitos = todos;
        return digitos.Length <= 10
            ? Montar(digitos, "(##) ####-####")
            : Montar(digitos, "(##) #####-####");
    }

    /// <summary>CNPJ alfanumérico: 12 letras ou números e 2 dígitos verificadores; letras em maiúsculas.</summary>
    private static string Cnpj(string texto)
    {
        var caracteres = new StringBuilder(14);
        foreach (var c in texto.ToUpperInvariant())
        {
            if (caracteres.Length == 14) break;
            var permitido = caracteres.Length < 12 ? char.IsAsciiLetterOrDigit(c) : char.IsAsciiDigit(c);
            if (permitido) caracteres.Append(c);
        }
        return caracteres.ToString();
    }

    private static string Digitos(string texto, int maximo)
    {
        var digitos = new StringBuilder(Math.Min(maximo, texto.Length));
        foreach (var c in texto)
        {
            if (digitos.Length == maximo) break;
            if (char.IsAsciiDigit(c)) digitos.Append(c);
        }
        return digitos.ToString();
    }

    /// <summary>Preenche os '#' do modelo com os caracteres; o que é fixo só entra antes de um caractere real.</summary>
    private static string Montar(string caracteres, string modelo)
    {
        var resultado = new StringBuilder(modelo.Length);
        var i = 0;
        foreach (var m in modelo)
        {
            if (i == caracteres.Length) break;
            if (m == '#') resultado.Append(caracteres[i++]);
            else resultado.Append(m);
        }
        return resultado.ToString();
    }
}
