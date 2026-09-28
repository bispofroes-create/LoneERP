using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Municipios;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>Uma escolha de uma lista do filtro (papel, UF, regime...), com caixa de marcar.</summary>
public sealed partial class OpcaoMarcavel : ObservableObject
{
    private readonly Action _aoMudar;

    public OpcaoMarcavel(string valor, string texto, Action aoMudar, bool marcado = false)
    {
        Valor = valor;
        Texto = texto;
        _aoMudar = aoMudar;
        _marcado = marcado;
    }

    public string Valor { get; }
    public string Texto { get; }

    [ObservableProperty] private bool _marcado;

    partial void OnMarcadoChanged(bool value) => _aoMudar();
}

/// <summary>
/// Um campo do painel de filtros: marcado, vira uma condição (e um chip). O editor depende do tipo do campo: lista com
/// caixas de marcar, texto, número (dias), período de datas ou sim/não. Campo incompleto (ex.: lista sem nada marcado)
/// não filtra até ser completado.
/// </summary>
public sealed partial class CampoFiltroItem : ObservableObject
{
    private readonly Action _aoMudar;
    private bool _montando = true;

    public CampoFiltroItem(CampoFiltroDto definicao, Action aoMudar, Func<string, Task<IReadOnlyList<MunicipioDto>>>? fonteMunicipios)
    {
        Definicao = definicao;
        _aoMudar = aoMudar;
        Operadores = [.. definicao.Operadores.Where(o => o is not (OperadorFiltro.Sim or OperadorFiltro.Nao))
            .Select(o => new Opcao<OperadorFiltro>(o, NomeOperador(o)))];
        _operador = Operadores.Count > 0 ? Operadores[0] : new Opcao<OperadorFiltro>(OperadorFiltro.Sim, "Sim");
        foreach (var o in definicao.Opcoes) Opcoes.Add(new OpcaoMarcavel(o.Valor, o.Texto, Mudou));

        if (definicao.OpcoesSobDemanda == "municipios")
        {
            Municipio = new SeletorMunicipio { Fonte = fonteMunicipios };
            Municipio.PropertyChanged += MunicipioEscolhido;
        }
        _montando = false;
    }

    public CampoFiltroDto Definicao { get; }
    public string Id => Definicao.Id;
    public string Nome => Definicao.Nome;
    public string? Dica => Definicao.Dica;
    public bool TemDica => !string.IsNullOrWhiteSpace(Definicao.Dica);

    public bool EhLista => Definicao.Tipo == TipoCampoFiltro.Lista;
    public bool EhTexto => Definicao.Tipo == TipoCampoFiltro.Texto && OperadorPedeValor;
    public bool EhNumero => Definicao.Tipo == TipoCampoFiltro.Numero;
    public bool EhData => Definicao.Tipo == TipoCampoFiltro.Data;
    public bool EhSimNao => Definicao.Tipo == TipoCampoFiltro.SimNao;
    public bool EhMunicipio => Municipio is not null;
    public bool TemVariosOperadores => Operadores.Count > 1;

    /// <summary>"Só PF"/"Só PJ" (vazio = vale para todas).</summary>
    public string TextoValePara => Definicao.ValePara switch
    {
        ValeParaNatureza.Fisica => "só PF",
        ValeParaNatureza.Juridica => "só PJ",
        _ => string.Empty
    };

    public bool TemValePara => TextoValePara.Length > 0;
    public string AvisoTextoLivre => "Busca em texto livre: pode demorar em bases grandes.";

    // ---- Estado ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarEditor))]
    private bool _marcado;

    /// <summary>Não se aplica à natureza escolhida no atalho (ex.: campo só de PJ com o atalho "Pessoas físicas").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Habilitado))]
    private bool _foraDaNatureza;

    public bool Habilitado => !ForaDaNatureza;

    /// <summary>Some da lista quando a busca de campos não o encontra.</summary>
    [ObservableProperty] private bool _visivel = true;

    public bool MostrarEditor => Marcado;

    public IReadOnlyList<Opcao<OperadorFiltro>> Operadores { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhTexto), nameof(MostrarSegundoValor), nameof(RotuloPrimeiroValor))]
    private Opcao<OperadorFiltro> _operador;

    public ObservableCollection<OpcaoMarcavel> Opcoes { get; } = new();

    /// <summary>Valor digitado (texto, número ou data dd/mm/aaaa, conforme o tipo do campo) e o fim do "Entre".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValorTexto), nameof(NumeroInicial), nameof(DataInicial))]
    private string _valor = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NumeroFinal), nameof(DataFinal))]
    private string _valorFinal = string.Empty;

    // Cada editor da tela tem a sua propriedade, que só lê e grava quando o campo é daquele tipo. Os editores dos outros
    // tipos ficam escondidos, mas continuam ligados: o de data aplica a máscara dd/mm/aaaa e, ligado direto em "Valor",
    // apagava o texto digitado no bairro, no CEP etc.
    public string ValorTexto
    {
        get => Definicao.Tipo == TipoCampoFiltro.Texto ? Valor : string.Empty;
        set { if (Definicao.Tipo == TipoCampoFiltro.Texto) Valor = value ?? string.Empty; }
    }

    public string NumeroInicial
    {
        get => Definicao.Tipo == TipoCampoFiltro.Numero ? Valor : string.Empty;
        set { if (Definicao.Tipo == TipoCampoFiltro.Numero) Valor = value ?? string.Empty; }
    }

    public string NumeroFinal
    {
        get => Definicao.Tipo == TipoCampoFiltro.Numero ? ValorFinal : string.Empty;
        set { if (Definicao.Tipo == TipoCampoFiltro.Numero) ValorFinal = value ?? string.Empty; }
    }

    public string DataInicial
    {
        get => Definicao.Tipo == TipoCampoFiltro.Data ? Valor : string.Empty;
        set { if (Definicao.Tipo == TipoCampoFiltro.Data) Valor = value ?? string.Empty; }
    }

    public string DataFinal
    {
        get => Definicao.Tipo == TipoCampoFiltro.Data ? ValorFinal : string.Empty;
        set { if (Definicao.Tipo == TipoCampoFiltro.Data) ValorFinal = value ?? string.Empty; }
    }

    /// <summary>Sim/não: verdadeiro = "Sim" (o padrão ao marcar o campo).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoSimNao))]
    private bool _sim = true;

    public string TextoSimNao => Sim ? "Sim" : "Não";

    /// <summary>Município: busca por UF + nome; cada escolhido entra na lista de opções marcadas.</summary>
    public SeletorMunicipio? Municipio { get; }

    public bool MostrarSegundoValor => Operador.Valor == OperadorFiltro.Entre;
    public string RotuloPrimeiroValor => Operador.Valor == OperadorFiltro.Entre ? "De" : "Valor";

    private bool OperadorPedeValor => Operador.Valor is not (OperadorFiltro.Vazio or OperadorFiltro.NaoVazio);

    partial void OnMarcadoChanged(bool value) => Mudou();
    partial void OnOperadorChanged(Opcao<OperadorFiltro> value) => Mudou();
    partial void OnValorChanged(string value) => Mudou();
    partial void OnValorFinalChanged(string value) => Mudou();
    partial void OnSimChanged(bool value) => Mudou();

    [RelayCommand]
    private void AlternarSimNao() => Sim = !Sim;

    [RelayCommand]
    private void Desmarcar() => Marcado = false;

    private void Mudou()
    {
        if (!_montando) _aoMudar();
    }

    private void MunicipioEscolhido(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SeletorMunicipio.Selecionado) || Municipio?.Selecionado is not { } m) return;
        var valor = m.Id.ToString(CultureInfo.InvariantCulture);
        var existente = Opcoes.FirstOrDefault(o => o.Valor == valor);
        // Opção nova já nasce marcada (o construtor não avisa a mudança): avisa aqui, para o filtro valer na hora.
        if (existente is null)
        {
            Opcoes.Add(new OpcaoMarcavel(valor, m.NomeComUf, Mudou, marcado: true));
            Mudou();
        }
        else existente.Marcado = true;
        // Não limpa o seletor aqui: este aviso chega no meio da escolha (o seletor ainda vai gravar o texto), e mexer
        // nele agora reabria a busca, que escolhia de novo, sem fim (StackOverflow). Para outro município, basta digitar.
    }

    /// <summary>Inclui uma opção já marcada (município de uma visão salva) e avisa a mudança.</summary>
    public void IncluirOpcaoMarcada(string valor, string texto)
    {
        Opcoes.Add(new OpcaoMarcavel(valor, texto, Mudou, marcado: true));
        Mudou();
    }

    /// <summary>Volta ao estado inicial (desmarcado, sem valores).</summary>
    public void Limpar()
    {
        _montando = true;
        try
        {
            Marcado = false;
            Operador = Operadores.Count > 0 ? Operadores[0] : Operador;
            Valor = string.Empty;
            ValorFinal = string.Empty;
            Sim = true;
            foreach (var o in Opcoes) o.Marcado = false;
            if (EhMunicipio) Opcoes.Clear();
        }
        finally { _montando = false; }
    }

    // ---- Para a API e para o chip ----

    /// <summary>A condição do campo, ou nulo se não estiver marcado, estiver fora da natureza ou incompleto.</summary>
    public CondicaoFiltro? ParaCondicao()
    {
        if (!Marcado || ForaDaNatureza) return null;
        var condicao = new CondicaoFiltro { Campo = Id, Operador = Operador.Valor };
        switch (Definicao.Tipo)
        {
            case TipoCampoFiltro.SimNao:
                condicao.Operador = Sim ? OperadorFiltro.Sim : OperadorFiltro.Nao;
                return condicao;

            case TipoCampoFiltro.Lista:
                condicao.Valores = [.. Opcoes.Where(o => o.Marcado).Select(o => o.Valor)];
                return condicao.Valores.Count > 0 ? condicao : null;

            case TipoCampoFiltro.Data:
                if (!TextoTela.TentarData(Valor, out var de) || de is null) return null;
                condicao.Valores = [Dia(de.Value)];
                if (MostrarSegundoValor)
                {
                    if (!TextoTela.TentarData(ValorFinal, out var ate) || ate is null) return null;
                    condicao.Valores.Add(Dia(ate.Value));
                }
                return condicao;

            case TipoCampoFiltro.Numero:
                var numeros = (MostrarSegundoValor ? new[] { Valor, ValorFinal } : new[] { Valor }).Select(Numero).ToList();
                if (numeros.Any(n => n is null)) return null;
                condicao.Valores = [.. numeros.Select(n => n!)];
                return condicao;

            default:
                if (!OperadorPedeValor) return condicao;
                // Mesmas regras do servidor (vindas do catálogo): enquanto o texto não serve, o campo fica incompleto e
                // não filtra — nada de erro na tela no meio da digitação.
                var texto = Definicao.SomenteDigitos ? new string(Valor.Where(char.IsAsciiDigit).ToArray()) : Valor.Trim();
                if (texto.Length == 0) return null;
                if (Definicao.TamanhoMinimo is { } minimo && texto.Length < minimo) return null;
                if (Definicao.TamanhoMaximo is { } maximo && texto.Length > maximo) return null;
                condicao.Valores = [texto];
                return condicao;
        }
    }

    /// <summary>Texto do chip: "UF: MG, SP", "Documentos vencidos", "Cadastrado em: 01/01/2026 a 30/06/2026".</summary>
    public string? TextoChip()
    {
        if (ParaCondicao() is not { } c) return null;
        string Valores() => string.Join(", ", Opcoes.Where(o => o.Marcado).Select(o => o.Texto));
        return c.Operador switch
        {
            OperadorFiltro.Sim => Nome,
            OperadorFiltro.Nao => $"{Nome}: não",
            OperadorFiltro.UmDestes => $"{Nome}: {Valores()}",
            OperadorFiltro.TodosDestes => $"{Nome}: todos de {Valores()}",
            OperadorFiltro.NenhumDestes => $"{Nome}: nenhum de {Valores()}",
            OperadorFiltro.Entre => $"{Nome}: {Valor} a {ValorFinal}",
            OperadorFiltro.Vazio or OperadorFiltro.NaoVazio => $"{Nome}: {NomeOperador(c.Operador).ToLowerInvariant()}",
            _ => $"{Nome}: {NomeOperador(c.Operador).ToLowerInvariant()} {Valor.Trim()}"
        };
    }

    private static string Dia(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Número digitado em português ("1.500,50") para o formato da API ("1500.50"); nulo se inválido.</summary>
    private static string? Numero(string texto) =>
        decimal.TryParse(texto, NumberStyles.Number, TextoTela.Brasil, out var n) ? n.ToString(CultureInfo.InvariantCulture) : null;

    public static string NomeOperador(OperadorFiltro o) => o switch
    {
        OperadorFiltro.Contem => "Contém",
        OperadorFiltro.ComecaCom => "Começa com",
        OperadorFiltro.Igual => "Igual a",
        OperadorFiltro.Vazio => "Não preenchido",
        OperadorFiltro.NaoVazio => "Preenchido",
        OperadorFiltro.UmDestes => "É um destes",
        OperadorFiltro.TodosDestes => "Tem todos estes",
        OperadorFiltro.NenhumDestes => "Não é nenhum destes",
        OperadorFiltro.Entre => "Entre",
        OperadorFiltro.APartirDe => "A partir de",
        OperadorFiltro.Ate => "Até",
        OperadorFiltro.Maior => "Maior que",
        OperadorFiltro.Menor => "Menor que",
        OperadorFiltro.Sim => "Sim",
        OperadorFiltro.Nao => "Não",
        OperadorFiltro.EmAteDias => "Nos próximos (dias)",
        OperadorFiltro.HaMaisDeDias => "Há mais de (dias)",
        _ => o.ToString()
    };
}

/// <summary>Um grupo do painel (os mesmos da ficha: Identificação, Endereços, Fiscal...), que abre e fecha.</summary>
public sealed partial class GrupoFiltroItem : ObservableObject
{
    public GrupoFiltroItem(string nome, IReadOnlyList<CampoFiltroItem> campos)
    {
        Nome = nome;
        Campos = campos;
    }

    public string Nome { get; }
    public IReadOnlyList<CampoFiltroItem> Campos { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Seta))]
    private bool _expandido;

    [ObservableProperty] private bool _visivel = true;

    public string Seta => Expandido ? "▾" : "▸";

    /// <summary>"Endereços (2)": quantos campos deste grupo estão filtrando.</summary>
    public string Titulo => Campos.Count(c => c.Marcado) is var n and > 0 ? $"{Nome} ({n})" : Nome;

    public void AtualizarTitulo() => OnPropertyChanged(nameof(Titulo));

    /// <summary>Avisa o painel (o texto "Abrir todos"/"Fechar todos" depende de todos os grupos).</summary>
    public Action? AoAlternar { get; set; }

    [RelayCommand]
    private void Alternar()
    {
        Expandido = !Expandido;
        AoAlternar?.Invoke();
    }
}

/// <summary>Filtro ativo mostrado acima da lista. Tocar abre o campo no painel; o ✕ tira o filtro.</summary>
public sealed class ChipFiltro
{
    public ChipFiltro(string texto, Action abrir, Action remover)
    {
        Texto = texto;
        AbrirCommand = new RelayCommand(abrir);
        RemoverCommand = new RelayCommand(remover);
    }

    public string Texto { get; }
    public IRelayCommand AbrirCommand { get; }
    public IRelayCommand RemoverCommand { get; }
}

/// <summary>
/// Painel de filtros da tela de Pessoas, montado a partir do catálogo que a API manda (só os campos que o usuário pode
/// usar). Marcar um campo e completar o valor gera uma condição e um chip; a tela relê a lista (com uma pequena espera)
/// sempre que algo muda. Entre campos, "E"; dentro de uma lista, "um destes" = "OU".
/// </summary>
public sealed partial class PainelFiltrosPessoas : ObservableObject
{
    /// <summary>Chamado quando as condições mudam (a tela relê a lista).</summary>
    public Action? Mudou { get; set; }

    /// <summary>Busca de municípios (definida pela tela).</summary>
    public Func<string, Task<IReadOnlyList<MunicipioDto>>>? FonteMunicipios { get; set; }

    public ObservableCollection<GrupoFiltroItem> Grupos { get; } = new();
    public ObservableCollection<ChipFiltro> Chips { get; } = new();

    [ObservableProperty] private bool _aberto;

    [ObservableProperty] private string _buscaCampo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso))]
    private string _aviso = string.Empty;

    public bool TemAviso => Aviso.Length > 0;
    public bool TemChips => Chips.Count > 0;
    public bool Carregado { get; private set; }

    private IEnumerable<CampoFiltroItem> Campos => Grupos.SelectMany(g => g.Campos);

    /// <summary>"Filtros" ou "Filtros (3)".</summary>
    public string TextoBotao => Chips.Count == 0 ? "Filtros" : $"Filtros ({Chips.Count})";

    private NaturezaPessoa? _natureza;
    private string _ultimaAssinatura = "[]"; // sem condições (não relê a lista ao montar o painel)

    /// <summary>Monta os grupos a partir do catálogo (mantém o que já estava marcado, pelo Id do campo).</summary>
    public void Carregar(CatalogoFiltrosPessoasDto catalogo)
    {
        var anteriores = Campos.Where(c => c.Marcado).Select(c => c.ParaCondicao()).OfType<CondicaoFiltro>().ToList();
        Grupos.Clear();
        foreach (var grupo in catalogo.Campos.GroupBy(c => c.Grupo))
            Grupos.Add(new GrupoFiltroItem(grupo.Key,
                [.. grupo.Select(d => new CampoFiltroItem(d, AlgoMudou, FonteMunicipios))])
            {
                AoAlternar = () => OnPropertyChanged(nameof(TextoExpandirTodos))
            });
        Carregado = true;
        Aviso = string.Empty;
        foreach (var condicao in anteriores) Aplicar(condicao);
        AplicarNatureza();
        FiltrarCampos();
        AlgoMudou();
    }

    public void FalhouAoCarregar(string mensagem) => Aviso = mensagem;

    /// <summary>Natureza do atalho (PF/PJ): campos da outra natureza ficam desabilitados e não filtram.</summary>
    public void DefinirNatureza(NaturezaPessoa? natureza)
    {
        _natureza = natureza;
        AplicarNatureza();
        AlgoMudou();
    }

    private void AplicarNatureza()
    {
        foreach (var c in Campos)
            c.ForaDaNatureza = _natureza switch
            {
                NaturezaPessoa.Fisica => c.Definicao.ValePara == ValeParaNatureza.Juridica,
                NaturezaPessoa.Juridica => c.Definicao.ValePara == ValeParaNatureza.Fisica,
                _ => false
            };
    }

    partial void OnBuscaCampoChanged(string value) => FiltrarCampos();

    /// <summary>Busca de campos: mostra só os que batem (sem acento/maiúsculas) e abre os grupos que têm algum.</summary>
    private void FiltrarCampos()
    {
        var termo = TextoBusca.Normalizar(BuscaCampo);
        foreach (var g in Grupos)
        {
            foreach (var c in g.Campos)
                c.Visivel = termo.Length == 0 || TextoBusca.Normalizar(c.Nome + " " + g.Nome).Contains(termo, StringComparison.Ordinal);
            g.Visivel = g.Campos.Any(c => c.Visivel);
            if (termo.Length > 0) g.Expandido = g.Visivel;
        }
        OnPropertyChanged(nameof(TextoExpandirTodos));
    }

    /// <summary>Condições completas dos campos marcados (as que vão para a API).</summary>
    public List<CondicaoFiltro> Condicoes() => Campos.Select(c => c.ParaCondicao()).OfType<CondicaoFiltro>().ToList();

    public bool TemCondicao(string campo) => Condicoes().Any(c => c.Campo == campo);

    /// <summary>O campo do painel com este Id (a linha de filtro das colunas escreve nele); nulo = o usuário não tem o campo.</summary>
    public CampoFiltroItem? CampoPorId(string id) => Campos.FirstOrDefault(c => c.Id == id);

    [RelayCommand]
    private void Alternar()
    {
        if (Aberto) Fechar();
        else Aberto = true;
    }

    /// <summary>Fechar arruma o painel para a próxima vez: só ficam abertos os grupos que estão filtrando.</summary>
    [RelayCommand]
    private void Fechar()
    {
        Aberto = false;
        BuscaCampo = string.Empty;
        foreach (var g in Grupos) g.Expandido = g.Campos.Any(c => c.Marcado);
        OnPropertyChanged(nameof(TextoExpandirTodos));
    }

    /// <summary>Limpar tira todos os filtros e fecha todos os grupos.</summary>
    [RelayCommand]
    public void Limpar()
    {
        foreach (var c in Campos) c.Limpar();
        BuscaCampo = string.Empty;
        foreach (var g in Grupos) g.Expandido = false;
        OnPropertyChanged(nameof(TextoExpandirTodos));
        AlgoMudou();
    }

    /// <summary>"Abrir todos" quando algum grupo está fechado; "Fechar todos" quando estão todos abertos.</summary>
    public string TextoExpandirTodos => Grupos.Count > 0 && Grupos.All(g => g.Expandido) ? "Fechar todos" : "Abrir todos";

    [RelayCommand]
    private void ExpandirTodos()
    {
        var abrir = !Grupos.All(g => g.Expandido);
        foreach (var g in Grupos) g.Expandido = abrir;
        OnPropertyChanged(nameof(TextoExpandirTodos));
    }

    /// <summary>Marca e preenche o campo de uma condição (ex.: filtro já aplicado antes de recarregar o catálogo).</summary>
    /// <returns>Falso se não deu para aplicar tudo (campo que o usuário não pode usar, opção que não existe mais).</returns>
    public bool Aplicar(CondicaoFiltro condicao)
    {
        if (Campos.FirstOrDefault(c => c.Id == condicao.Campo) is not { } campo) return false;
        campo.Marcado = true;
        if (campo.EhSimNao)
        {
            campo.Sim = condicao.Operador != OperadorFiltro.Nao;
            return true;
        }
        campo.Operador = campo.Operadores.FirstOrDefault(o => o.Valor == condicao.Operador) ?? campo.Operador;
        if (campo.EhLista)
        {
            // Município: as opções vêm da busca; o salvo só tem o código IBGE.
            if (campo.EhMunicipio)
                foreach (var valor in condicao.Valores.Where(v => campo.Opcoes.All(o => o.Valor != v)))
                    campo.IncluirOpcaoMarcada(valor, $"Município (IBGE {valor})");
            foreach (var o in campo.Opcoes) o.Marcado = condicao.Valores.Contains(o.Valor);
            return condicao.Valores.All(v => campo.Opcoes.Any(o => o.Valor == v));
        }
        if (campo.EhData)
        {
            campo.Valor = TextoDia(condicao.Valores.ElementAtOrDefault(0));
            campo.ValorFinal = TextoDia(condicao.Valores.ElementAtOrDefault(1));
            return true;
        }
        if (campo.EhNumero)
        {
            // Na condição o número vem com ponto ("1500.5"); o campo da tela é em português ("1500,5").
            campo.Valor = NumeroNaTela(condicao.Valores.ElementAtOrDefault(0));
            campo.ValorFinal = NumeroNaTela(condicao.Valores.ElementAtOrDefault(1));
            return true;
        }
        campo.Valor = condicao.Valores.ElementAtOrDefault(0) ?? string.Empty;
        campo.ValorFinal = condicao.Valores.ElementAtOrDefault(1) ?? string.Empty;
        return true;
    }

    private static string NumeroNaTela(string? valor) =>
        decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n.ToString(TextoTela.Brasil) : string.Empty;

    private static string TextoDia(string? iso) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? TextoTela.Data(d) : string.Empty;

    /// <summary>Algo mudou num campo: refaz os chips e, se as condições mudaram de fato, avisa a tela.</summary>
    private void AlgoMudou()
    {
        Chips.Clear();
        foreach (var g in Grupos)
        {
            g.AtualizarTitulo();
            foreach (var c in g.Campos)
                if (c.TextoChip() is { } texto)
                    Chips.Add(new ChipFiltro(texto, () => AbrirCampo(g), () => { c.Limpar(); AlgoMudou(); }));
        }
        OnPropertyChanged(nameof(TemChips));
        OnPropertyChanged(nameof(TextoBotao));

        // Digitar num campo incompleto (ou marcar sem valor) não relê a lista: só quando as condições mudam.
        var assinatura = System.Text.Json.JsonSerializer.Serialize(Condicoes());
        if (assinatura == _ultimaAssinatura) return;
        _ultimaAssinatura = assinatura;
        Mudou?.Invoke();
    }

    private void AbrirCampo(GrupoFiltroItem grupo)
    {
        Aberto = true;
        BuscaCampo = string.Empty;
        grupo.Expandido = true;
        OnPropertyChanged(nameof(TextoExpandirTodos));
    }
}
