using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Filtro rápido embaixo do título de uma coluna. Não é um filtro à parte: escreve no campo do painel de filtros com o
/// mesmo Id (vira condição, chip, entra na visão e na exportação), e mostra o que o painel tiver nesse campo.
/// Texto: "contém" (ou o primeiro operador de texto do campo). Número: "1000" (igual), "1000.." ou ">=1000" (a partir
/// de), "..500" ou "<=500" (até), "10..50" (entre). Data: "15/09/2026", "09/2026", "2026", "01/09/2026..30/09/2026",
/// ">=01/09/2026", "<=30/09/2026". Lista: uma opção (várias, só pelo painel).
/// </summary>
public sealed partial class FiltroColuna : ObservableObject
{
    public static readonly Opcao<string> Todos = new(string.Empty, "Todos");

    private bool _escrevendo;

    public FiltroColuna(CampoFiltroItem campo)
    {
        Campo = campo;
        Escolhas = EhEscolha ? [Todos, .. campo.Opcoes.Select(o => new Opcao<string>(o.Valor, o.Texto))] : [];
        Escrever(Ler);
        campo.PropertyChanged += CampoMudou;
        foreach (var o in campo.Opcoes) o.PropertyChanged += CampoMudou;
    }

    public CampoFiltroItem Campo { get; }

    /// <summary>Lista fechada (situação, natureza, sexo, vendedor...): escolhe uma opção.</summary>
    public bool EhEscolha => Campo.EhLista && !Campo.EhMunicipio;

    /// <summary>Digitado: texto, número ou data.</summary>
    public bool EhDigitado => Campo.Definicao.Tipo is TipoCampoFiltro.Texto or TipoCampoFiltro.Numero or TipoCampoFiltro.Data;

    /// <summary>"Todos" + as opções do campo (lista comum: o Picker precisa de IList).</summary>
    public List<Opcao<string>> Escolhas { get; }

    public string Dica => Campo.Definicao.Tipo switch
    {
        TipoCampoFiltro.Numero => "ex.: 1000.. ou 10..50",
        TipoCampoFiltro.Data => "ex.: 09/2026",
        _ => OperadorDeTexto() switch
        {
            OperadorFiltro.ComecaCom => "começa com…",
            OperadorFiltro.Igual => "igual a…",
            _ => "contém…"
        }
    };

    /// <summary>Texto digitado que ainda não forma um filtro (ex.: "32/13"): a coluna mostra em vermelho e não filtra.</summary>
    [ObservableProperty] private bool _invalido;

    [ObservableProperty] private string _texto = string.Empty;

    [ObservableProperty] private Opcao<string>? _escolha;

    partial void OnTextoChanged(string value)
    {
        if (_escrevendo) return;
        Escrever(() => Invalido = !AplicarTexto(value ?? string.Empty));
    }

    partial void OnEscolhaChanged(Opcao<string>? value)
    {
        if (_escrevendo || value is null) return;
        Escrever(() =>
        {
            var marcar = value.Valor.Length > 0;
            if (marcar) Campo.Operador = Operador(OperadorFiltro.UmDestes) ?? Campo.Operador;
            foreach (var o in Campo.Opcoes) o.Marcado = marcar && o.Valor == value.Valor;
            Campo.Marcado = marcar;
        });
    }

    private void Escrever(Action alterar)
    {
        _escrevendo = true;
        try { alterar(); }
        finally { _escrevendo = false; }
    }

    private void CampoMudou(object? sender, PropertyChangedEventArgs e)
    {
        if (_escrevendo) return;
        Escrever(Ler);
    }

    /// <summary>Mostra o que o painel tem no campo (marcado e completo), no formato da linha de filtro.</summary>
    private void Ler()
    {
        var condicao = Campo.ParaCondicao();
        Invalido = false;
        if (EhEscolha)
        {
            var marcadas = condicao is { Operador: OperadorFiltro.UmDestes } ? condicao.Valores : [];
            Escolha = marcadas.Count == 1 ? Escolhas.FirstOrDefault(o => o.Valor == marcadas[0]) : marcadas.Count == 0 ? Todos : null;
            return;
        }
        if (condicao is null) { if (!Campo.Marcado) Texto = string.Empty; return; }
        Texto = Campo.Definicao.Tipo switch
        {
            TipoCampoFiltro.Numero or TipoCampoFiltro.Data => condicao.Operador switch
            {
                OperadorFiltro.Entre when Campo.Valor == Campo.ValorFinal => Campo.Valor,
                OperadorFiltro.Entre => $"{Campo.Valor}..{Campo.ValorFinal}",
                OperadorFiltro.APartirDe => $"{Campo.Valor}..",
                OperadorFiltro.Ate => $"..{Campo.Valor}",
                _ => Campo.Valor
            },
            _ => Campo.Valor
        };
    }

    /// <summary>Escreve no campo do painel. Falso = texto que ainda não forma um filtro (o campo fica desmarcado).</summary>
    private bool AplicarTexto(string texto)
    {
        texto = texto.Trim();
        if (texto.Length == 0)
        {
            Campo.Marcado = false;
            return true;
        }
        switch (Campo.Definicao.Tipo)
        {
            case TipoCampoFiltro.Numero:
                return Faixa(texto, n => decimal.TryParse(n, NumberStyles.Number, TextoTela.Brasil, out _), (de, ate) => (de, ate));
            case TipoCampoFiltro.Data:
                return Faixa(texto, d => Periodo(d) is not null, (de, ate) => (Periodo(de)!.Value.De, Periodo(ate)!.Value.Ate),
                    unico: d => Periodo(d)!.Value);
            default:
                Campo.Operador = Operador(OperadorDeTexto()) ?? Campo.Operador;
                Campo.Valor = texto;
                Campo.Marcado = true;
                return Campo.ParaCondicao() is not null;
        }
    }

    /// <summary>"a..b", "a..", "..b", ">=a", "<=b", ">a", "<b" ou só "a".</summary>
    private bool Faixa(string texto, Func<string, bool> valido, Func<string, string, (string De, string Ate)> entre,
                       Func<string, (string De, string Ate)>? unico = null)
    {
        string? de = null, ate = null;
        var partes = texto.Split("..", 2, StringSplitOptions.TrimEntries);
        if (partes.Length == 2) { de = partes[0]; ate = partes[1]; }
        else if (texto.StartsWith(">=") || texto.StartsWith("<=")) { if (texto[0] == '>') de = texto[2..].Trim(); else ate = texto[2..].Trim(); }
        else if (texto.StartsWith('>') || texto.StartsWith('<')) { if (texto[0] == '>') de = texto[1..].Trim(); else ate = texto[1..].Trim(); }
        else { de = texto; ate = texto; }

        de = string.IsNullOrEmpty(de) ? null : de;
        ate = string.IsNullOrEmpty(ate) ? null : ate;
        if ((de is null && ate is null) || (de is not null && !valido(de)) || (ate is not null && !valido(ate)))
        {
            Campo.Marcado = false;
            return false;
        }

        OperadorFiltro operador;
        string valor, valorFinal = string.Empty;
        if (de is not null && ate is not null)
        {
            (valor, valorFinal) = de == ate && unico is not null ? unico(de) : entre(de, ate);
            operador = OperadorFiltro.Entre;
        }
        else if (de is not null)
        {
            valor = unico is not null ? unico(de).De : de;
            operador = OperadorFiltro.APartirDe;
        }
        else
        {
            valor = unico is not null ? unico(ate!).Ate : ate!;
            operador = OperadorFiltro.Ate;
        }
        if (Operador(operador) is not { } escolhido)
        {
            Campo.Marcado = false;
            return false;
        }
        Campo.Operador = escolhido;
        Campo.Valor = valor;
        Campo.ValorFinal = valorFinal;
        Campo.Marcado = true;
        return Campo.ParaCondicao() is not null;
    }

    /// <summary>"15/09/2026" → o dia; "09/2026" → o mês; "2026" → o ano (início e fim, em dd/mm/aaaa).</summary>
    private static (string De, string Ate)? Periodo(string texto)
    {
        var br = TextoTela.Brasil;
        if (TextoTela.TentarData(texto, out var dia) && dia is { } d)
            return (TextoTela.Data(d), TextoTela.Data(d));
        if (DateOnly.TryParseExact(texto, ["MM/yyyy", "M/yyyy"], br, DateTimeStyles.None, out var mes))
            return (TextoTela.Data(mes), TextoTela.Data(mes.AddMonths(1).AddDays(-1)));
        if (texto.Length == 4 && int.TryParse(texto, NumberStyles.None, br, out var ano) && ano is >= 1900 and <= 2200)
            return (TextoTela.Data(new DateOnly(ano, 1, 1)), TextoTela.Data(new DateOnly(ano, 12, 31)));
        return null;
    }

    private OperadorFiltro OperadorDeTexto() =>
        new[] { OperadorFiltro.Contem, OperadorFiltro.ComecaCom, OperadorFiltro.Igual }
            .FirstOrDefault(o => Campo.Operadores.Any(x => x.Valor == o), OperadorFiltro.Contem);

    private Opcao<OperadorFiltro>? Operador(OperadorFiltro o) => Campo.Operadores.FirstOrDefault(x => x.Valor == o);
}
