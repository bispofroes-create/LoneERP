using System.Security.Cryptography;
using System.Text;

namespace Lone.Aplicacao.Seguranca;

/// <summary>
/// PBKDF2-HMAC-SHA256 com sal aleatório de 16 bytes e 600.000 iterações (recomendação OWASP).
/// Formato gravado: "PBKDF2-SHA256$iteracoes$sal$hash" (Base64). As iterações ficam no texto,
/// então dá para aumentá-las no futuro sem invalidar senhas antigas.
/// </summary>
public sealed class HasherSenhaPbkdf2 : IHasherSenha
{
    private const string Algoritmo = "PBKDF2-SHA256";
    private const int Iteracoes = 600_000;
    private const int TamanhoSal = 16;
    private const int TamanhoHash = 32;

    public string Gerar(string senha)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanhoSal);
        var hash = Derivar(senha, sal, Iteracoes);
        return $"{Algoritmo}${Iteracoes}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string senha, string hashGravado)
    {
        var partes = hashGravado.Split('$');
        if (partes.Length != 4 || partes[0] != Algoritmo || !int.TryParse(partes[1], out var iteracoes))
            return false;

        try
        {
            var sal = Convert.FromBase64String(partes[2]);
            var esperado = Convert.FromBase64String(partes[3]);
            var calculado = Derivar(senha, sal, iteracoes);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derivar(string senha, byte[] sal, int iteracoes) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), sal, iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
}
