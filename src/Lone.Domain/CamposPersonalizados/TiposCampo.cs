using System.Globalization;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.ObjetosDeValor;

namespace Lone.Domain.CamposPersonalizados;

/// <summary>Registro dos tipos de campo personalizado. Para criar um tipo: uma classe ITipoCampo e uma linha aqui.</summary>
public static class TiposCampo
{
    private static readonly Dictionary<TipoCampoPersonalizado, ITipoCampo> PorTipo = new ITipoCampo[]
    {
        new TipoTexto(TipoCampoPersonalizado.Texto, "Texto", 200),
        new TipoTexto(TipoCampoPersonalizado.TextoLongo, "Texto longo", 2000),
        new TipoNumero(TipoCampoPersonalizado.Inteiro, "Número inteiro", casasFixas: 0),
        new TipoNumero(TipoCampoPersonalizado.Decimal, "Número decimal", casasFixas: null),
        new TipoNumero(TipoCampoPersonalizado.Moeda, "Moeda (R$)", casasFixas: 2),
        new TipoSimNao(),
        new TipoData(TipoCampoPersonalizado.Data, "Data", comHora: false),
        new TipoHora(),
        new TipoData(TipoCampoPersonalizado.DataHora, "Data e hora", comHora: true),
        new TipoLista(),
        new TipoEmail(),
        new TipoTelefone()
    }.ToDictionary(t => t.Tipo);

    /// <summary>Tamanho da coluna ValorTexto (o maior texto aceito por qualquer tipo).</summary>
    public const int TamanhoMaximoTexto = 2000;

    /// <summary>Casas decimais aceitas no tipo decimal (a coluna guarda 6).</summary>
    public const int MaximoCasasDecimais = 6;

    public static IReadOnlyCollection<ITipoCampo> Todos => PorTipo.Values;

    public static ITipoCampo Obter(TipoCampoPersonalizado tipo) =>
        PorTipo.TryGetValue(tipo, out var t) ? t : throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de campo desconhecido.");

    public static bool Existe(TipoCampoPersonalizado tipo) => PorTipo.ContainsKey(tipo);

    internal static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

internal sealed class TipoTexto(TipoCampoPersonalizado tipo, string nome, int maximo) : ITipoCampo
{
    public TipoCampoPersonalizado Tipo => tipo;
    public string Nome => nome;
    public ColunaValor Coluna => ColunaValor.Texto;

    public string? Normalizar(CampoPersonalizado campo, PessoaValorPersonalizado valor)
    {
        valor.ValorTexto = TiposCampo.Texto(valor.ValorTexto);
        return valor.ValorTexto is { Length: var n } && n > maximo ? $"no máximo {maximo} caracteres" : null;
    }
}

internal sealed class TipoNumero(TipoCampoPersonalizado tipo, string nome, int? casasFixas) : ITipoCampo
{
    public TipoCampoPersonalizado Tipo => tipo;
    public string Nome => nome;
    public ColunaValor Coluna => ColunaValor.Numero;

    public string? Normalizar(CampoPersonalizado campo, PessoaValorPersonalizado valor)
    {
        if (valor.ValorNumero is not { } numero) return null;

        var casas = casasFixas ?? Math.Clamp((int)(campo.CasasDecimais ?? 2), 0, TiposCampo.MaximoCasasDecimais);
        if (casasFixas == 0 && numero != decimal.Truncate(numero))
            return "use um número inteiro";

        numero = Math.Round(numero, casas, MidpointRounding.AwayFromZero);
        valor.ValorNumero = numero;

        if (Math.Abs(numero) >= 1_000_000_000_000m) return "número grande demais";
        if (campo.Minimo is { } minimo && numero < minimo) return $"o mínimo é {Formatar(minimo, casas)}";
        if (campo.Maximo is { } maximo && numero > maximo) return $"o máximo é {Formatar(maximo, casas)}";
        return null;
    }

    private static string Formatar(decimal n, int casas) => n.ToString("N" + casas, CultureInfo.GetCultureInfo("pt-BR"));
}

internal sealed class TipoSimNao : ITipoCampo
{
    public TipoCampoPersonalizado Tipo => TipoCampoPersonalizado.SimNao;
    public string Nome => "Sim/Não";
    public ColunaValor Coluna => ColunaValor.Logico;
    public string? Normalizar(CampoPersonalizado campo, PessoaValorPersonalizado valor) => null;
}

internal sealed class TipoData(TipoCampoPersonalizado tipo, string nome, bool comHora) : ITipoCampo
{
    private static readonly DateTime Minimo = new(1900, 1, 1);
    private static readonly DateTime Maximo = new(2200, 12, 31);

    public TipoCampoPersonalizado Tipo => tipo;
    public string Nome => nome;
    public ColunaValor Coluna => ColunaValor.Data;

    /// <summary>Data e hora "de parede" (como digitada), sem fuso; só data = meia-noite.</summary>
    public string? Normalizar(CampoPersonalizado campo, PessoaValorPersonalizado valor)
    {
        if (valor.ValorData is not { } data) return null;
        data = DateTime.SpecifyKind(comHora ? data.AddTicks(-(data.Ticks % TimeSpan.TicksPerMinute)) : data.Date, DateTimeKind.Unspecified);
        valor.ValorData = data;
        return data < Minimo || data > Maximo ? "data fora do intervalo aceito (1900 a 2200)" : null;
    }
}

/// <summary>Hora do dia guardada como texto "HH:mm" (formato fixo: ordena e filtra corretamente).</summary>
internal sealed class TipoHora : ITipoCampo
{
    public TipoCampoPersonalizado Tipo => TipoCampoPersonalizado.Hora;
    public string Nome => "Hora";
    public ColunaValor Coluna => ColunaValor.Texto;

    public string? Normalizar(CampoPersonalizado campo, PessoaValorPersonalizado valor)
    {
        var texto = TiposCampo.Texto(valor.ValorTexto);
        if (texto is null)
        {
            valor.ValorTexto = null;
            return null;
        }
        if (!TimeOnly.TryParseExact(texto, ["H:mm", "HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
            return "hora inválida (use hh:mm)";
        valor.ValorTexto = hora.ToString("HH:mm", CultureInfo.InvariantCulture);
        return null;
    }
}

internal sealed class TipoLista : ITipoCampo
{
    public TipoCampoPersonalizado Tipo => TipoCampoPersonalizado.Lista;
    public string Nome => "Lista de opções";
    public ColunaValor Coluna => ColunaValor.Opcao;

    /// <summary>A opção precisa ser deste campo. Opção desativada só vale se já era a gravada (conferido pelo validador).</summary>
    public string? Normalizar(CampoPersonalizado campo, PessoaValorPersonalizado valor) =>
        valor.OpcaoId is { } id && campo.Opcoes.All(o => o.Id != id) ? "opção inexistente" : null;
}

internal sealed class TipoEmail : ITipoCampo
{
    public TipoCampoPersonalizado Tipo => TipoCampoPersonalizado.Email;
    public string Nome => "E-mail";
    public ColunaValor Coluna => ColunaValor.Texto;

    public string? Normalizar(CampoPersonalizado campo, PessoaValorPersonalizado valor)
    {
        var texto = TiposCampo.Texto(valor.ValorTexto);
        valor.ValorTexto = texto;
        if (texto is null) return null;
        if (!Email.TentarCriar(texto, out var email)) return "e-mail inválido";
        valor.ValorTexto = email!.Valor;
        return null;
    }
}

internal sealed class TipoTelefone : ITipoCampo
{
    public TipoCampoPersonalizado Tipo => TipoCampoPersonalizado.Telefone;
    public string Nome => "Telefone";
    public ColunaValor Coluna => ColunaValor.Texto;

    public string? Normalizar(CampoPersonalizado campo, PessoaValorPersonalizado valor)
    {
        var texto = TiposCampo.Texto(valor.ValorTexto);
        valor.ValorTexto = texto;
        if (texto is null) return null;
        if (!Telefone.TentarCriar(texto, out var telefone)) return "telefone inválido (informe com DDD)";
        valor.ValorTexto = telefone!.Normalizado;
        return null;
    }
}
