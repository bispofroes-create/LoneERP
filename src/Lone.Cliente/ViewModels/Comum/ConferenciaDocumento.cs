using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Comum;

/// <summary>
/// Conferência de CPF/CNPJ na tela, com as mesmas regras da API (dígitos verificadores; CNPJ pode ser alfanumérico).
/// Enquanto a pessoa digita não acusa erro; acusa quando o número está completo e errado, ou ao sair do campo.
/// A API continua conferindo ao gravar: isto só antecipa o aviso para junto do campo.
/// </summary>
public static class ConferenciaDocumento
{
    public const int TamanhoCpf = 11;
    public const int TamanhoCnpj = 14;

    /// <summary>Mensagem de erro do CPF (vazio = sem erro). <paramref name="conferido"/> = o foco já saiu do campo.</summary>
    public static string ErroCpf(string? texto, bool conferido)
    {
        var numero = Documento.SomenteDigitos(texto);
        if (numero.Length == 0) return string.Empty;
        if (numero.Length < TamanhoCpf) return conferido ? "CPF incompleto: são 11 dígitos." : string.Empty;
        if (numero.Length > TamanhoCpf) return "CPF tem 11 dígitos.";
        return Documento.CpfValido(numero) ? string.Empty : "CPF inválido: confira os dígitos.";
    }

    public static bool CpfValido(string? texto) => Documento.SomenteDigitos(texto) is { Length: TamanhoCpf } n && Documento.CpfValido(n);

    /// <summary>Mensagem de erro do CNPJ (vazio = sem erro). Aceita o CNPJ alfanumérico (letras nas 12 primeiras posições).</summary>
    public static string ErroCnpj(string? texto, bool conferido)
    {
        var numero = Documento.Normalizar(texto);
        if (numero.Length == 0) return string.Empty;
        if (numero.Length < TamanhoCnpj) return conferido ? "CNPJ incompleto: são 14 posições." : string.Empty;
        if (numero.Length > TamanhoCnpj) return "CNPJ tem 14 posições.";
        return Documento.CnpjValido(numero) ? string.Empty : "CNPJ inválido: confira os dígitos.";
    }

    public static bool CnpjValido(string? texto) => Documento.Normalizar(texto) is { Length: TamanhoCnpj } n && Documento.CnpjValido(n);
}
