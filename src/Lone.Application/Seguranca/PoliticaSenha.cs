using System.Text.RegularExpressions;

namespace Lone.Application.Seguranca;

/// <summary>Regras de senha e de login, num lugar só (usadas no primeiro acesso, cadastro e troca de senha).</summary>
public static partial class PoliticaSenha
{
    public const int TamanhoMinimo = 8;

    public const string MensagemLoginInvalido =
        "Login inválido: use de 3 a 60 letras sem acento, números, ponto, hífen ou sublinhado.";

    public static List<string> ValidarSenha(string? senha, string? login)
    {
        var erros = new List<string>();
        if (string.IsNullOrEmpty(senha) || senha.Length < TamanhoMinimo)
            erros.Add($"A senha precisa ter pelo menos {TamanhoMinimo} caracteres.");
        else
        {
            if (!senha.Any(char.IsLetter) || !senha.Any(char.IsDigit))
                erros.Add("A senha precisa ter letras e números.");
            if (login is not null && string.Equals(senha, login, StringComparison.OrdinalIgnoreCase))
                erros.Add("A senha não pode ser igual ao login.");
        }
        return erros;
    }

    /// <summary>Login em minúsculas, sem espaços nas pontas.</summary>
    public static string NormalizarLogin(string? login) => (login ?? string.Empty).Trim().ToLowerInvariant();

    public static bool LoginValido(string login) => FormatoLogin().IsMatch(login);

    /// <summary>3 a 60 caracteres: letras sem acento, números, ponto, hífen e sublinhado.</summary>
    [GeneratedRegex("^[a-z0-9._-]{3,60}$")]
    private static partial Regex FormatoLogin();
}
