using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Grade;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Cliente.Sessao;
using Lone.Contracts.Seguranca;

namespace Lone.Cliente.ViewModels.Comercial;

/// <summary>Textos das regras de crédito na ausência (parâmetros e coberturas).</summary>
public static class TextosCobertura
{
    public static readonly Opcao<RegraCreditoAusencia>[] Creditos =
    [
        new(RegraCreditoAusencia.Titular, "Do titular (a carteira é dele)"),
        new(RegraCreditoAusencia.Substituto, "De quem cobre"),
        new(RegraCreditoAusencia.Dividido, "Dividido (% para quem cobre)")
    ];

    public static string Situacao(SituacaoCobertura s) => s switch
    {
        SituacaoCobertura.Agendada => "Agendada",
        SituacaoCobertura.Vigente => "Vigente",
        SituacaoCobertura.Encerrada => "Encerrada",
        _ => "Cancelada"
    };

    public static string Periodo(DateOnly inicio, DateOnly fim) => $"{TextoTela.Data(inicio)} a {TextoTela.Data(fim)}";
}

// ======================================================================= Parâmetros

/// <summary>Parâmetros do módulo Comercial: antecedência do aviso de fim do vínculo e crédito padrão nas ausências.</summary>
public sealed partial class ParametrosComerciaisViewModel : ViewModelBase
{
    private readonly ComercialApi _api;
    private byte[]? _versao;

    public ParametrosComerciaisViewModel(ComercialApi api) => _api = api;

    [ObservableProperty] private string _diasAviso = "30";

    /// <summary>Até quantos dias no passado valem o efeito de uma transferência e o início de uma cobertura (0 = só hoje em diante).</summary>
    [ObservableProperty] private string _diasRetroativos = "30";
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Dividido))] private Opcao<RegraCreditoAusencia> _credito = TextosCobertura.Creditos[0];
    [ObservableProperty] private string _percentualSubstituto = string.Empty;

    public IReadOnlyList<Opcao<RegraCreditoAusencia>> Creditos => TextosCobertura.Creditos;
    public bool Dividido => Credito.Valor == RegraCreditoAusencia.Dividido;

    [RelayCommand]
    private Task CarregarAsync() => ExecutarAsync(async () => Aplicar(await _api.ObterParametrosAsync()));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        var erros = new List<string>();
        if (!TextoTela.TentarInteiro(DiasAviso, out var dias) || dias is null) erros.Add("Aviso de fim do vínculo: informe os dias.");
        if (!TextoTela.TentarInteiro(DiasRetroativos, out var retroativos) || retroativos is null)
            erros.Add("Datas no passado: informe os dias (0 = só hoje ou datas futuras).");
        if (!TextoTela.TentarDecimal(PercentualSubstituto, out var pct)) erros.Add("Percentual de quem cobre: número inválido.");
        if (erros.Count > 0)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        ParametrosComerciaisDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarParametrosAsync(new ParametrosComerciaisDto
            {
                Versao = _versao,
                DiasAvisoFimVinculo = dias ?? 30,
                DiasRetroativosMaximo = retroativos ?? 30,
                CreditoNaAusencia = Credito.Valor,
                PercentualSubstitutoPadrao = Dividido ? pct : null
            })))
            return;
        Aplicar(salvo!);
        Mostrar("Parâmetros salvos.", TipoMensagem.Sucesso);
    }

    private void Aplicar(ParametrosComerciaisDto p)
    {
        _versao = p.Versao;
        DiasAviso = TextoTela.Inteiro(p.DiasAvisoFimVinculo);
        DiasRetroativos = TextoTela.Inteiro(p.DiasRetroativosMaximo);
        Credito = Opcao.De(TextosCobertura.Creditos, p.CreditoNaAusencia);
        PercentualSubstituto = TextoTela.Decimal(p.PercentualSubstitutoPadrao);
    }
}

// ======================================================================= Carteira vencendo
/// <summary>Linha da Carteira vencendo (03/10/2026: lista em colunas, filtros e exportação, como o histórico de Pessoas).</summary>
public sealed class LinhaVencendo
{
    public LinhaVencendo(VinculoVencendoDto v) => Item = v;

    public VinculoVencendoDto Item { get; }
    public string Cliente => Item.Cliente;
    public string Papel => Item.Papel;
    public string Pessoa => Item.Pessoa;
    public string Empresa => Item.Empresa ?? "—";
    public string Quem => $"{Item.Papel}: {Item.Pessoa}" + (Item.Empresa is { } e ? $" · {e}" : string.Empty);
    public string Inicio => TextoTela.Data(Item.InicioEm);
    public string Fim => TextoTela.Data(Item.FimEm);
    public string Faltam => CarteiraVencendo.Faltam(Item.DiasRestantes);
    public string TomFaltam => CarteiraVencendo.Tom(Item.DiasRestantes);
}

/// <summary>Atalho com contagem na barra da lista ("Hoje 1", "Até 7 dias 3"): tocar filtra pela faixa do prazo.</summary>
public sealed partial class AtalhoFaixa : ObservableObject
{
    public AtalhoFaixa(FaixaVencimento faixa, string texto)
    {
        Faixa = faixa;
        Texto = texto;
    }

    public FaixaVencimento Faixa { get; }
    public string Texto { get; }
    [ObservableProperty] private int _quantidade;
    [ObservableProperty] private bool _selecionado;

    /// <summary>Some quando repete o período ("Até 30 dias" com o período de 30 dias é o mesmo que "Todos").</summary>
    [ObservableProperty] private bool _visivel = true;
}

/// <summary>Faixas do prazo restante (atalhos da Carteira vencendo).</summary>
public enum FaixaVencimento { Todos, Hoje, AteSete, AteTrinta }

/// <summary>Opção do campo Período ("Aviso padrão (30 dias)", "Próximos 60 dias", "Personalizado…").</summary>
public sealed record PeriodoVencimento(int? Dias, string Texto, bool Personalizado = false)
{
    public override string ToString() => Texto;
}

/// <summary>Regras da Carteira vencendo, sem tela (testadas): período, faixas, filtro, ordem, resumo, cor e CSV.</summary>
public static class CarteiraVencendo
{
    public static readonly Opcao<string?> Todos = new(null, "Todos");
    public static readonly Opcao<string?> Todas = new(null, "Todas");

    /// <summary>Prazos prontos do campo Período (o aviso configurado vem primeiro, com o número real).</summary>
    public static readonly int[] PrazosProntos = [7, 15, 30, 60, 90, 180, 365];

    /// <summary>Próximos prazos oferecidos pela lista vazia ("Ampliar para 90 dias").</summary>
    private static readonly int[] Ampliacoes = [7, 15, 30, 60, 90, 180, 365];

    public static PeriodoVencimento[] Periodos(int? diasDoAviso) =>
    [
        new(null, diasDoAviso is { } a ? $"Aviso padrão ({Quantos(a)})" : "Aviso padrão"),
        .. PrazosProntos.Select(d => new PeriodoVencimento(d, $"Próximos {Quantos(d)}")),
        new(null, "Personalizado…", Personalizado: true)
    ];

    /// <summary>"Termina hoje", "Falta 1 dia", "Faltam 12 dias".</summary>
    public static string Faltam(int dias) => dias switch
    {
        <= 0 => "Termina hoje",
        1 => "Falta 1 dia",
        _ => $"Faltam {dias.ToString("N0", TextoTela.Brasil)} dias"
    };

    /// <summary>Cor do prazo: até 7 dias vermelho (Erro), até 30 laranja (Aviso), depois cinza.</summary>
    public static string Tom(int dias) => dias <= 7 ? "Erro" : dias <= 30 ? "Aviso" : "Neutro";

    public static bool NaFaixa(VinculoVencendoDto v, FaixaVencimento faixa) => faixa switch
    {
        FaixaVencimento.Hoje => v.DiasRestantes <= 0,
        FaixaVencimento.AteSete => v.DiasRestantes <= 7,
        FaixaVencimento.AteTrinta => v.DiasRestantes <= 30,
        _ => true
    };

    /// <summary>Opções de um filtro tiradas dos vencimentos encontrados (nunca uma opção que dá lista vazia).</summary>
    public static Opcao<string?>[] Opcoes(IEnumerable<string?> valores, Opcao<string?> primeira) =>
    [
        primeira,
        .. valores.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase).Select(v => new Opcao<string?>(v, v))
    ];

    /// <summary>Os vencimentos que passam no filtro, do fim mais próximo ao mais distante.</summary>
    public static List<VinculoVencendoDto> Filtrar(IEnumerable<VinculoVencendoDto> todos, string? papel, string? pessoa,
        string? empresa, string? cliente, FaixaVencimento faixa = FaixaVencimento.Todos)
    {
        var busca = (cliente ?? string.Empty).Trim();
        var comparar = System.Globalization.CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        const System.Globalization.CompareOptions semCaixaNemAcento =
            System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace;
        return [.. todos
            .Where(v => papel is null || string.Equals(v.Papel, papel, StringComparison.CurrentCultureIgnoreCase))
            .Where(v => pessoa is null || string.Equals(v.Pessoa, pessoa, StringComparison.CurrentCultureIgnoreCase))
            .Where(v => empresa is null || string.Equals(v.Empresa, empresa, StringComparison.CurrentCultureIgnoreCase))
            .Where(v => busca.Length == 0 || comparar.IndexOf(v.Cliente, busca, semCaixaNemAcento) >= 0)
            .Where(v => NaFaixa(v, faixa))
            .OrderBy(v => v.FimEm).ThenBy(v => v.Cliente, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Ordem pela coluna clicada (chave da coluna); sem chave, a ordem padrão (fim mais próximo).</summary>
    public static List<VinculoVencendoDto> Ordenar(IEnumerable<VinculoVencendoDto> itens, string? chave, bool decrescente)
    {
        Func<VinculoVencendoDto, object?> por = chave switch
        {
            "titulo" => v => v.Cliente,
            "papel" => v => v.Papel,
            "quem" => v => v.Pessoa,
            "empresa" => v => v.Empresa ?? string.Empty,
            "inicio" => v => v.InicioEm,
            _ => v => v.FimEm // fim e faltam andam juntos
        };
        var comparador = Comparer<object?>.Create((a, b) => a is string sa && b is string sb
            ? string.Compare(sa, sb, StringComparison.CurrentCultureIgnoreCase)
            : Comparer<object?>.Default.Compare(a, b));
        var ordem = decrescente ? itens.OrderByDescending(por, comparador) : itens.OrderBy(por, comparador);
        return [.. ordem.ThenBy(v => v.Cliente, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Título da lista com o contador: "Vínculos vencendo (12)".</summary>
    public static string TituloLista(int quantidade) => $"Vínculos vencendo ({quantidade.ToString("N0", TextoTela.Brasil)})";

    /// <summary>"Período consultado: 03/10/2026 a 02/11/2026".</summary>
    public static string TextoPeriodo(DateOnly de, DateOnly ate) => $"Período consultado: {TextoTela.Data(de)} a {TextoTela.Data(ate)}";

    /// <summary>Próximo prazo maior que o consultado (nulo se já é o maior).</summary>
    /// <summary>"Nenhum vínculo termina hoje" / "nos próximos 30 dias".</summary>
    public static string TituloSemVinculos(int dias) => dias <= 0 ? "Nenhum vínculo termina hoje" : dias == 1 ? "Nenhum vínculo termina até amanhã" : $"Nenhum vínculo termina nos próximos {TextoDias(dias)}";

    /// <summary>"1 dia", "30 dias", "365 dias".</summary>
    public static string TextoDias(int dias) => dias == 1 ? "1 dia" : $"{dias} dias";

    /// <summary>
    /// O atalho por faixa acrescenta algo ao período? "Até 7 dias" com período de até 7 dias e "Até 30 dias" com período de
    /// até 30 dias repetem "Todos" e não aparecem; "Todos" e "Hoje" sempre aparecem.
    /// </summary>
    public static bool FaixaUtil(FaixaVencimento faixa, int? dias) => faixa switch
    {
        FaixaVencimento.AteSete => dias is null or > 7,
        FaixaVencimento.AteTrinta => dias is null or > 30,
        _ => true
    };

    public static int? ProximoPrazo(int dias) => Ampliacoes.FirstOrDefault(d => d > dias) is var p and > 0 ? p : null;

    /// <summary>"12 vínculos terminam nos próximos 90 dias · 3 nesta semana · 1 hoje" (impressão).</summary>
    public static string Resumo(IReadOnlyCollection<VinculoVencendoDto> itens, int? dias)
    {
        var prazo = dias is { } d ? $"nos próximos {Quantos(d)}" : "no prazo do aviso configurado";
        if (itens.Count == 0) return $"Nenhum vínculo termina {prazo}";
        var total = itens.Count == 1 ? $"1 vínculo termina {prazo}" : $"{itens.Count.ToString("N0", TextoTela.Brasil)} vínculos terminam {prazo}";
        var semana = itens.Count(v => v.DiasRestantes is > 0 and <= 7);
        var hoje = itens.Count(v => v.DiasRestantes <= 0);
        return string.Join(" · ", new[] { total, semana > 0 ? $"{semana} nesta semana" : null, hoje > 0 ? $"{hoje} hoje" : null }
            .Where(x => x is not null));
    }

    /// <summary>CSV para o Excel (separador ";", como o Excel em português abre direto; texto entre aspas quando precisa).</summary>
    public static string Csv(IEnumerable<LinhaVencendo> linhas)
    {
        static string C(string s) => s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        var sb = new System.Text.StringBuilder("Cliente;Papel;Quem atende;Empresa;Início;Fim;Faltam (dias)\r\n");
        foreach (var l in linhas)
            sb.Append(C(l.Cliente)).Append(';').Append(C(l.Papel)).Append(';').Append(C(l.Pessoa)).Append(';')
              .Append(C(l.Item.Empresa ?? string.Empty)).Append(';').Append(l.Inicio).Append(';').Append(l.Fim).Append(';')
              .Append(Math.Max(0, l.Item.DiasRestantes).ToString(TextoTela.Brasil)).Append("\r\n");
        return sb.ToString();
    }

    private static string Quantos(int n) => n == 1 ? "1 dia" : $"{n.ToString("N0", TextoTela.Brasil)} dias";
}

/// <summary>
/// Carteira vencendo (Motor Comercial, Fase 1c; refeita em 03/10/2026 no padrão de consulta do Lone): período (aviso
/// padrão, prazos prontos ou personalizado), filtros (papel, quem atende, empresa, cliente), barra da lista com o contador,
/// atalhos por faixa ("Hoje", "Até 7 dias"...) e Exportar (imprimir/PDF e Excel), lista em colunas ordenável e lista vazia
/// com o próximo passo. A API devolve os vencimentos do período (no alcance de quem consulta); o resto é aplicado aqui.
/// </summary>
public sealed partial class CarteiraVencendoViewModel : ViewModelBase
{
    private readonly ComercialApi _api;
    private readonly AberturaDePessoa _abertura;
    private readonly IArquivos _arquivos;
    private CarteiraVencendoDto? _consulta;
    private List<VinculoVencendoDto> _filtrados = [];
    private bool _ajustandoPeriodo;

    public CarteiraVencendoViewModel(ComercialApi api, AberturaDePessoa abertura, IArquivos arquivos)
    {
        _api = api;
        _abertura = abertura;
        _arquivos = arquivos;
        _ajustandoPeriodo = true;
        try { Periodo = Periodos[0]; } // aviso padrão (sem consultar ainda: a tela consulta ao aparecer)
        finally { _ajustandoPeriodo = false; }
    }

    /// <summary>Navegação para outra tela (a tela liga ao Shell): usada para abrir a ficha do cliente.</summary>
    public Func<string, Task>? AbrirTela { get; set; }

    public bool PodeAbrirFicha => _abertura.Permitida;

    /// <summary>Lista em colunas: Cliente · Papel · Quem atende · Empresa · Início · Fim · Faltam.</summary>
    public GradeCadastro<LinhaVencendo> GradeDaLista { get; } = new(
        "Cliente", l => l.Item.VinculoId, l => l.Cliente, l => null,
        ColunaCadastro<LinhaVencendo>.Curto("papel", "Papel", l => l.Papel, 160),
        ColunaCadastro<LinhaVencendo>.Texto("quem", "Quem atende", l => l.Pessoa),
        ColunaCadastro<LinhaVencendo>.Texto("empresa", "Empresa", l => l.Empresa, 140),
        ColunaCadastro<LinhaVencendo>.Curto("inicio", "Início", l => l.Inicio, 120),
        ColunaCadastro<LinhaVencendo>.Curto("fim", "Fim", l => l.Fim, 120),
        ColunaCadastro<LinhaVencendo>.Selo("faltam", "Faltam", l => l.Faltam, l => l.TomFaltam, 150));

    [ObservableProperty] private ConteudoGrade _conteudoLista = ConteudoGrade.Vazio;

    // ---- Período ----
    [ObservableProperty] private PeriodoVencimento[] _periodos = CarteiraVencendo.Periodos(null);
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Personalizado))] private PeriodoVencimento? _periodo;

    /// <summary>Dias do período personalizado (campo com ▲▼).</summary>
    [ObservableProperty] private string _diasPersonalizados = "120";

    public bool Personalizado => Periodo?.Personalizado == true;

    // ---- Filtros ----
    [ObservableProperty] private Opcao<string?>[] _papeis = [CarteiraVencendo.Todos];
    [ObservableProperty] private Opcao<string?>? _papel = CarteiraVencendo.Todos;
    [ObservableProperty] private Opcao<string?>[] _pessoas = [CarteiraVencendo.Todos];
    [ObservableProperty] private Opcao<string?>? _pessoa = CarteiraVencendo.Todos;
    [ObservableProperty] private Opcao<string?>[] _empresas = [CarteiraVencendo.Todas];
    [ObservableProperty] private Opcao<string?>? _empresa = CarteiraVencendo.Todas;
    [ObservableProperty] private string _buscaCliente = string.Empty;

    /// <summary>Empresa só aparece com mais de uma nos vencimentos.</summary>
    public bool MostrarEmpresa => Empresas.Length > 2;

    /// <summary>Há filtro além do período e da faixa (mostra "Limpar").</summary>
    public bool Filtrado => Papel?.Valor is not null || Pessoa?.Valor is not null || Empresa?.Valor is not null || BuscaCliente.Trim().Length > 0;

    // ---- Barra da lista ----
    public IReadOnlyList<AtalhoFaixa> Atalhos { get; } =
    [
        new(FaixaVencimento.Todos, "Todos") { Selecionado = true },
        new(FaixaVencimento.Hoje, "Hoje"),
        new(FaixaVencimento.AteSete, "Até 7 dias"),
        new(FaixaVencimento.AteTrinta, "Até 30 dias")
    ];

    private FaixaVencimento _faixa = FaixaVencimento.Todos;

    [ObservableProperty] private string _tituloLista = CarteiraVencendo.TituloLista(0);

    // ---- Ordem (clicar no título da coluna) ----
    [ObservableProperty] private string? _colunaOrdenadaChave;
    [ObservableProperty] private bool _ordemDecrescente;

    /// <summary>
    /// Sem nada para mostrar, os atalhos por faixa (todos com 0) e o Exportar somem: fica só "Vínculos vencendo (0)"
    /// (menos repetição, 03/10/2026). O período é escolhido num lugar só: o campo Período.
    /// </summary>
    [ObservableProperty] private bool _mostrarAtalhos;
    [ObservableProperty] private bool _mostrarExportar;

    // ---- Lista vazia ----
    public bool ListaVazia => _filtrados.Count == 0;
    [ObservableProperty] private string _tituloVazio = "Nenhum vínculo vencendo";
    [ObservableProperty] private string _textoVazio = string.Empty;
    [ObservableProperty] private string _textoAcaoVazio = string.Empty;
    public bool MostrarAcaoVazio => TextoAcaoVazio.Length > 0;

    partial void OnEmpresasChanged(Opcao<string?>[] value) => OnPropertyChanged(nameof(MostrarEmpresa));
    partial void OnTextoAcaoVazioChanged(string value) => OnPropertyChanged(nameof(MostrarAcaoVazio));

    /// <summary>Escolher um período pronto já consulta; "Personalizado…" espera os dias e o Filtrar.</summary>
    partial void OnPeriodoChanged(PeriodoVencimento? value)
    {
        if (_ajustandoPeriodo || value is null || value.Personalizado) return;
        CarregarCommand.Execute(null);
    }

    /// <summary>Busca na API com o período escolhido e aplica filtros, faixa e ordem.</summary>
    [RelayCommand]
    private async Task CarregarAsync()
    {
        int? dias = Periodo?.Dias;
        if (Personalizado)
        {
            if (!TextoTela.TentarInteiro(DiasPersonalizados, out var d) || d is not > 0)
            {
                Mostrar("Período personalizado: informe os dias (1 a 365).", TipoMensagem.Erro);
                return;
            }
            dias = d;
        }
        CarteiraVencendoDto? consulta = null;
        if (!await ExecutarAsync(async () => consulta = await _api.CarteiraVencendoAsync(dias))) return;
        _consulta = consulta!;
        if (_consulta.DoAviso)
        {
            // O aviso padrão agora mostra o número real; mantém a escolha atual.
            var atual = Periodo;
            _ajustandoPeriodo = true;
            try
            {
                Periodos = CarteiraVencendo.Periodos(_consulta.Dias);
                Periodo = atual is { Dias: null, Personalizado: false } ? Periodos[0] : Periodos.FirstOrDefault(p => p == atual) ?? atual;
            }
            finally { _ajustandoPeriodo = false; }
        }
        var todos = _consulta.Vinculos;
        Papeis = CarteiraVencendo.Opcoes(todos.Select(v => v.Papel), CarteiraVencendo.Todos);
        Papel = Papeis.FirstOrDefault(o => o.Valor == Papel?.Valor) ?? CarteiraVencendo.Todos;
        Pessoas = CarteiraVencendo.Opcoes(todos.Select(v => v.Pessoa), CarteiraVencendo.Todos);
        Pessoa = Pessoas.FirstOrDefault(o => o.Valor == Pessoa?.Valor) ?? CarteiraVencendo.Todos;
        Empresas = CarteiraVencendo.Opcoes(todos.Select(v => v.Empresa), CarteiraVencendo.Todas);
        Empresa = Empresas.FirstOrDefault(o => o.Valor == Empresa?.Valor) ?? CarteiraVencendo.Todas;
        Aplicar();
    }

    /// <summary>Filtrar: no personalizado, busca de novo; senão filtra o que já veio.</summary>
    [RelayCommand]
    private Task FiltrarAsync()
    {
        if (Personalizado || _consulta is null) return CarregarAsync();
        Aplicar();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void LimparFiltro()
    {
        Papel = CarteiraVencendo.Todos;
        Pessoa = CarteiraVencendo.Todos;
        Empresa = CarteiraVencendo.Todas;
        BuscaCliente = string.Empty;
        SelecionarFaixa(Atalhos[0]);
    }

    /// <summary>Atalho da barra da lista: filtra pela faixa do prazo (tocar de novo volta para Todos).</summary>
    [RelayCommand]
    private void SelecionarFaixa(AtalhoFaixa? atalho)
    {
        if (atalho is null) return;
        _faixa = atalho.Selecionado && atalho.Faixa != FaixaVencimento.Todos ? FaixaVencimento.Todos : atalho.Faixa;
        foreach (var a in Atalhos) a.Selecionado = a.Faixa == _faixa;
        Aplicar();
    }

    /// <summary>Clicar no título da coluna: ordena por ela; clicar de novo inverte.</summary>
    [RelayCommand]
    private void OrdenarColuna(ColunaGradeDef? coluna)
    {
        if (coluna is null) return;
        if (ColunaOrdenadaChave == coluna.Chave) OrdemDecrescente = !OrdemDecrescente;
        else
        {
            ColunaOrdenadaChave = coluna.Chave;
            OrdemDecrescente = false;
        }
        Aplicar();
    }

    /// <summary>Ação da lista vazia: limpar o filtro, ou consultar o próximo prazo maior ("Ampliar para 90 dias").</summary>
    [RelayCommand]
    private void AcaoVazio()
    {
        if (_consulta is null) return;
        if (Filtrado || _faixa != FaixaVencimento.Todos)
        {
            LimparFiltro();
            return;
        }
        if (CarteiraVencendo.ProximoPrazo(_consulta.Dias) is not { } proximo) return;
        var pronto = Periodos.FirstOrDefault(p => p.Dias == proximo);
        if (pronto is not null) Periodo = pronto; // consulta sozinho
        else
        {
            _ajustandoPeriodo = true;
            try { Periodo = Periodos.First(p => p.Personalizado); }
            finally { _ajustandoPeriodo = false; }
            DiasPersonalizados = proximo.ToString();
            CarregarCommand.Execute(null);
        }
    }

    /// <summary>Exportar › Imprimir / PDF: a lista (com o filtro em uso) no navegador.</summary>
    [RelayCommand]
    private async Task ImprimirAsync()
    {
        var html = CarteiraVencendoImpressao.Html(DescreverFiltro(), _filtrados.Select(v => new LinhaVencendo(v)).ToList(),
            CarteiraVencendo.Resumo(_filtrados, _consulta?.Dias), DateTime.Now);
        await ExecutarAsync(() => _arquivos.AbrirAsync($"carteira-vencendo-{DateTime.Now:yyyyMMdd-HHmm}.html", System.Text.Encoding.UTF8.GetBytes(html)));
    }

    /// <summary>Exportar › Excel (CSV): abre no Excel (ou no programa padrão de planilhas).</summary>
    [RelayCommand]
    private async Task ExportarCsvAsync()
    {
        var csv = CarteiraVencendo.Csv(_filtrados.Select(v => new LinhaVencendo(v)));
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray(); // BOM: acentos certos no Excel
        await ExecutarAsync(() => _arquivos.AbrirAsync($"carteira-vencendo-{DateTime.Now:yyyyMMdd-HHmm}.csv", bytes));
    }

    /// <summary>Tocar numa linha abre a ficha do cliente (para renovar, trocar ou deixar encerrar).</summary>
    [RelayCommand]
    private async Task AbrirLinhaAsync(ILinhaGrade? linha)
    {
        if (linha is not LinhaCadastro { Item: LinhaVencendo l } || !PodeAbrirFicha || AbrirTela is null) return;
        _abertura.Pedir(l.Item.ClienteId);
        await AbrirTela(AberturaDePessoa.RotaPessoas);
    }

    private void Aplicar()
    {
        var todos = _consulta?.Vinculos ?? [];
        // Contagem dos atalhos: com os filtros, sem a faixa (cada atalho diz quantos teria).
        var semFaixa = CarteiraVencendo.Filtrar(todos, Papel?.Valor, Pessoa?.Valor, Empresa?.Valor, BuscaCliente);
        foreach (var a in Atalhos)
        {
            a.Quantidade = semFaixa.Count(v => CarteiraVencendo.NaFaixa(v, a.Faixa));
            a.Visivel = CarteiraVencendo.FaixaUtil(a.Faixa, _consulta?.Dias);
        }
        if (!CarteiraVencendo.FaixaUtil(_faixa, _consulta?.Dias)) // a faixa marcada passou a repetir o período
        {
            _faixa = FaixaVencimento.Todos;
            foreach (var a in Atalhos) a.Selecionado = a.Faixa == _faixa;
        }
        var filtrados = semFaixa.Where(v => CarteiraVencendo.NaFaixa(v, _faixa));
        _filtrados = ColunaOrdenadaChave is null ? [.. filtrados] : CarteiraVencendo.Ordenar(filtrados, ColunaOrdenadaChave, OrdemDecrescente);
        ConteudoLista = GradeDaLista.Montar(_filtrados.Select(v => new LinhaVencendo(v)), aberto: null);
        TituloLista = CarteiraVencendo.TituloLista(_filtrados.Count);
        MostrarAtalhos = semFaixa.Count > 0;
        MostrarExportar = _filtrados.Count > 0;
        AtualizarVazio(todos.Count);
        OnPropertyChanged(nameof(ListaVazia));
        OnPropertyChanged(nameof(Filtrado));
    }

    /// <summary>
    /// Lista vazia em uma frase e uma ação (03/10/2026): com filtro, "Limpar filtro"; sem filtro, o período consultado e
    /// "Ampliar para N dias" (o próximo prazo pronto). Outros períodos: o campo Período.
    /// </summary>
    private void AtualizarVazio(int total)
    {
        if (_consulta is not { } c)
        {
            TituloVazio = "Nenhum vínculo vencendo";
            TextoVazio = string.Empty;
            TextoAcaoVazio = string.Empty;
            return;
        }
        if (total > 0 && (Filtrado || _faixa != FaixaVencimento.Todos))
        {
            TituloVazio = "Nenhum vínculo com este filtro";
            TextoVazio = $"Há {total} vínculo(s) no período sem o filtro.";
            TextoAcaoVazio = "Limpar filtro";
            return;
        }
        TituloVazio = CarteiraVencendo.TituloSemVinculos(c.Dias);
        TextoVazio = $"{TextoTela.Data(c.De)} a {TextoTela.Data(c.Ate)}";
        TextoAcaoVazio = CarteiraVencendo.ProximoPrazo(c.Dias) is { } proximo ? $"Ampliar para {CarteiraVencendo.TextoDias(proximo)}" : string.Empty;
    }

    private string DescreverFiltro() => string.Join(" · ", new[]
    {
        _faixa switch { FaixaVencimento.Hoje => "Terminam hoje", FaixaVencimento.AteSete => "Até 7 dias", FaixaVencimento.AteTrinta => "Até 30 dias", _ => null },
        Papel?.Valor is { } p ? $"Papel: {p}" : null,
        Pessoa?.Valor is { } q ? $"Quem atende: {q}" : null,
        Empresa?.Valor is { } e ? $"Empresa: {e}" : null,
        BuscaCliente.Trim().Length > 0 ? $"Cliente contém \"{BuscaCliente.Trim()}\"" : null,
        _consulta is { } c ? CarteiraVencendo.TextoPeriodo(c.De, c.Ate) : null
    }.Where(x => x is not null));
}

// ======================================================================= Coberturas

/// <summary>Linha da lista de coberturas.</summary>
public sealed class LinhaCobertura
{
    public LinhaCobertura(CoberturaDto c) => Item = c;

    public CoberturaDto Item { get; }
    public Guid Id => Item.Id;
    public string Titular => Item.Titular ?? "?";
    public string Detalhe => $"{Item.TipoAusencia} · {TextosCobertura.Periodo(Item.InicioEm, Item.FimEm)} · por {Item.QuemCobre}";
    public string Situacao => TextosCobertura.Situacao(Item.Situacao);
    public string Clientes => Item.ClientesAfetados == 1 ? "1 cliente" : $"{Item.ClientesAfetados} clientes";
}

/// <summary>
/// Ficha de uma cobertura. Antes de começar, tudo muda (e pode ser cancelada); depois, só fim, observação e acesso
/// (a API confere as mesmas regras: RegrasCobertura).
/// </summary>
public sealed partial class CoberturaEdicao : ObservableObject
{
    public static readonly Opcao<Guid?> Todos = new(null, "Todos os papéis");
    public static readonly Opcao<Guid?> Todas = new(null, "Todas as empresas");
    public static readonly Opcao<Guid?> Nenhum = new(null, "—");
    public static readonly Opcao<bool>[] Modos = [new(false, "Uma pessoa"), new(true, "Uma equipe")];

    private readonly CoberturaDto _gravada;
    private readonly DateOnly _hoje;
    private CoberturaOpcoesDto _opcoes = new();

    private CoberturaEdicao(CoberturaDto d, bool novo, DateOnly hoje)
    {
        _gravada = d;
        _hoje = hoje;
        Novo = novo;
        Id = d.Id;
        _inicioEm = TextoTela.Data(d.InicioEm == default ? null : d.InicioEm);
        _fimEm = TextoTela.Data(d.FimEm == default ? null : d.FimEm);
        _porEquipe = Modos[d.EquipeSubstitutaId is null ? 0 : 1];
        _credito = Opcao.De(TextosCobertura.Creditos, d.RegraCredito);
        _percentualSubstituto = TextoTela.Decimal(d.PercentualSubstituto);
        _permiteAcesso = d.PermiteAcesso;
        _observacao = d.Observacao ?? string.Empty;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao => _gravada.Versao;
    public bool Cancelada => _gravada.Cancelada;

    /// <summary>Gravada e já começou: titular, tipo, início, quem cobre, escopo e crédito ficam travados.</summary>
    public bool Travado => !Novo && (_gravada.Cancelada || _gravada.InicioEm <= _hoje);
    public bool Editavel => !Travado;
    public bool PodeCancelar => !Novo && !_gravada.Cancelada && _gravada.InicioEm > _hoje;
    public bool PodeEncerrarHoje => !Novo && !_gravada.Cancelada && _gravada.InicioEm <= _hoje && _gravada.FimEm >= _hoje;

    public string Titulo => Novo ? "Nova cobertura" : $"Cobertura de {_gravada.Titular}";
    public string SituacaoTexto => Novo ? "Nova cobertura"
        : _gravada.Cancelada ? $"Cancelada: {_gravada.MotivoCancelamento}"
        : $"{TextosCobertura.Situacao(_gravada.Situacao)} · {(_gravada.ClientesAfetados == 1 ? "1 cliente" : $"{_gravada.ClientesAfetados} clientes")} na carteira do titular, no escopo";

    [ObservableProperty] private Opcao<Guid?>[] _pessoas = [Nenhum];
    [ObservableProperty] private Opcao<Guid?> _titular = Nenhum;
    [ObservableProperty] private Opcao<Guid?>[] _tipos = [Nenhum];
    [ObservableProperty] private Opcao<Guid?> _tipo = Nenhum;
    [ObservableProperty] private string _inicioEm;
    [ObservableProperty] private string _fimEm;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(CobrePessoa))] private Opcao<bool> _porEquipe;
    [ObservableProperty] private Opcao<Guid?>[] _substitutos = [Nenhum];
    [ObservableProperty] private Opcao<Guid?> _substituto = Nenhum;
    [ObservableProperty] private Opcao<Guid?>[] _equipes = [Nenhum];
    [ObservableProperty] private Opcao<Guid?> _equipe = Nenhum;
    [ObservableProperty] private Opcao<Guid?>[] _papeis = [Todos];
    [ObservableProperty] private Opcao<Guid?> _papel = Todos;
    [ObservableProperty] private Opcao<Guid?>[] _empresas = [Todas];
    [ObservableProperty] private Opcao<Guid?> _empresa = Todas;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Dividido))] private Opcao<RegraCreditoAusencia> _credito;
    [ObservableProperty] private string _percentualSubstituto;
    [ObservableProperty] private bool _permiteAcesso;
    [ObservableProperty] private string _observacao;

    public IReadOnlyList<Opcao<bool>> ListaModos => Modos;
    public IReadOnlyList<Opcao<RegraCreditoAusencia>> Creditos => TextosCobertura.Creditos;
    public bool CobrePessoa => !PorEquipe.Valor;
    public bool Dividido => Credito.Valor == RegraCreditoAusencia.Dividido;

    public static CoberturaEdicao Criar(DateOnly hoje, ParametrosComerciaisDto parametros) => new(new CoberturaDto
    {
        Id = IdSequencial.Novo(),
        InicioEm = hoje,
        RegraCredito = parametros.CreditoNaAusencia,
        PercentualSubstituto = parametros.PercentualSubstitutoPadrao,
        PermiteAcesso = true
    }, novo: true, hoje);

    public static CoberturaEdicao De(CoberturaDto d, DateOnly hoje) => new(d, novo: false, hoje);

    /// <summary>Monta as listas (ativos, mais o gravado se estiver desativado) e escolhe o que está gravado.</summary>
    public void DefinirOpcoes(CoberturaOpcoesDto opcoes)
    {
        _opcoes = opcoes;
        Pessoas = Lista(opcoes.Pessoas.Select(p => (p.Id, p.Nome)), _gravada.TitularId, _gravada.Titular);
        Titular = OpcoesComercial.Escolher(Pessoas, _gravada.TitularId == Guid.Empty ? null : _gravada.TitularId);
        Tipos = Lista(opcoes.TiposAusencia.Where(t => t.Ativo || t.Id == _gravada.TipoAusenciaId).Select(t => (t.Id, t.Ativo ? t.Nome : t.Nome + " (desativado)")),
            _gravada.TipoAusenciaId, _gravada.TipoAusencia);
        Tipo = OpcoesComercial.Escolher(Tipos, _gravada.TipoAusenciaId == Guid.Empty ? (opcoes.TiposAusencia.FirstOrDefault(t => t.Ativo)?.Id) : _gravada.TipoAusenciaId);
        Papeis = [Todos, .. opcoes.Papeis.Where(p => p.Ativo || p.Id == _gravada.TipoCarteiraId).Select(p => new Opcao<Guid?>(p.Id, p.Ativo ? p.Nome : p.Nome + " (desativado)"))];
        Papel = OpcoesComercial.Escolher(Papeis, _gravada.TipoCarteiraId);
        Empresas = [Todas, .. opcoes.Empresas.Where(e => e.Ativa || e.Id == _gravada.EmpresaId).Select(e => new Opcao<Guid?>(e.Id, e.Nome))];
        Empresa = OpcoesComercial.Escolher(Empresas, _gravada.EmpresaId);
        Equipes = Lista(opcoes.Equipes.Select(e => (e.Id, e.Nome)), _gravada.EquipeSubstitutaId ?? Guid.Empty,
            _gravada.EquipeSubstitutaId is null ? null : _gravada.QuemCobre);
        Equipe = OpcoesComercial.Escolher(Equipes, _gravada.EquipeSubstitutaId);
        MontarSubstitutos(_gravada.SubstitutoId);
    }

    partial void OnPapelChanged(Opcao<Guid?> value) => MontarSubstitutos(Substituto.Valor);

    partial void OnTitularChanged(Opcao<Guid?> value) => MontarSubstitutos(Substituto.Valor);

    /// <summary>Quem pode cobrir: quem pode ocupar o papel da cobertura (ou algum papel, quando vale para todos).</summary>
    private void MontarSubstitutos(Guid? escolhido)
    {
        var aceitas = (Papel.Valor is { } id && _opcoes.Papeis.FirstOrDefault(p => p.Id == id) is { } papel
            ? papel.Classificacoes
            : _opcoes.Papeis.Where(p => p.Ativo).SelectMany(p => p.Classificacoes)).ToHashSet();
        var lista = _opcoes.Pessoas.Where(p => p.Id != Titular.Valor && p.Classificacoes.Any(aceitas.Contains)).Select(p => (p.Id, p.Nome));
        Substitutos = Lista(lista, _gravada.SubstitutoId ?? Guid.Empty, _gravada.SubstitutoId is null ? null : _gravada.QuemCobre);
        Substituto = OpcoesComercial.Escolher(Substitutos, escolhido);
    }

    private static Opcao<Guid?>[] Lista(IEnumerable<(Guid Id, string Nome)> itens, Guid gravado, string? nomeGravado)
    {
        var lista = itens.Select(i => new Opcao<Guid?>(i.Id, i.Nome)).ToList();
        if (gravado != Guid.Empty && lista.All(o => o.Valor != gravado))
            lista.Add(new Opcao<Guid?>(gravado, (nomeGravado ?? "(gravado)") + " (fora da lista)"));
        return [Nenhum, .. lista];
    }

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (Titular.Valor is null) erros.Add("Escolha quem vai se ausentar.");
        if (Tipo.Valor is null) erros.Add("Escolha o tipo de ausência.");
        if (!TextoTela.TentarData(InicioEm, out var inicio) || inicio is null) erros.Add("Início: use dd/mm/aaaa.");
        if (!TextoTela.TentarData(FimEm, out var fim) || fim is null) erros.Add("Fim: use dd/mm/aaaa (obrigatório).");
        if (CobrePessoa && Substituto.Valor is null) erros.Add("Escolha quem cobre.");
        if (!CobrePessoa && Equipe.Valor is null) erros.Add("Escolha a equipe que cobre.");
        if (!TextoTela.TentarDecimal(PercentualSubstituto, out _)) erros.Add("Percentual de quem cobre: número inválido.");
        return erros;
    }

    public CoberturaDto ParaDto()
    {
        TextoTela.TentarData(InicioEm, out var inicio);
        TextoTela.TentarData(FimEm, out var fim);
        TextoTela.TentarDecimal(PercentualSubstituto, out var pct);
        return new CoberturaDto
        {
            Id = Id,
            Versao = _gravada.Versao,
            TitularId = Titular.Valor ?? Guid.Empty,
            TipoAusenciaId = Tipo.Valor ?? Guid.Empty,
            InicioEm = inicio ?? default,
            FimEm = fim ?? default,
            SubstitutoId = CobrePessoa ? Substituto.Valor : null,
            EquipeSubstitutaId = CobrePessoa ? null : Equipe.Valor,
            TipoCarteiraId = Papel.Valor,
            EmpresaId = Empresa.Valor,
            RegraCredito = Credito.Valor,
            PercentualSubstituto = Dividido ? pct : null,
            PermiteAcesso = PermiteAcesso,
            Observacao = TextoTela.Nulo(Observacao)?.Trim(),
            Cancelada = _gravada.Cancelada
        };
    }
}

/// <summary>
/// Ausências e coberturas (Motor Comercial, Fase 1c): quem cobre a carteira de quem sai de férias, licença... A carteira
/// não muda; a cobertura vale pelas datas e acaba sozinha. Nada é apagado: cancela (antes de começar) ou encerra.
/// </summary>
public sealed partial class CoberturasViewModel : CadastroViewModelBase<LinhaCobertura>
{
    private readonly ComercialApi _api;
    private CoberturaOpcoesDto _opcoes = new();

    private readonly SessaoCliente _sessao;

    public CoberturasViewModel(ComercialApi api, SessaoCliente sessao, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
        _sessao = sessao;
    }

    /// <summary>Quem só vê o Comercial não cria cobertura (Comercial.Coberturas).</summary>
    public override bool PodeCriar => _sessao.Possui(Permissoes.Comercial.Coberturas);

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty] private CoberturaEdicao? _formulario;

    /// <summary>Mostra também as encerradas e as canceladas (histórico).</summary>
    [ObservableProperty] private bool _incluirEncerradas;

    partial void OnIncluirEncerradasChanged(bool value) => _ = RecarregarAsync();

    protected override string TextoDeBusca(LinhaCobertura item) => item.Titular + " " + item.Detalhe;

    /// <summary>Lista em colunas (padrão de tela de cadastro, 03/10/2026; tela-piloto).</summary>
    protected override GradeCadastro<LinhaCobertura> CriarGradeDaLista() => new(
        "Quem se ausenta", l => l.Id, l => l.Titular, l => l.Item.TipoAusencia,
        ColunaCadastro<LinhaCobertura>.Curto("periodo", "Período", l => TextosCobertura.Periodo(l.Item.InicioEm, l.Item.FimEm), 210),
        ColunaCadastro<LinhaCobertura>.Texto("quemCobre", "Quem cobre", l => l.Item.QuemCobre),
        ColunaCadastro<LinhaCobertura>.Curto("clientes", "Clientes", l => l.Clientes, 110),
        ColunaCadastro<LinhaCobertura>.Selo("situacao", "Situação", l => l.Situacao, l => l.Item.Situacao switch
        {
            SituacaoCobertura.Vigente => "Sucesso",
            SituacaoCobertura.Agendada => "Informacao",
            _ => "Neutro"
        }));

    protected override async Task AntesDeListarAsync() => _opcoes = await _api.ListarOpcoesCoberturaAsync();

    protected override async Task<IReadOnlyList<LinhaCobertura>> ListarAsync() =>
        [.. (await _api.ListarCoberturasAsync(IncluirEncerradas)).Select(c => new LinhaCobertura(c))];

    private CoberturaEdicao Preparar(CoberturaEdicao f)
    {
        f.DefinirOpcoes(_opcoes);
        return f;
    }

    protected override async Task AbrirAsync(LinhaCobertura item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(CoberturaEdicao.Criar(Hoje, _opcoes.Parametros));
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override object? FichaObservada => Formulario;
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        Formulario = await ObterAsync(formulario.Id);
    }

    private async Task<CoberturaEdicao> ObterAsync(Guid id) =>
        Preparar(CoberturaEdicao.De(await _api.ObterCoberturaAsync(id) ?? throw new ValidacaoException(["Esta cobertura não existe mais."]), Hoje));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }
        await GravarAsync(formulario.ParaDto(), formulario.Novo ? "Cobertura cadastrada." : "Alterações salvas.");
    }

    /// <summary>Encerra hoje a cobertura que já começou (o fim passa a ser hoje).</summary>
    [RelayCommand]
    private async Task EncerrarHojeAsync()
    {
        if (Formulario is not { PodeEncerrarHoje: true } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de encerrar.", TipoMensagem.Aviso);
            return;
        }
        if (!await ConfirmarAsync("Encerrar cobertura", $"A cobertura passa a terminar hoje ({TextoTela.Data(Hoje)}). O período em que valeu fica no histórico.",
                "Encerrar hoje", "Voltar"))
            return;
        var dto = formulario.ParaDto();
        dto.FimEm = Hoje;
        await GravarAsync(dto, "Cobertura encerrada hoje.");
    }

    /// <summary>Cancela a cobertura que ainda não começou (pede o motivo; fica no histórico).</summary>
    [RelayCommand]
    private async Task CancelarCoberturaAsync()
    {
        if (Formulario is not { PodeCancelar: true } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de cancelar.", TipoMensagem.Aviso);
            return;
        }
        var motivo = await PerguntarAsync("Cancelar cobertura", "Por que a cobertura foi cancelada? (fica no histórico)", "Cancelar cobertura", "Voltar",
            dica: "Ex.: férias adiadas", tamanhoMaximo: 250);
        if (string.IsNullOrWhiteSpace(motivo)) return;

        CoberturaDto? gravada = null;
        if (!await ExecutarAsync(async () => gravada = await _api.CancelarCoberturaAsync(formulario.Id, formulario.Versao, motivo))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(CoberturaEdicao.De(gravada!, Hoje));
        MarcarFichaSemAlteracoes();
        Mostrar("Cobertura cancelada.", TipoMensagem.Sucesso);
    }

    private async Task GravarAsync(CoberturaDto dto, string sucesso)
    {
        CoberturaDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarCoberturaAsync(dto))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(CoberturaEdicao.De(salvo!, Hoje));
        MarcarFichaSemAlteracoes();
        Mostrar(sucesso, TipoMensagem.Sucesso);
    }
}
