using Lone.Core.Validacao;

namespace Lone.Core.ObjetosDeValor;

/// <summary>CEP brasileiro: 8 dígitos.</summary>
public sealed record Cep
{
    private Cep(string valor) => Valor = valor;

    public string Valor { get; }
    public string Formatado => $"{Valor[..5]}-{Valor[5..]}";

    public static bool EhValido(string? texto) => TentarCriar(texto, out _);

    public static bool TentarCriar(string? texto, out Cep? cep)
    {
        var digitos = Documento.SomenteDigitos(texto);
        cep = digitos.Length == 8 && digitos != "00000000" ? new Cep(digitos) : null;
        return cep is not null;
    }

    public override string ToString() => Formatado;
}
