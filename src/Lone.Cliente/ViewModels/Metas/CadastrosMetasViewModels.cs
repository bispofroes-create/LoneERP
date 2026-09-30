using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Comum;
using Lone.Contracts.Metas;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Metas;

/// <summary>Textos e listas de escolha das telas de metas.</summary>
public static class OpcoesMetas
{
    public static readonly Opcao<Guid?> Nenhum = new(null, "—");

    public static readonly Opcao<FonteIndicador>[] Fontes =
    [
        new(FonteIndicador.Informado, "Informado (lançado/importado)"),
        new(FonteIndicador.NovosClientes, "Novos clientes (cadastro)"),
        new(FonteIndicador.ClientesAtivos, "Clientes ativos (cadastro)"),
        new(FonteIndicador.ClientesReativados, "Clientes reativados (cadastro)"),
        new(FonteIndicador.InteracoesRegistradas, "Interações registradas (cadastro)")
    ];

    public static readonly Opcao<UnidadeIndicador>[] Unidades =
        [new(UnidadeIndicador.Quantidade, "Quantidade"), new(UnidadeIndicador.Moeda, "Valor (R$)"), new(UnidadeIndicador.Percentual, "Percentual (%)")];

    public static readonly Opcao<SentidoIndicador>[] Sentidos =
        [new(SentidoIndicador.MaiorMelhor, "Quanto maior, melhor"), new(SentidoIndicador.MenorMelhor, "Quanto menor, melhor")];

    public static readonly Opcao<NivelParticipante>[] Niveis =
    [
        new(NivelParticipante.Empresa, "Empresa"), new(NivelParticipante.Filial, "Filial"), new(NivelParticipante.Departamento, "Departamento"),
        new(NivelParticipante.Equipe, "Equipe"), new(NivelParticipante.Colaborador, "Colaborador")
    ];

    public static string Nome(NivelParticipante n) => Niveis.First(x => x.Valor == n).Texto;

    public static string Nome(SituacaoMeta s) => s switch
    {
        SituacaoMeta.Rascunho => "Rascunho",
        SituacaoMeta.Publicada => "Publicada",
        SituacaoMeta.EmApuracao => "Em apuração",
        SituacaoMeta.Fechada => "Fechada",
        _ => s.ToString()
    };

    public static string Nome(OrigemRealizado? o) => o switch
    {
        OrigemRealizado.Calculado => "calculado",
        OrigemRealizado.Informado => "informado",
        OrigemRealizado.Importado => "importado",
        _ => string.Empty
    };

    /// <summary>Opções de um nível (as ativas, mais a gravada se não estiver entre elas).</summary>
    public static Opcao<Guid?>[] Participantes(IReadOnlyList<ParticipanteOpcaoDto> todas, NivelParticipante nivel, Guid? gravado, string? nomeGravado)
    {
        var lista = todas.Where(o => o.Nivel == nivel).Select(o => new Opcao<Guid?>(o.Id, o.Nome)).ToList();
        if (gravado is { } g && g != Guid.Empty && lista.All(o => o.Valor != g))
            lista.Insert(0, new Opcao<Guid?>(g, (nomeGravado ?? "(gravado)") + " (inativo)"));
        return [Nenhum, .. lista];
    }

    public static Opcao<Guid?> Escolher(Opcao<Guid?>[] lista, Guid? id) => lista.FirstOrDefault(o => o.Valor == id) ?? lista[0];
}

// =====================================================================================================================
// Indicadores
// =====================================================================================================================

public sealed class LinhaIndicador
{
    public LinhaIndicador(IndicadorDto item) => Item = item;
    public IndicadorDto Item { get; }
    public string Nome => Item.Nome;
    public string Detalhe => $"{Item.Codigo} · {OpcoesMetas.Fontes.First(f => f.Valor == Item.Fonte).Texto}";
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha do indicador. Código e fonte não mudam depois de gravado (as metas já apontam para eles).</summary>
public sealed partial class IndicadorEdicao : ObservableObject
{
    private IndicadorEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
        _fonte = OpcoesMetas.Fontes[0];
        _unidade = OpcoesMetas.Unidades[0];
        _sentido = OpcoesMetas.Sentidos[0];
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public bool DoSistema { get; private set; }
    public bool PodeMudarCodigo => Novo;
    public bool PodeMudarSentido => !DoSistema;

    [ObservableProperty] private string _codigo = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private Opcao<FonteIndicador> _fonte;
    [ObservableProperty] private Opcao<UnidadeIndicador> _unidade;
    [ObservableProperty] private Opcao<SentidoIndicador> _sentido;

    public IReadOnlyList<Opcao<FonteIndicador>> ListaFontes => Novo ? OpcoesMetas.Fontes.Take(1).ToArray() : OpcoesMetas.Fontes;
    public IReadOnlyList<Opcao<UnidadeIndicador>> ListaUnidades => OpcoesMetas.Unidades;
    public IReadOnlyList<Opcao<SentidoIndicador>> ListaSentidos => OpcoesMetas.Sentidos;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo indicador" : Nome;
    public string SituacaoTexto => Novo
        ? "Novo indicador informado: o realizado é lançado na meta ou importado de planilha."
        : DoSistema ? "Indicador do sistema: o realizado é contado no cadastro (carteira de clientes do participante)."
        : Ativo ? "Ativo" : "Desativado (não aparece para novas metas; continua nas que já o usam)";

    public static IndicadorEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static IndicadorEdicao De(IndicadorDto d) => new(d.Id, novo: false)
    {
        Versao = d.Versao,
        Ativo = d.Ativo,
        DoSistema = d.DoSistema,
        Codigo = d.Codigo,
        Nome = d.Nome,
        Fonte = OpcoesMetas.Fontes.First(f => f.Valor == d.Fonte),
        Unidade = OpcoesMetas.Unidades.First(f => f.Valor == d.Unidade),
        Sentido = OpcoesMetas.Sentidos.First(f => f.Valor == d.Sentido)
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Codigo)) erros.Add("Informe o código (ex.: FATURAMENTO).");
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome.");
        return erros;
    }

    public IndicadorDto ParaDto() => new()
    {
        Id = Id, Versao = Versao, Codigo = Codigo.Trim().ToUpperInvariant(), Nome = Nome.Trim(),
        Fonte = Fonte.Valor, Unidade = Unidade.Valor, Sentido = Sentido.Valor, Ativo = Ativo
    };
}

/// <summary>Indicadores das metas. Nada é excluído: desativar esconde das metas novas.</summary>
public sealed partial class IndicadoresViewModel : CadastroViewModelBase<LinhaIndicador>
{
    private readonly MetasApi _api;

    public IndicadoresViewModel(MetasApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private IndicadorEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true, DoSistema: false };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaIndicador item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaIndicador>> ListarAsync() =>
        (await _api.ListarIndicadoresAsync()).OrderBy(t => !t.Ativo).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new LinhaIndicador(t)).ToList();

    protected override async Task AbrirAsync(LinhaIndicador item) => Formulario = await ObterAsync(item.Item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = IndicadorEdicao.Criar();
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override object? FichaObservada => Formulario;
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is { Novo: false } f) Formulario = await ObterAsync(f.Id);
    }

    private async Task<IndicadorEdicao> ObterAsync(Guid id) =>
        IndicadorEdicao.De(await _api.ObterIndicadorAsync(id) ?? throw new ValidacaoException(["Este indicador não existe mais."]));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } f) return;
        if (f.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }
        IndicadorDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarIndicadorAsync(f.ParaDto()))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = IndicadorEdicao.De(salvo!);
        MarcarFichaSemAlteracoes();
        Mostrar(f.Novo ? "Indicador criado." : "Alterações salvas.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private Task DesativarAsync() => AlterarAtivoAsync(ativar: false);

    [RelayCommand]
    private Task ReativarAsync() => AlterarAtivoAsync(ativar: true);

    private async Task AlterarAtivoAsync(bool ativar)
    {
        if (Formulario is not { Novo: false } f) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes.", TipoMensagem.Aviso);
            return;
        }
        IndicadorDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = await _api.AlterarAtivoAsync<IndicadorDto>(Rotas.Metas.Indicadores, f.Id, ativar, f.Versao))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = IndicadorEdicao.De(gravado!);
        MarcarFichaSemAlteracoes();
        Mostrar(ativar ? "Reativado." : "Desativado.", TipoMensagem.Sucesso);
    }
}

// =====================================================================================================================
// Equipes
// =====================================================================================================================

public sealed class LinhaEquipe
{
    public LinhaEquipe(EquipeDto item, DateOnly hoje)
    {
        Item = item;
        Membros = item.Membros.Count(m => m.InicioEm <= hoje && (m.FimEm is null || m.FimEm >= hoje)).ToString("N0", TextoTela.Brasil);
    }

    public EquipeDto Item { get; }
    public string Nome => Item.Nome;
    public string Detalhe => string.Join("  ·  ", new[]
    {
        Item.Lider is { } l ? "Líder: " + l : null,
        Item.EquipePai is { } p ? "Acima: " + p : null
    }.Where(x => x is not null));
    public string Membros { get; }
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Membro com entrada e saída. Gravado não é apagado: encerra pela saída.</summary>
public sealed partial class MembroEquipeFormulario : ItemDeLista
{
    private readonly MembroEquipeDto _gravado;

    private MembroEquipeFormulario(MembroEquipeDto d, bool gravado, IReadOnlyList<ParticipanteOpcaoDto> opcoes)
    {
        _gravado = d;
        Gravado = gravado;
        Id = d.Id;
        _inicioEm = TextoTela.Data(d.InicioEm == default ? null : d.InicioEm);
        _fimEm = TextoTela.Data(d.FimEm);
        _pessoas = OpcoesMetas.Participantes(opcoes, NivelParticipante.Colaborador, d.PessoaId == Guid.Empty ? null : d.PessoaId, d.Pessoa);
        _pessoa = OpcoesMetas.Escolher(_pessoas, d.PessoaId);
        _papel = Papeis.FirstOrDefault(x => x.Valor == d.Papel) ?? Papeis[0];
    }

    /// <summary>Membro ou Líder (um líder por vez; decisão F3). Array: o Picker precisa de IList.</summary>
    public static readonly Opcao<PapelNaEquipe>[] Papeis = [new(PapelNaEquipe.Membro, "Membro"), new(PapelNaEquipe.Lider, "Líder")];

    public Opcao<PapelNaEquipe>[] ListaPapeis => Papeis;

    public static MembroEquipeFormulario De(MembroEquipeDto d, IReadOnlyList<ParticipanteOpcaoDto> opcoes) => new(d, true, opcoes);

    public static MembroEquipeFormulario Novo(IReadOnlyList<ParticipanteOpcaoDto> opcoes) =>
        new(new MembroEquipeDto { Id = IdSequencial.Novo(), InicioEm = DateOnly.FromDateTime(DateTime.Today) }, false, opcoes);

    public Guid Id { get; }
    public bool Gravado { get; }
    public bool PodeRemover => !Gravado;
    public bool PodeTrocarPessoa => !Gravado;

    /// <summary>Papel de quem já entrou não muda (o histórico não é reescrito): encerra e inclui de novo.</summary>
    public bool PodeTrocarPapel => !Gravado || _gravado.InicioEm > DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty] private string _inicioEm;
    [ObservableProperty] private string _fimEm;
    [ObservableProperty] private Opcao<Guid?>[] _pessoas;
    [ObservableProperty] private Opcao<Guid?> _pessoa;
    [ObservableProperty] private Opcao<PapelNaEquipe> _papel;

    public IEnumerable<string> Validar()
    {
        var nome = Pessoa.Valor is null ? "Membro" : Pessoa.Texto;
        if (Pessoa.Valor is null) yield return "Escolha a pessoa de cada membro.";
        if (!TextoTela.TentarData(InicioEm, out var i) || i is null) yield return $"{nome}: informe a entrada (dd/mm/aaaa).";
        if (!TextoTela.TentarData(FimEm, out _)) yield return $"{nome}: saída inválida (dd/mm/aaaa).";
    }

    public MembroEquipeDto ParaDto()
    {
        TextoTela.TentarData(InicioEm, out var i);
        TextoTela.TentarData(FimEm, out var f);
        return new MembroEquipeDto { Id = Id, PessoaId = Pessoa.Valor ?? _gravado.PessoaId, InicioEm = i ?? default, FimEm = f, Papel = Papel.Valor };
    }
}

public sealed partial class EquipeEdicao : ObservableObject
{
    private EquipeEdicao(Guid id, bool novo, MetaOpcoesDto opcoes, IReadOnlyList<EquipeDto> equipes, Guid? departamento, Guid? pai,
                         string? nomeLider)
    {
        Id = id;
        Novo = novo;
        Opcoes = opcoes;
        _departamentos = OpcoesMetas.Participantes(opcoes.Participantes, NivelParticipante.Departamento, departamento, null);
        _departamento = OpcoesMetas.Escolher(_departamentos, departamento);
        _equipesAcima = EquipesAcimaPossiveis(id, equipes, pai);
        _equipeAcima = OpcoesMetas.Escolher(_equipesAcima, pai);
        LiderHoje = nomeLider ?? "Sem líder hoje";
    }

    /// <summary>
    /// Equipes que podem ficar acima: as ativas, menos a própria e as que estão abaixo dela (evita ciclo); a gravada fica
    /// na lista mesmo desativada. O servidor confere de novo (RegrasEquipe).
    /// </summary>
    public static Opcao<Guid?>[] EquipesAcimaPossiveis(Guid id, IReadOnlyList<EquipeDto> equipes, Guid? atual)
    {
        var abaixo = Abaixo(id, equipes);
        return
        [
            OpcoesMetas.Nenhum,
            .. equipes.Where(e => e.Id != id && !abaixo.Contains(e.Id) && (e.Ativo || e.Id == atual))
                .OrderBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(e => new Opcao<Guid?>(e.Id, e.Ativo ? e.Nome : e.Nome + " (desativada)"))
        ];
    }

    /// <summary>As equipes abaixo de <paramref name="id"/> (filhas, netas...). Resiste a ciclo gravado.</summary>
    private static HashSet<Guid> Abaixo(Guid id, IReadOnlyList<EquipeDto> equipes)
    {
        var filhas = equipes.Where(e => e.EquipePaiId is not null).ToLookup(e => e.EquipePaiId!.Value, e => e.Id);
        var resultado = new HashSet<Guid>();
        var fila = new Queue<Guid>();
        fila.Enqueue(id);
        while (fila.Count > 0)
            foreach (var filha in filhas[fila.Dequeue()])
                if (filha != id && resultado.Add(filha)) fila.Enqueue(filha);
        return resultado;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public MetaOpcoesDto Opcoes { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private Opcao<Guid?>[] _departamentos;
    [ObservableProperty] private Opcao<Guid?> _departamento;
    [ObservableProperty] private Opcao<Guid?>[] _equipesAcima;
    [ObservableProperty] private Opcao<Guid?> _equipeAcima;

    /// <summary>Somente leitura: o líder é o membro com papel Líder vigente hoje (recalculado ao salvar).</summary>
    public string LiderHoje { get; }

    public ObservableCollection<MembroEquipeFormulario> Membros { get; } = new();

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Nova equipe" : Nome;
    public string SituacaoTexto => Novo ? "Nova equipe" : Ativo ? "Ativa" : "Desativada (não aparece para novas metas)";

    public static EquipeEdicao Criar(MetaOpcoesDto opcoes, IReadOnlyList<EquipeDto> equipes) =>
        new(IdSequencial.Novo(), true, opcoes, equipes, null, null, null);

    public static EquipeEdicao De(EquipeDto d, MetaOpcoesDto opcoes, IReadOnlyList<EquipeDto> equipes)
    {
        var e = new EquipeEdicao(d.Id, false, opcoes, equipes, d.DepartamentoId, d.EquipePaiId, d.Lider)
            { Versao = d.Versao, Ativo = d.Ativo, Nome = d.Nome };
        foreach (var m in d.Membros.OrderBy(m => m.FimEm is not null).ThenBy(m => m.Pessoa)) e.Incluir(MembroEquipeFormulario.De(m, opcoes.Participantes));
        return e;
    }

    public void Incluir(MembroEquipeFormulario m)
    {
        m.AoRemover = () => Membros.Remove(m);
        Membros.Add(m);
    }

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome.");
        erros.AddRange(Membros.SelectMany(m => m.Validar()).Distinct());
        return erros;
    }

    public EquipeDto ParaDto() => new()
    {
        Id = Id, Versao = Versao, Nome = Nome.Trim(), DepartamentoId = Departamento.Valor, EquipePaiId = EquipeAcima.Valor, Ativo = Ativo,
        Membros = Membros.Select(m => m.ParaDto()).ToList()
    };
}

/// <summary>Equipes das metas (nível entre departamento e colaborador). Nada é excluído.</summary>
public sealed partial class EquipesViewModel : CadastroViewModelBase<LinhaEquipe>
{
    private readonly MetasApi _api;
    private MetaOpcoesDto _opcoes = new();
    private IReadOnlyList<EquipeDto> _equipes = [];

    public EquipesViewModel(MetasApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private EquipeEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaEquipe item) => item.Nome + " " + item.Detalhe;

    protected override async Task AntesDeListarAsync() => _opcoes = await _api.ListarOpcoesAsync();

    protected override async Task<IReadOnlyList<LinhaEquipe>> ListarAsync()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        _equipes = await _api.ListarEquipesAsync();
        return _equipes.OrderBy(t => !t.Ativo).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new LinhaEquipe(t, hoje)).ToList();
    }

    protected override async Task AbrirAsync(LinhaEquipe item) => Formulario = await ObterAsync(item.Item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = EquipeEdicao.Criar(_opcoes, _equipes);
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override object? FichaObservada => Formulario;
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is { Novo: false } f) Formulario = await ObterAsync(f.Id);
    }

    private async Task<EquipeEdicao> ObterAsync(Guid id) =>
        EquipeEdicao.De(await _api.ObterEquipeAsync(id) ?? throw new ValidacaoException(["Esta equipe não existe mais."]), _opcoes, _equipes);

    [RelayCommand]
    private void AdicionarMembro() => Formulario?.Incluir(MembroEquipeFormulario.Novo(_opcoes.Participantes));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } f) return;
        if (f.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }
        EquipeDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarEquipeAsync(f.ParaDto()))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = EquipeEdicao.De(salvo!, _opcoes, _equipes);
        MarcarFichaSemAlteracoes();
        Mostrar(f.Novo ? "Equipe criada." : "Alterações salvas.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private Task DesativarAsync() => AlterarAtivoAsync(ativar: false);

    [RelayCommand]
    private Task ReativarAsync() => AlterarAtivoAsync(ativar: true);

    private async Task AlterarAtivoAsync(bool ativar)
    {
        if (Formulario is not { Novo: false } f) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes.", TipoMensagem.Aviso);
            return;
        }
        EquipeDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = await _api.AlterarAtivoAsync<EquipeDto>(Rotas.Metas.Equipes, f.Id, ativar, f.Versao))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = EquipeEdicao.De(gravado!, _opcoes, _equipes);
        MarcarFichaSemAlteracoes();
        Mostrar(ativar ? "Reativada." : "Desativada.", TipoMensagem.Sucesso);
    }
}
