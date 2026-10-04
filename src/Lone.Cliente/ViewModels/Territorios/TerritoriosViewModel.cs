using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.Sessao;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Seguranca;
using Lone.Contracts.Territorios;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Cliente.Grade;

namespace Lone.Cliente.ViewModels.Territorios;

/// <summary>
/// Uma linha da árvore: o território com o recuo do nível. A lista é a árvore "achatada" em ordem (pai, depois os filhos
/// em ordem alfabética), então a busca continua funcionando linha a linha e o caminho mostra onde cada um está.
/// </summary>
public sealed class LinhaTerritorio
{
    public const double RecuoPorNivel = 18;

    public LinhaTerritorio(TerritorioResumoDto item, int nivel, string? caminho, int abaixo)
    {
        Item = item;
        Nivel = nivel;
        Caminho = caminho;
        Abaixo = abaixo;
    }

    public TerritorioResumoDto Item { get; }
    public int Nivel { get; }

    /// <summary>"Brasil › Sudeste" (os de cima); nulo no primeiro nível.</summary>
    public string? Caminho { get; }

    /// <summary>Quantos territórios ativos há logo abaixo.</summary>
    public int Abaixo { get; }

    public double Recuo => (Nivel - 1) * RecuoPorNivel;
    public string Marcador => Abaixo > 0 ? "▾" : "•";
    public string Nome => Item.Nome;
    public bool Encerrado => Item.Situacao == SituacaoTerritorio.Encerrado;
    public string Detalhe => string.Join("  ·  ", new[]
    {
        Item.Codigo, Item.Tipo, Encerrado ? $"encerrado em {TextoTela.Data(Item.FimEm)}" : null, Item.ResponsaveisHoje
    }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

/// <summary>Monta a árvore achatada e as opções de "território acima" (sem a própria e sem as de baixo: evita ciclo).</summary>
public static class ArvoreTerritorios
{
    public static readonly Opcao<Guid?> PrimeiroNivel = new(null, "— (primeiro nível do mapa)");

    public static List<LinhaTerritorio> Linhas(IReadOnlyList<TerritorioResumoDto> todos, bool incluirEncerrados)
    {
        var visiveis = todos.Where(t => incluirEncerrados || t.Situacao == SituacaoTerritorio.Ativo).ToList();
        var ids = visiveis.Select(t => t.Id).ToHashSet();
        var filhos = visiveis.Where(t => t.PaiId is { } p && ids.Contains(p)).ToLookup(t => t.PaiId!.Value);
        var linhas = new List<LinhaTerritorio>();
        var vistos = new HashSet<Guid>();

        void Visitar(TerritorioResumoDto t, int nivel, string? caminho)
        {
            if (!vistos.Add(t.Id)) return; // resiste a dado circular vindo de fora
            var abaixo = filhos[t.Id].Count(f => f.Situacao == SituacaoTerritorio.Ativo);
            linhas.Add(new LinhaTerritorio(t, nivel, caminho, abaixo));
            var caminhoFilhos = caminho is null ? t.Nome : $"{caminho} › {t.Nome}";
            foreach (var f in Ordenar(filhos[t.Id])) Visitar(f, nivel + 1, caminhoFilhos);
        }

        foreach (var raiz in Ordenar(visiveis.Where(t => t.PaiId is not { } p || !ids.Contains(p)))) Visitar(raiz, 1, null);
        return linhas;
    }

    private static IEnumerable<TerritorioResumoDto> Ordenar(IEnumerable<TerritorioResumoDto> itens) =>
        itens.OrderBy(t => t.Situacao).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>Os territórios abaixo de <paramref name="id"/> (filhos, netos...). Resiste a ciclo gravado.</summary>
    public static HashSet<Guid> Abaixo(Guid id, IReadOnlyList<TerritorioResumoDto> todos)
    {
        var filhos = todos.Where(t => t.PaiId is not null).ToLookup(t => t.PaiId!.Value, t => t.Id);
        var resultado = new HashSet<Guid>();
        var fila = new Queue<Guid>();
        fila.Enqueue(id);
        while (fila.Count > 0)
            foreach (var filho in filhos[fila.Dequeue()])
                if (filho != id && resultado.Add(filho)) fila.Enqueue(filho);
        return resultado;
    }

    /// <summary>
    /// Territórios que podem ficar acima: os ativos do mapa, menos o próprio e os de baixo; o atual fica na lista mesmo
    /// encerrado (só para mostrar). O servidor confere de novo.
    /// </summary>
    public static Opcao<Guid?>[] PaisPossiveis(Guid id, IReadOnlyList<TerritorioResumoDto> todos, Guid? atual)
    {
        var abaixo = Abaixo(id, todos);
        var caminhos = Linhas(todos, incluirEncerrados: true).ToDictionary(l => l.Item.Id, l => l.Caminho);
        return
        [
            PrimeiroNivel,
            .. todos.Where(t => t.Id != id && !abaixo.Contains(t.Id) && (t.Situacao == SituacaoTerritorio.Ativo || t.Id == atual))
                .Select(t => (t, Texto: caminhos.GetValueOrDefault(t.Id) is { } c ? $"{c} › {t.Nome}" : t.Nome))
                .OrderBy(x => x.Texto, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new Opcao<Guid?>(x.t.Id, x.t.Situacao == SituacaoTerritorio.Ativo ? x.Texto : x.Texto + " (encerrado)"))
        ];
    }

    /// <summary>O território ou algum abaixo dele tem uso operacional (mover só por operação territorial).</summary>
    public static bool UsoNaSubarvore(Guid id, IReadOnlyList<TerritorioResumoDto> todos) =>
        todos.Any(t => t.Id == id && t.ComUso) || Abaixo(id, todos).Any(a => todos.Any(t => t.Id == a && t.ComUso));
}

/// <summary>
/// Responsável pelo território: pessoa ou equipe, numa função (papel comercial), com início e fim. Quem já começou não
/// muda de pessoa, equipe, função nem início (só o fim); quem ainda não começou pode ser corrigido ou removido (anulado).
/// </summary>
public sealed partial class ResponsavelTerritorioFormulario : ItemDeLista
{
    public static readonly Opcao<bool>[] TiposQuem = [new(false, "Pessoa"), new(true, "Equipe")];
    private static readonly Opcao<Guid?> Nenhum = new(null, "—");

    private readonly TerritorioResponsavelDto _gravado;
    private readonly TerritoriosOpcoesDto _opcoes;

    private ResponsavelTerritorioFormulario(TerritorioResponsavelDto d, bool gravado, TerritoriosOpcoesDto opcoes)
    {
        _gravado = d;
        _opcoes = opcoes;
        Gravado = gravado;
        Id = d.Id;
        _funcoes = [.. opcoes.Funcoes.Where(f => f.Ativo || f.Id == d.TipoCarteiraId)
            .Select(f => new Opcao<Guid>(f.Id, f.Ativo ? f.Nome : f.Nome + " (desativada)"))];
        _funcao = _funcoes.FirstOrDefault(f => f.Valor == d.TipoCarteiraId) ?? _funcoes.FirstOrDefault() ?? new Opcao<Guid>(Guid.Empty, "—");
        _tipoQuem = d.EquipeId is not null ? TiposQuem[1] : TiposQuem[0];
        _equipes = [Nenhum, .. opcoes.Equipes.Select(e => new Opcao<Guid?>(e.Id, e.Nome))];
        if (d.EquipeId is { } eq && _equipes.All(o => o.Valor != eq)) _equipes = [.. _equipes, new Opcao<Guid?>(eq, d.Nome ?? "Equipe")];
        _equipe = _equipes.FirstOrDefault(o => o.Valor == d.EquipeId) ?? Nenhum;
        _pessoas = PessoasDaFuncao(_funcao.Valor, d.PessoaId, d.Nome);
        _pessoa = _pessoas.FirstOrDefault(o => o.Valor == d.PessoaId) ?? Nenhum;
        _inicioEm = TextoTela.Data(d.InicioEm == default ? null : d.InicioEm);
        _fimEm = TextoTela.Data(d.FimEm);
        _observacao = d.Observacao ?? string.Empty;
    }

    public static ResponsavelTerritorioFormulario De(TerritorioResponsavelDto d, TerritoriosOpcoesDto opcoes) => new(d, true, opcoes);

    public static ResponsavelTerritorioFormulario Novo(TerritoriosOpcoesDto opcoes) =>
        new(new TerritorioResponsavelDto { Id = IdSequencial.Novo(), InicioEm = DateOnly.FromDateTime(DateTime.Today) }, false, opcoes);

    public Guid Id { get; }
    public bool Gravado { get; }

    /// <summary>Gravado e já começou: o histórico não é reescrito.</summary>
    public bool JaComecou => Gravado && _gravado.InicioEm <= DateOnly.FromDateTime(DateTime.Today);
    public bool PodeEditarQuem => !JaComecou;
    public bool PodeRemover => !JaComecou;

    public Opcao<bool>[] ListaTiposQuem => TiposQuem;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(EhEquipe), nameof(EhPessoa))] private Opcao<bool> _tipoQuem;
    [ObservableProperty] private Opcao<Guid>[] _funcoes;
    [ObservableProperty] private Opcao<Guid> _funcao;
    [ObservableProperty] private Opcao<Guid?>[] _pessoas;
    [ObservableProperty] private Opcao<Guid?> _pessoa;
    [ObservableProperty] private Opcao<Guid?>[] _equipes;
    [ObservableProperty] private Opcao<Guid?> _equipe;
    [ObservableProperty] private string _inicioEm;
    [ObservableProperty] private string _fimEm;
    [ObservableProperty] private string _observacao;

    public bool EhEquipe => TipoQuem.Valor;
    public bool EhPessoa => !TipoQuem.Valor;

    /// <summary>Só quem pode ocupar a função ("Quem pode ser" do papel comercial); a pessoa gravada fica na lista.</summary>
    private Opcao<Guid?>[] PessoasDaFuncao(Guid funcaoId, Guid? atual, string? nomeAtual)
    {
        var aceitas = _opcoes.Funcoes.FirstOrDefault(f => f.Id == funcaoId)?.Classificacoes.ToHashSet() ?? [];
        Opcao<Guid?>[] lista =
        [
            Nenhum,
            .. _opcoes.Pessoas.Where(p => p.Classificacoes.Any(aceitas.Contains)).Select(p => new Opcao<Guid?>(p.Id, p.Nome))
        ];
        if (atual is { } a && lista.All(o => o.Valor != a)) lista = [.. lista, new Opcao<Guid?>(a, nomeAtual ?? "Pessoa")];
        return lista;
    }

    partial void OnFuncaoChanged(Opcao<Guid> value)
    {
        var atual = Pessoa?.Valor;
        Pessoas = PessoasDaFuncao(value.Valor, JaComecou ? _gravado.PessoaId : null, _gravado.Nome);
        Pessoa = Pessoas.FirstOrDefault(o => o.Valor == atual) ?? Nenhum;
    }

    public IEnumerable<string> Validar()
    {
        var nome = EhEquipe ? Equipe.Texto : Pessoa.Texto;
        if (Funcao.Valor == Guid.Empty) yield return "Escolha a função de cada responsável.";
        if (EhPessoa && Pessoa.Valor is null) yield return "Escolha a pessoa de cada responsável (ou mude para Equipe).";
        if (EhEquipe && Equipe.Valor is null) yield return "Escolha a equipe de cada responsável (ou mude para Pessoa).";
        if (!TextoTela.TentarData(InicioEm, out var i) || i is null) yield return $"{nome}: informe o início (dd/mm/aaaa).";
        if (!TextoTela.TentarData(FimEm, out _)) yield return $"{nome}: fim inválido (dd/mm/aaaa).";
    }

    public TerritorioResponsavelDto ParaDto(bool ativo = true)
    {
        TextoTela.TentarData(InicioEm, out var i);
        TextoTela.TentarData(FimEm, out var f);
        return new TerritorioResponsavelDto
        {
            Id = Id, PessoaId = EhPessoa ? Pessoa.Valor : null, EquipeId = EhEquipe ? Equipe.Valor : null, TipoCarteiraId = Funcao.Valor,
            InicioEm = i ?? default, FimEm = f, Observacao = TextoTela.Nulo(Observacao?.Trim()), Ativo = ativo
        };
    }
}

/// <summary>Ficha do território. Regras finais (árvore, datas, responsáveis) são da API.</summary>
public sealed partial class TerritorioEdicao : ObservableObject
{
    private readonly List<ResponsavelTerritorioFormulario> _anulados = [];

    private TerritorioEdicao(Guid id, bool novo, Guid mapaId, TerritoriosOpcoesDto opcoes, IReadOnlyList<TerritorioResumoDto> doMapa, Guid tipo, Guid? pai,
                             bool podeConfigurar)
    {
        Id = id;
        Novo = novo;
        PodeConfigurar = podeConfigurar;
        MapaId = mapaId;
        Opcoes = opcoes;
        _tipos = [.. opcoes.Tipos.Where(t => t.Ativo || t.Id == tipo).Select(t => new Opcao<Guid>(t.Id, t.Ativo ? t.Nome : t.Nome + " (desativado)"))];
        _tipo = _tipos.FirstOrDefault(t => t.Valor == tipo) ?? _tipos.FirstOrDefault() ?? new Opcao<Guid>(Guid.Empty, "—");
        _pais = ArvoreTerritorios.PaisPossiveis(id, doMapa, pai);
        _pai = _pais.FirstOrDefault(p => p.Valor == pai) ?? ArvoreTerritorios.PrimeiroNivel;
        UsoNaSubarvore = !novo && ArvoreTerritorios.UsoNaSubarvore(id, doMapa);
        _inicioEm = TextoTela.Data(DateOnly.FromDateTime(DateTime.Today));
    }

    public Guid Id { get; }
    public bool Novo { get; }

    /// <summary>Quem só visualiza vê a ficha sem poder mudar nada (o servidor recusa de qualquer jeito).</summary>
    public bool PodeConfigurar { get; }
    public Guid MapaId { get; }
    public TerritoriosOpcoesDto Opcoes { get; }
    public byte[]? Versao { get; private set; }
    public SituacaoTerritorio Situacao { get; private set; }
    public DateOnly? FimEm { get; private set; }
    public bool ComUso { get; private set; }
    public bool UsoNaSubarvore { get; }
    public string? Caminho { get; private set; }

    [ObservableProperty] private string _codigo = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private Opcao<Guid>[] _tipos;
    [ObservableProperty] private Opcao<Guid> _tipo;
    [ObservableProperty] private Opcao<Guid?>[] _pais;
    [ObservableProperty] private Opcao<Guid?> _pai;
    [ObservableProperty] private string _inicioEm;
    [ObservableProperty] private string _descricao = string.Empty;

    public ObservableCollection<ResponsavelTerritorioFormulario> Responsaveis { get; } = new();

    /// <summary>Onde o território esteve na árvore (mais recente primeiro).</summary>
    public ObservableCollection<string> Posicoes { get; } = new();

    public bool Ativo => Situacao == SituacaoTerritorio.Ativo;
    public bool Encerrado => !Novo && !Ativo;
    public bool Editavel => Ativo && PodeConfigurar;

    /// <summary>Código e "existe desde" mudam só sem uso; mover, só sem uso no território e abaixo dele.</summary>
    public bool CodigoSomenteLeitura => ComUso || !Editavel;
    public bool PodeMover => !UsoNaSubarvore && Editavel;
    public bool InicioSomenteLeitura => UsoNaSubarvore || !Editavel;
    public bool TemPosicoes => Posicoes.Count > 0;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo território" : Nome;
    public string SituacaoTexto => Novo
        ? "Novo território"
        : (Ativo ? "Ativo" : $"Encerrado (último dia {TextoTela.Data(FimEm)}): só consulta; reative para alterar") +
          (Caminho is null ? " · primeiro nível" : $" · em {Caminho}") +
          (ComUso ? " · tem regras ou atribuições: código, posição e início só mudam por operação territorial" : string.Empty);

    public static TerritorioEdicao Criar(Guid mapaId, TerritoriosOpcoesDto opcoes, IReadOnlyList<TerritorioResumoDto> doMapa, Guid? pai,
                                         bool podeConfigurar = true)
    {
        // Filho herda o tipo do pai (Brasil › Sudeste › MG: todos geográficos); na raiz, o primeiro tipo ativo.
        var tipo = pai is { } p && doMapa.FirstOrDefault(t => t.Id == p) is { } dadosPai
            ? dadosPai.TipoId
            : opcoes.Tipos.Where(t => t.Ativo).Select(t => t.Id).FirstOrDefault();
        return new TerritorioEdicao(IdSequencial.Novo(), true, mapaId, opcoes, doMapa, tipo, pai, podeConfigurar) { Situacao = SituacaoTerritorio.Ativo };
    }

    public static TerritorioEdicao De(TerritorioDto d, TerritoriosOpcoesDto opcoes, IReadOnlyList<TerritorioResumoDto> doMapa,
                                      bool podeConfigurar = true)
    {
        var e = new TerritorioEdicao(d.Id, false, d.MapaId, opcoes, doMapa, d.TipoId, d.PaiId, podeConfigurar)
        {
            Versao = d.Versao, Situacao = d.Situacao, FimEm = d.FimEm, ComUso = d.ComUso, Caminho = d.Caminho, Codigo = d.Codigo, Nome = d.Nome,
            Descricao = d.Descricao ?? string.Empty, InicioEm = TextoTela.Data(d.InicioEm)
        };
        foreach (var r in d.Responsaveis.Where(r => r.Ativo)) e.Incluir(ResponsavelTerritorioFormulario.De(r, opcoes));
        foreach (var p in d.Posicoes.Where(p => p.Ativo))
            e.Posicoes.Add($"{p.Pai ?? "Primeiro nível"}: de {TextoTela.Data(p.InicioEm)}" + (p.FimEm is { } f ? $" até {TextoTela.Data(f)}" : " em diante"));
        return e;
    }

    public void Incluir(ResponsavelTerritorioFormulario r)
    {
        r.AoRemover = () =>
        {
            Responsaveis.Remove(r);
            if (r.Gravado) _anulados.Add(r); // gravado que ainda não começou: vai como anulado (nunca apagado)
        };
        Responsaveis.Add(r);
    }

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Codigo)) erros.Add("Informe o código (ex.: MG_NORTE).");
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome do território.");
        if (Tipo.Valor == Guid.Empty) erros.Add("Escolha o tipo do território.");
        if (!TextoTela.TentarData(InicioEm, out var i) || i is null) erros.Add("Existe desde: informe a data (dd/mm/aaaa).");
        erros.AddRange(Responsaveis.SelectMany(r => r.Validar()).Distinct());
        return erros;
    }

    public TerritorioDto ParaDto()
    {
        TextoTela.TentarData(InicioEm, out var inicio);
        return new TerritorioDto
        {
            Id = Id, Versao = Versao, MapaId = MapaId, Codigo = Codigo.Trim(), Nome = Nome.Trim(), TipoId = Tipo.Valor, PaiId = Pai.Valor,
            Descricao = TextoTela.Nulo(Descricao?.Trim()), InicioEm = inicio,
            Responsaveis = [.. Responsaveis.Select(r => r.ParaDto()), .. _anulados.Select(r => r.ParaDto(ativo: false))]
        };
    }
}

/// <summary>
/// Comercial › Territórios (Fase 2b-1a): escolhe o mapa, mostra a árvore (com busca) e a ficha do território com os
/// responsáveis e o histórico da posição. Nada é excluído: território é encerrado. Com uso operacional (2b-1b), mover,
/// encerrar e reativar passam a exigir operação territorial.
/// </summary>
public sealed partial class TerritoriosViewModel : CadastroViewModelBase<LinhaTerritorio>
{
    /// <summary>Lista em colunas (padrão de tela de cadastro, 03/10/2026).</summary>
    protected override GradeCadastro<LinhaTerritorio> CriarGradeDaLista() => new(
        "Território", l => l.Item.Id, l => l.Nome, l => l.Item.Codigo,
        ColunaCadastro<LinhaTerritorio>.Curto("tipo", "Tipo", l => l.Item.Tipo, 160),
        ColunaCadastro<LinhaTerritorio>.Texto("responsaveis", "Responsáveis hoje", l => l.Item.ResponsaveisHoje),
        ColunaCadastro<LinhaTerritorio>.Curto("abaixo", "Abaixo", l => l.Abaixo > 0 ? l.Abaixo.ToString("N0", TextoTela.Brasil) : "—", 100),
        ColunaCadastro<LinhaTerritorio>.Selo("situacao", "Situação", l => l.Encerrado ? "Encerrado" : "Em vigor", l => l.Encerrado ? "Neutro" : "Sucesso", 130))
    {
        Recuo = l => l.Recuo,
        Marcador = l => l.Marcador
    };

    private readonly TerritoriosApi _api;
    private readonly SessaoCliente _sessao;
    private TerritoriosOpcoesDto _opcoes = new();
    private IReadOnlyList<TerritorioResumoDto> _doMapa = [];

    /// <summary>Versão da árvore que a tela mostra: vai em toda mudança de estrutura (a árvore mudou = conflito, nada grava).</summary>
    private byte[]? _versaoArvore;
    private Guid? _paiDoNovo;
    private bool _trocandoMapa;

    public TerritoriosViewModel(TerritoriosApi api, SessaoCliente sessao, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
        _sessao = sessao;
    }

    /// <summary>Criar, alterar, encerrar e reativar: só com TERRITORIOS.CONFIGURAR (quem só visualiza consulta).</summary>
    public bool PodeConfigurar => _sessao.Possui(Permissoes.Territorios.Configurar);
    public override bool PodeCriar => PodeConfigurar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeEncerrar), nameof(PodeReativar), nameof(PodeCriarAbaixo), nameof(PodeSalvar))]
    private TerritorioEdicao? _formulario;

    [ObservableProperty] private Opcao<Guid?>[] _mapas = [];
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemMapa), nameof(TextoMapa))] private Opcao<Guid?>? _mapa;
    [ObservableProperty] private bool _mostrarEncerrados;

    public bool TemMapa => Mapa?.Valor is not null;

    /// <summary>Resumo do mapa escolhido ("exclusivo · Cliente · endereço Comercial").</summary>
    public string TextoMapa => _opcoes.Mapas.FirstOrDefault(m => m.Id == Mapa?.Valor) is { } m
        ? string.Join("  ·  ", new[]
        {
            m.Exclusivo ? "Exclusivo: um território por cliente" : "Não exclusivo: o cliente pode ficar em vários",
            "Universo: " + string.Join(", ", _opcoes.Classificacoes.Where(c => m.Classificacoes.Contains(c.Id)).Select(c => c.Nome)),
            "Endereço de referência: " + (m.FinalidadeEnderecoReferencia ?? "—"),
            m.Empresa ?? "Grupo todo",
            m.Ativo ? null : "Mapa desativado (só consulta)"
        }.Where(x => x is not null))
        : "Cadastre um mapa territorial em Comercial › Configurações › Mapas territoriais.";

    public bool PodeEncerrar => PodeConfigurar && Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => PodeConfigurar && Formulario is { Encerrado: true };
    public bool PodeCriarAbaixo => PodeConfigurar && Formulario is { Novo: false, Ativo: true };
    public bool PodeSalvar => Formulario is { Editavel: true };

    protected override string TextoDeBusca(LinhaTerritorio item) => $"{item.Nome} {item.Detalhe} {item.Caminho}";

    protected override async Task AntesDeListarAsync()
    {
        _opcoes = await _api.ListarOpcoesAsync();
        var anterior = Mapa?.Valor;
        _trocandoMapa = true;
        try
        {
            Mapas = [.. _opcoes.Mapas.OrderBy(m => !m.Ativo).ThenBy(m => m.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(m => new Opcao<Guid?>(m.Id, m.Ativo ? m.Nome : m.Nome + " (desativado)"))];
            Mapa = Mapas.FirstOrDefault(m => m.Valor == anterior) ?? Mapas.FirstOrDefault();
        }
        finally
        {
            _trocandoMapa = false;
        }
        OnPropertyChanged(nameof(TextoMapa));
    }

    protected override async Task<IReadOnlyList<LinhaTerritorio>> ListarAsync()
    {
        var arvore = Mapa?.Valor is { } mapaId ? await _api.ListarDoMapaAsync(mapaId) : new ArvoreTerritorialDto();
        _doMapa = arvore.Territorios;
        _versaoArvore = arvore.VersaoArvore;
        return ArvoreTerritorios.Linhas(_doMapa, MostrarEncerrados);
    }

    partial void OnMapaChanged(Opcao<Guid?>? oldValue, Opcao<Guid?>? newValue)
    {
        if (_trocandoMapa || oldValue?.Valor == newValue?.Valor) return;
        _ = TrocarMapaAsync(oldValue);
    }

    /// <summary>Trocar de mapa fecha a ficha (perguntando antes se houver alterações) e relê a árvore.</summary>
    private async Task TrocarMapaAsync(Opcao<Guid?>? anterior)
    {
        if (!await PodePerderAlteracoesAsync())
        {
            _trocandoMapa = true;
            try { Mapa = anterior; }
            finally { _trocandoMapa = false; }
            return;
        }
        FecharSemPerguntar();
        await RecarregarAsync();
    }

    partial void OnMostrarEncerradosChanged(bool value) => _ = RecarregarAsync();

    protected override async Task AbrirAsync(LinhaTerritorio item)
    {
        Formulario = await ObterAsync(item.Item.Id);
        await CarregarMotorAsync(item.Item.Id);
    }

    // ---- Fase 2b-1b: regras, exceções e clientes do território (só leitura: mudam por operação territorial) ----

    public ObservableCollection<string> Regras { get; } = new();
    public ObservableCollection<string> Excecoes { get; } = new();
    public ObservableCollection<string> Clientes { get; } = new();
    public ObservableCollection<string> OperacoesAbertas { get; } = new();
    [ObservableProperty] private string _textoClientes = string.Empty;
    [ObservableProperty] private bool _temMotor;

    /// <summary>O que está gravado (nunca recalculado): versões da regra, exceções, clientes atribuídos hoje e operações em aberto.</summary>
    private async Task CarregarMotorAsync(Guid territorioId)
    {
        Regras.Clear();
        Excecoes.Clear();
        Clientes.Clear();
        OperacoesAbertas.Clear();
        TemMotor = false;
        TerritorioMotorDto motor;
        try
        {
            motor = await _api.MotorDoTerritorioAsync(territorioId);
        }
        catch (Exception)
        {
            return; // sem as regras, a ficha continua (a árvore e os responsáveis são da 2b-1a)
        }
        foreach (var r in motor.Regras)
            Regras.Add($"v{r.Numero}{(r.Vigente ? " (vigente)" : r.Ativo ? string.Empty : " (anulada)")} · " +
                       (r.Prioridade is { } p ? $"prioridade {p}" : "sem prioridade") +
                       $" · de {TextoTela.Data(r.InicioEm)}" + (r.FimEm is { } f ? $" até {TextoTela.Data(f)}" : " em diante") +
                       (r.Operacao is null ? string.Empty : $" · {r.Operacao}") + Environment.NewLine + r.Criterios +
                       (r.ComCondicaoRestrita ? " (há condição restrita que você não pode ver)" : string.Empty));
        foreach (var x in motor.Excecoes)
            Excecoes.Add($"{(x.Tipo == TipoExcecaoTerritorio.Fixar ? "Fixar" : "Retirar")} {x.Pessoa} · de {TextoTela.Data(x.InicioEm)}" +
                         (x.FimEm is { } f ? $" até {TextoTela.Data(f)}" : " em diante") + $" · {x.Motivo}" + (x.Operacao is null ? string.Empty : $" · {x.Operacao}") +
                         (x.Vigente ? string.Empty : x.Ativo ? " (encerrada)" : " (anulada)"));
        foreach (var c in motor.Clientes)
            Clientes.Add($"{c.Pessoa} · desde {TextoTela.Data(c.InicioEm)} · {c.OrigemDescricao}" + (c.Operacao is null ? string.Empty : $" · {c.Operacao}"));
        foreach (var o in motor.OperacoesAbertas)
            OperacoesAbertas.Add($"{o.Numero} ({o.SituacaoNome}) · efeito {TextoTela.Data(o.EfeitoEm)} · {o.Motivo}");
        TextoClientes = motor.TotalClientes == 0
            ? "Nenhum cliente atribuído hoje."
            : $"{motor.TotalClientes.ToString("N0", TextoTela.Brasil)} cliente(s) atribuído(s) hoje" +
              (motor.TotalClientes > motor.Clientes.Count ? $" (mostrando {motor.Clientes.Count})." : ".");
        TemMotor = true;
    }

    protected override Task NovoItemAsync()
    {
        if (Mapa?.Valor is not { } mapaId) throw new ValidacaoException(["Escolha (ou cadastre) um mapa territorial antes."]);
        Formulario = TerritorioEdicao.Criar(mapaId, _opcoes, _doMapa, _paiDoNovo, PodeConfigurar);
        TemMotor = false; // território novo: ainda sem regra nem clientes
        _paiDoNovo = null;
        return Task.CompletedTask;
    }

    /// <summary>"+ Território abaixo deste": a ficha nova já vem com este como território acima.</summary>
    [RelayCommand]
    private async Task NovoAbaixoAsync()
    {
        if (Formulario is not { Novo: false, Ativo: true } f) return;
        _paiDoNovo = f.Id;
        await NovoCommand.ExecuteAsync(null);
        _paiDoNovo = null;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override object? FichaObservada => Formulario;
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is { Novo: false } f)
        {
            Formulario = await ObterAsync(f.Id);
            await CarregarMotorAsync(f.Id);
        }
    }

    private async Task<TerritorioEdicao> ObterAsync(Guid id) =>
        TerritorioEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Este território não existe mais."]), _opcoes, _doMapa, PodeConfigurar);

    [RelayCommand]
    private void AdicionarResponsavel()
    {
        if (Formulario is { Editavel: true } f) f.Incluir(ResponsavelTerritorioFormulario.Novo(_opcoes));
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } f) return;
        if (f.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }
        TerritorioDto? salvo = null;
        var dto = f.ParaDto();
        dto.VersaoArvore = _versaoArvore; // o servidor só exige quando a gravação muda a estrutura
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(dto))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = TerritorioEdicao.De(salvo!, _opcoes, _doMapa, PodeConfigurar);
        MarcarFichaSemAlteracoes();
        Mostrar(f.Novo ? "Território criado." : "Alterações salvas.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private async Task EncerrarAsync()
    {
        if (Formulario is not { Novo: false, Ativo: true } f) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes.", TipoMensagem.Aviso);
            return;
        }
        var motivo = await PerguntarAsync("Encerrar território",
            $"\"{f.Nome}\" sai da árvore a partir de amanhã (hoje é o último dia) e os responsáveis dele terminam hoje. Nada é apagado: " +
            "o território continua no histórico e pode ser reativado enquanto não tiver regras nem atribuições. Motivo (opcional):",
            "Encerrar", "Cancelar", "Ex.: região incorporada a MG Norte");
        if (motivo is null) return;
        TerritorioDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = await _api.EncerrarAsync(f.Id, f.Versao, _versaoArvore, TextoTela.Nulo(motivo.Trim())))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = TerritorioEdicao.De(gravado!, _opcoes, _doMapa, PodeConfigurar);
        MarcarFichaSemAlteracoes();
        Mostrar("Território encerrado.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private async Task ReativarAsync()
    {
        if (Formulario is not { Encerrado: true } f) return;
        TerritorioDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = await _api.ReativarAsync(f.Id, f.Versao, _versaoArvore))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = TerritorioEdicao.De(gravado!, _opcoes, _doMapa, PodeConfigurar);
        MarcarFichaSemAlteracoes();
        Mostrar("Território reativado.", TipoMensagem.Sucesso);
    }
}
