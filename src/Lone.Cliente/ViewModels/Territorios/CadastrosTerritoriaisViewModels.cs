using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Territorios;
using Lone.Domain.Comum;
using Lone.Domain.Enderecos;
using Lone.Domain.Enums;
using Lone.Domain.Papeis;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Territorios;

// =====================================================================================================================
// Tipos de território
// =====================================================================================================================

public sealed class LinhaTipoTerritorio
{
    public LinhaTipoTerritorio(TipoTerritorioDto item) => Item = item;

    public TipoTerritorioDto Item { get; }
    public string Nome => Item.Nome;
    public string Detalhe => Item.DoSistema ? $"{Item.Codigo} · do sistema" : Item.Codigo;
    public string Usos => Item.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha do tipo de território. O código é informado ao criar e depois não muda.</summary>
public sealed partial class TipoTerritorioEdicao : ObservableObject
{
    private TipoTerritorioEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public bool DoSistema { get; private set; }
    public int QuantidadeUsos { get; private set; }

    [ObservableProperty] private string _codigo = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private string _ordem = string.Empty;

    /// <summary>O código só é digitado na criação (relatórios e integrações dependem dele).</summary>
    public bool CodigoSomenteLeitura => !Novo;
    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo tipo" : Nome;
    public string SituacaoTexto => Novo ? "Novo tipo" : (Ativo ? "Ativo" : "Desativado (não aparece para territórios novos; continua nos que já o têm)") +
        (DoSistema ? " · nasceu com o sistema (pode ser renomeado ou desativado)" : string.Empty);
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} território(s) com este tipo.";

    public static TipoTerritorioEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static TipoTerritorioEdicao De(TipoTerritorioDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao, Ativo = t.Ativo, DoSistema = t.DoSistema, QuantidadeUsos = t.QuantidadeUsos, Codigo = t.Codigo, Nome = t.Nome,
        Descricao = t.Descricao ?? string.Empty, Ordem = t.Ordem.ToString(CultureInfo.InvariantCulture)
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (Novo && string.IsNullOrWhiteSpace(Codigo)) erros.Add("Informe o código (ex.: GEOGRAFICO).");
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome do tipo.");
        if (!string.IsNullOrWhiteSpace(Ordem) && !TextoTela.TentarInteiro(Ordem, out _)) erros.Add("Ordem: use um número inteiro.");
        return erros;
    }

    public TipoTerritorioDto ParaDto()
    {
        TextoTela.TentarInteiro(Ordem, out var ordem);
        return new TipoTerritorioDto
        {
            Id = Id, Versao = Versao, Codigo = Codigo.Trim(), Nome = Nome.Trim(), Descricao = TextoTela.Nulo(Descricao?.Trim()), Ordem = ordem ?? 0,
            Ativo = Ativo
        };
    }
}

/// <summary>Tipos de território (Geográfico, Segmento, Estratégico...): só classificam. Nada é excluído.</summary>
public sealed partial class TiposTerritorioViewModel : CadastroViewModelBase<LinhaTipoTerritorio>
{
    private readonly TerritoriosApi _api;

    public TiposTerritorioViewModel(TerritoriosApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private TipoTerritorioEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaTipoTerritorio item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaTipoTerritorio>> ListarAsync() =>
        (await _api.ListarTiposAsync()).OrderBy(t => !t.Ativo).ThenBy(t => t.Ordem).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new LinhaTipoTerritorio(t)).ToList();

    protected override async Task AbrirAsync(LinhaTipoTerritorio item) => Formulario = await ObterAsync(item.Item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = TipoTerritorioEdicao.Criar();
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override object? FichaObservada => Formulario;
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is { Novo: false } f) Formulario = await ObterAsync(f.Id);
    }

    private async Task<TipoTerritorioEdicao> ObterAsync(Guid id) =>
        TipoTerritorioEdicao.De(await _api.ObterTipoAsync(id) ?? throw new ValidacaoException(["Este tipo de território não existe mais."]));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } f) return;
        if (f.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }
        TipoTerritorioDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarTipoAsync(f.ParaDto()))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = TipoTerritorioEdicao.De(salvo!);
        MarcarFichaSemAlteracoes();
        Mostrar(f.Novo ? "Tipo criado." : "Alterações salvas.", TipoMensagem.Sucesso);
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
        TipoTerritorioDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = await _api.AlterarAtivoAsync<TipoTerritorioDto>(
                Lone.Contracts.Comum.Rotas.Territorios.Tipos, f.Id, ativar, f.Versao))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = TipoTerritorioEdicao.De(gravado!);
        MarcarFichaSemAlteracoes();
        Mostrar(ativar ? "Tipo reativado." : "Tipo desativado.", TipoMensagem.Sucesso);
    }
}

// =====================================================================================================================
// Mapas territoriais
// =====================================================================================================================

public sealed class LinhaMapaTerritorial
{
    public LinhaMapaTerritorial(MapaTerritorialDto item) => Item = item;

    public MapaTerritorialDto Item { get; }
    public string Nome => Item.Nome;
    public string Detalhe => string.Join("  ·  ", new[]
    {
        Item.Codigo, Item.Exclusivo ? "exclusivo" : "não exclusivo", Item.Empresa ?? "grupo todo"
    });
    public string Territorios => Item.TerritoriosAtivos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>
/// Ficha do mapa territorial. Empresa, exclusividade, endereço de referência e universo ficam travados depois que o mapa
/// tem uso (2b-1b); o código só é digitado na criação. As regras finais são da API.
/// </summary>
public sealed partial class MapaTerritorialEdicao : ObservableObject
{
    public static readonly Opcao<Guid?> TodasAsEmpresas = new(null, "Todas as empresas do grupo");

    private MapaTerritorialEdicao(Guid id, bool novo, TerritoriosOpcoesDto opcoes, Guid? empresa, Guid finalidade, IEnumerable<Guid> classificacoes)
    {
        Id = id;
        Novo = novo;
        _empresas = [TodasAsEmpresas, .. opcoes.Empresas.Where(e => e.Ativa || e.Id == empresa).Select(e => new Opcao<Guid?>(e.Id, e.Ativa ? e.Nome : e.Nome + " (inativa)"))];
        _empresa = _empresas.FirstOrDefault(o => o.Valor == empresa) ?? TodasAsEmpresas;
        _finalidades = [.. opcoes.Finalidades.Where(f => f.Ativo || f.Id == finalidade)
            .Select(f => new Opcao<Guid>(f.Id, f.Ativo ? f.Nome : f.Nome + " (desativada)"))];
        _finalidade = _finalidades.FirstOrDefault(o => o.Valor == finalidade) ?? _finalidades.FirstOrDefault() ?? new Opcao<Guid>(Guid.Empty, "—");
        var marcadas = classificacoes.ToHashSet();
        foreach (var c in opcoes.Classificacoes.Where(c => c.Ativo || marcadas.Contains(c.Id)))
            Classificacoes.Add(new ClassificacaoMarcavel(c.Id, c.Ativo ? c.Nome : c.Nome + " (desativada)", marcadas.Contains(c.Id)));
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public bool EmUso { get; private set; }
    public int TerritoriosAtivos { get; private set; }

    [ObservableProperty] private string _codigo = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private bool _exclusivo = true;

    /// <summary>DN-15: o território deste mapa vai para os documentos futuros (pedido, venda, comissão). Por ora, só o contrato.</summary>
    [ObservableProperty] private bool _registrarNosDocumentos;
    [ObservableProperty] private Opcao<Guid?>[] _empresas;
    [ObservableProperty] private Opcao<Guid?> _empresa;
    [ObservableProperty] private Opcao<Guid>[] _finalidades;
    [ObservableProperty] private Opcao<Guid> _finalidade;

    /// <summary>Universo: quem pode ser atribuído a um território deste mapa (padrão: Cliente).</summary>
    public ObservableCollection<ClassificacaoMarcavel> Classificacoes { get; } = new();

    public bool CodigoSomenteLeitura => !Novo;

    /// <summary>Empresa, exclusividade, endereço de referência e universo só mudam enquanto o mapa não tem uso.</summary>
    public bool PodeMudarEstrutura => !EmUso;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo mapa territorial" : Nome;
    public string SituacaoTexto => Novo ? "Novo mapa" : (Ativo ? "Ativo" : "Desativado (a árvore fica só para consulta)") +
        $" · {TerritoriosAtivos.ToString("N0", TextoTela.Brasil)} território(s) ativo(s)" +
        (EmUso ? " · em uso: empresa, exclusividade, endereço de referência e universo não mudam mais pelo cadastro" : string.Empty);

    /// <summary>Mapa novo já nasce com o padrão da empresa pequena: exclusivo, universo Cliente, endereço Comercial.</summary>
    public static MapaTerritorialEdicao Criar(TerritoriosOpcoesDto opcoes) =>
        new(IdSequencial.Novo(), true, opcoes, null, FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Comercial),
            [PapeisSistema.Id(TipoPapel.Cliente)]);

    public static MapaTerritorialEdicao De(MapaTerritorialDto m, TerritoriosOpcoesDto opcoes) =>
        new(m.Id, false, opcoes, m.EmpresaId, m.FinalidadeEnderecoReferenciaId, m.Classificacoes)
        {
            Versao = m.Versao, Ativo = m.Ativo, EmUso = m.EmUso, TerritoriosAtivos = m.TerritoriosAtivos, Codigo = m.Codigo, Nome = m.Nome,
            Descricao = m.Descricao ?? string.Empty, Exclusivo = m.Exclusivo, RegistrarNosDocumentos = m.RegistrarNosDocumentos
        };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (Novo && string.IsNullOrWhiteSpace(Codigo)) erros.Add("Informe o código (ex.: GEOGRAFIA).");
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome do mapa.");
        if (Finalidade.Valor == Guid.Empty) erros.Add("Escolha o endereço de referência.");
        if (!Classificacoes.Any(c => c.Marcado)) erros.Add("Marque ao menos uma classificação no universo (ex.: Cliente).");
        return erros;
    }

    public MapaTerritorialDto ParaDto() => new()
    {
        Id = Id, Versao = Versao, Codigo = Codigo.Trim(), Nome = Nome.Trim(), Descricao = TextoTela.Nulo(Descricao?.Trim()), EmpresaId = Empresa.Valor,
        Exclusivo = Exclusivo, FinalidadeEnderecoReferenciaId = Finalidade.Valor, Ativo = Ativo, RegistrarNosDocumentos = RegistrarNosDocumentos,
        Classificacoes = [.. Classificacoes.Where(c => c.Marcado).Select(c => c.Id)]
    };
}

/// <summary>Mapas territoriais: as dimensões independentes de atribuição (Geografia, Segmentos, Contas estratégicas...).</summary>
public sealed partial class MapasTerritoriaisViewModel : CadastroViewModelBase<LinhaMapaTerritorial>
{
    private readonly TerritoriosApi _api;
    private TerritoriosOpcoesDto _opcoes = new();

    public MapasTerritoriaisViewModel(TerritoriosApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private MapaTerritorialEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaMapaTerritorial item) => item.Nome + " " + item.Detalhe;

    protected override async Task AntesDeListarAsync() => _opcoes = await _api.ListarOpcoesAsync();

    protected override async Task<IReadOnlyList<LinhaMapaTerritorial>> ListarAsync() =>
        (await _api.ListarMapasAsync()).OrderBy(m => !m.Ativo).ThenBy(m => m.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(m => new LinhaMapaTerritorial(m)).ToList();

    protected override async Task AbrirAsync(LinhaMapaTerritorial item) => Formulario = await ObterAsync(item.Item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = MapaTerritorialEdicao.Criar(_opcoes);
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override object? FichaObservada => Formulario;
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is { Novo: false } f) Formulario = await ObterAsync(f.Id);
    }

    private async Task<MapaTerritorialEdicao> ObterAsync(Guid id) =>
        MapaTerritorialEdicao.De(await _api.ObterMapaAsync(id) ?? throw new ValidacaoException(["Este mapa territorial não existe mais."]), _opcoes);

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } f) return;
        if (f.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }
        MapaTerritorialDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarMapaAsync(f.ParaDto()))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = MapaTerritorialEdicao.De(salvo!, _opcoes);
        MarcarFichaSemAlteracoes();
        Mostrar(f.Novo ? "Mapa criado. Monte a árvore em Comercial › Territórios." : "Alterações salvas.", TipoMensagem.Sucesso);
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
        MapaTerritorialDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = await _api.AlterarAtivoAsync<MapaTerritorialDto>(
                Lone.Contracts.Comum.Rotas.Territorios.Mapas, f.Id, ativar, f.Versao))) return;
        await AtualizarListaAposGravarAsync();
        Formulario = MapaTerritorialEdicao.De(gravado!, _opcoes);
        MarcarFichaSemAlteracoes();
        Mostrar(ativar ? "Mapa reativado." : "Mapa desativado: a árvore fica só para consulta.", TipoMensagem.Sucesso);
    }
}
