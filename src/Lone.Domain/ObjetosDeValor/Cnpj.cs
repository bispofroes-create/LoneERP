using Lone.Domain.Validacao;

namespace Lone.Domain.ObjetosDeValor;

/// <summary>
/// CNPJ válido, numérico ou alfanumérico. Raiz (8 posições) identifica a empresa;
/// ordem (4 posições) identifica o estabelecimento — 0001 é a matriz.
/// </summary>
public sealed record Cnpj
{
    private Cnpj(string valor) => Valor = valor;

    /// <summary>14 caracteres, sem máscara, letras em maiúsculas.</summary>
    public string Valor { get; }

    public string Raiz => Valor[..8];
    public string Ordem => Valor.Substring(8, 4);
    public bool EhMatriz => Ordem == "0001";
    public string Formatado => Documento.Formatar(Valor);

    public static bool EhValido(string? texto) => Documento.CnpjValido(texto);

    public static bool TentarCriar(string? texto, out Cnpj? cnpj)
    {
        cnpj = EhValido(texto) ? new Cnpj(Documento.Normalizar(texto)) : null;
        return cnpj is not null;
    }

    public override string ToString() => Formatado;
}
