using Lone.Domain.Validacao;

namespace Lone.Domain.ObjetosDeValor;

/// <summary>CPF válido (dígitos verificadores conferidos). Só existe se for válido.</summary>
public sealed record Cpf
{
    private Cpf(string valor) => Valor = valor;

    /// <summary>11 dígitos, sem máscara.</summary>
    public string Valor { get; }

    public string Formatado => Documento.Formatar(Valor);

    public static bool EhValido(string? texto) => Documento.CpfValido(texto);

    public static bool TentarCriar(string? texto, out Cpf? cpf)
    {
        cpf = EhValido(texto) ? new Cpf(Documento.Normalizar(texto)) : null;
        return cpf is not null;
    }

    public override string ToString() => Formatado;
}
