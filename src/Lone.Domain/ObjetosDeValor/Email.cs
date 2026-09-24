using System.Net.Mail;

namespace Lone.Domain.ObjetosDeValor;

/// <summary>Endereço de e-mail válido, guardado em minúsculas.</summary>
public sealed record Email
{
    private Email(string valor) => Valor = valor;

    public string Valor { get; }

    public static bool EhValido(string? texto) => TentarCriar(texto, out _);

    public static bool TentarCriar(string? texto, out Email? email)
    {
        email = null;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var valor = texto.Trim().ToLowerInvariant();
        // MailAddress aceita "a@b"; exigimos domínio com ponto e sem nome de exibição.
        if (!MailAddress.TryCreate(valor, out var endereco) || endereco.Address != valor)
            return false;
        if (!endereco.Host.Contains('.') || endereco.Host.StartsWith('.') || endereco.Host.EndsWith('.'))
            return false;

        email = new Email(valor);
        return true;
    }

    public override string ToString() => Valor;
}
