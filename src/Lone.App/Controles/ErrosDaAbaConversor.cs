using System.Globalization;

namespace Lone.App.Controles;

/// <summary>
/// "●2" ao lado do nome da aba com erros (Lone Contextual, Fase 1). Valores: a aba (enum) e o dicionário aba → quantidade
/// de erros do formulário. Sem erros: vazio.
/// </summary>
public sealed class ErrosDaAbaConversor : IMultiValueConverter
{
    public object Convert(object[] valores, Type tipo, object? parametro, CultureInfo cultura)
    {
        if (valores.Length < 2 || valores[0] is null || valores[1] is not System.Collections.IDictionary porAba) return string.Empty;
        return porAba.Contains(valores[0]) && porAba[valores[0]] is int n && n > 0 ? $"●{n}" : string.Empty;
    }

    public object[] ConvertBack(object? valor, Type[] tipos, object? parametro, CultureInfo cultura) => throw new NotSupportedException();
}
