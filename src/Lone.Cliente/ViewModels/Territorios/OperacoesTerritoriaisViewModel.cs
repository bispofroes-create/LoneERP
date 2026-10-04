using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.Sessao;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Auditoria;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Contracts.Territorios;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Cliente.Grade;

namespace Lone.Cliente.ViewModels.Territorios;

/// <summary>Uma operação na lista: número, mapa, efeito com os dias, situação (com os indicadores) e o resumo.</summary>
public sealed class LinhaOperacaoTerritorial
{
    public LinhaOperacaoTerritorial(OperacaoTerritorialResumoDto item, DateOnly hoje)
    {
        Item = item;
        Hoje = hoje;
    }

    public OperacaoTerritorialResumoDto Item { get; }
    private DateOnly Hoje { get; }
    public string Titulo => $"{Item.Numero} · {Item.Mapa}";
    public string Situacao => Item.SituacaoNome;
    public string Detalhe => string.Join("  ·  ", new[]
    {
        "Efeito " + ExplicacaoTerritorial.DataComDias(Item.EfeitoEm, Hoje), Item.Motivo,
        Item.Situacao == SituacaoOperacaoTerritorial.Aplicada ? $"{Item.Entraram} entraram, {Item.Sairam} saíram, {Item.Mudaram} mudaram" : $"{Item.QuantidadeMudancas} mudança(s)",
        "por " + (Item.AplicadaPor ?? Item.CriadaPor)
    }.Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>Colunas da lista (padrão de tela de cadastro, 03/10/2026).</summary>
    public string Efeito => ExplicacaoTerritorial.DataComDias(Item.EfeitoEm, Hoje);
    public string Resultado => Item.Situacao == SituacaoOperacaoTerritorial.Aplicada
        ? $"{Item.Entraram} entraram, {Item.Sairam} saíram, {Item.Mudaram} mudaram"
        : $"{Item.QuantidadeMudancas} mudança(s)";
    public string Por => Item.AplicadaPor ?? Item.CriadaPor;

    /// <summary>Ordenação da coluna "Mudanças": aplicada = entraram + saíram + mudaram; senão, as planejadas.</summary>
    public int TotalMudancas => Item.Situacao == SituacaoOperacaoTerritorial.Aplicada ? Item.Entraram + Item.Sairam + Item.Mudaram : Item.QuantidadeMudancas;
}

/// <summary>Uma mudança planejada na tela (retirar pelo próprio item).</summary>
public sealed partial class LinhaMudancaTerritorial : ObservableObject
{
    private readonly Func<LinhaMudancaTerritorial, Task> _retirar;

    public LinhaMudancaTerritorial(MudancaTerritorialDto dto, bool podeRetirar, Func<LinhaMudancaTerritorial, Task> retirar)
    {
        Dto = dto;
        PodeRetirar = podeRetirar;
        _retirar = retirar;
    }

    public MudancaTerritorialDto Dto { get; }
    public bool PodeRetirar { get; }
    public string Titulo => $"{Dto.Ordem}. {Dto.Descricao}";
    public string? Criterios => Dto.Criterios;
    public bool TemCriterios => !string.IsNullOrWhiteSpace(Dto.Criterios);
    public string Motivo => $"Motivo: {Dto.Motivo}" + (Dto.FimEm is { } f ? $" · até {TextoTela.Data(f)}" : string.Empty);
    public bool TemMotivo => !string.IsNullOrWhiteSpace(Dto.Motivo);
    public string? BaseVelha => Dto.BaseVelha;
    public bool TemBaseVelha => !string.IsNullOrWhiteSpace(Dto.BaseVelha);

    [RelayCommand]
    private Task RetirarAsync() => _retirar(this);
}

/// <summary>Um grupo de condições de uma regra: o mesmo painel de filtros de Pessoas, com os campos aceitos em regra (DN-07).</summary>
public sealed partial class GrupoRegraEdicao : ObservableObject
{
    public GrupoRegraEdicao(bool exclusao, CatalogoFiltrosPessoasDto catalogo, Func<string, Task<IReadOnlyList<Lone.Contracts.Municipios.MunicipioDto>>> municipios)
    {
        Exclusao = exclusao;
        Painel.FonteMunicipios = municipios;
        Painel.Carregar(catalogo);
    }

    public bool Exclusao { get; }
    public PainelFiltrosPessoas Painel { get; } = new();
    public string Titulo => Exclusao ? "Não entra quem atende a (grupo de exclusão)" : "Entra quem atende a (grupo de inclusão)";
    public Action<GrupoRegraEdicao>? AoRemover { get; set; }

    [RelayCommand]
    private void Remover() => AoRemover?.Invoke(this);

    public GrupoCondicoesTerritorioDto ParaDto() => new() { Condicoes = Painel.Condicoes() };
}

/// <summary>Um cliente na simulação (ou no resultado aplicado), com o "por quê?" em texto.</summary>
public sealed class LinhaItemTerritorial
{
    public LinhaItemTerritorial(ItemOperacaoTerritorialDto item, IReadOnlyDictionary<Guid, string> territorios)
    {
        Item = item;
        PorQue = ExplicacaoTerritorial.Texto(item.Explicacao, territorios);
    }

    public ItemOperacaoTerritorialDto Item { get; }
    public string Titulo => $"{Item.Codigo} · {Item.Pessoa}";
    public string Mudanca => $"{Item.TerritorioAtual ?? "sem território"} → {Item.TerritorioProposto ?? "sem território"}";
    public string Detalhe => string.Join("  ·  ", new[]
    {
        Item.EfeitoNome, Item.ResultadoNome, Item.Motivo,
        Item.OrigemEfeito switch
        {
            OrigemEfeitoSimulado.EstaOperacao => "desta operação",
            OrigemEfeitoSimulado.DivergenciaExistente => "divergência que já existia",
            _ => null
        }
    }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string PorQue { get; }
    public bool Bloqueado => Item.Efeito == EfeitoNoCliente.Bloqueado;
}

/// <summary>Texto da explicação congelada e das datas com dias (regra de UX: toda data mostra os dias).</summary>
public static class ExplicacaoTerritorial
{
    public static string DataComDias(DateOnly data, DateOnly hoje)
    {
        var dias = data.DayNumber - hoje.DayNumber;
        var relativo = dias switch
        {
            0 => "hoje",
            > 0 => dias == 1 ? "amanhã" : $"em {dias} dias",
            _ => dias == -1 ? "ontem" : $"há {-dias} dias"
        };
        return $"{TextoTela.Data(data)} ({relativo})";
    }

    /// <summary>"Resultado: Atribuído · Passo: Prioridade · MG Norte: vencedor; MG: perdeu por especificidade · Atributos: UF = MG".</summary>
    public static string Texto(string? json, IReadOnlyDictionary<Guid, string> territorios)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var texto = new StringBuilder();
            string Nome(JsonElement e) => e.ValueKind == JsonValueKind.String && Guid.TryParse(e.GetString(), out var id)
                ? territorios.GetValueOrDefault(id) ?? "?"
                : "?";
            if (r.TryGetProperty("passo", out var passo) && Enum.TryParse<PassoDecisaoTerritorial>(passo.GetString(), out var p))
                texto.Append("Decidido por: ").Append(NomePasso(p)).Append('.');
            if (r.TryGetProperty("estados", out var estados) && estados.ValueKind == JsonValueKind.Array)
                foreach (var e in estados.EnumerateArray())
                    if (e.TryGetProperty("territorio", out var t) && e.TryGetProperty("estado", out var s) &&
                        Enum.TryParse<EstadoCandidatoTerritorial>(s.GetString(), out var estado))
                        texto.Append(' ').Append(Nome(t)).Append(": ").Append(NomeEstado(estado)).Append('.');
            if (r.TryGetProperty("atributos", out var atributos) && atributos.ValueKind == JsonValueKind.Object &&
                atributos.TryGetProperty("lidos", out var lidos) && lidos.GetString() is { Length: > 0 } valores)
                texto.Append(" Dados lidos: ").Append(valores).Append('.');
            return texto.ToString().Trim();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    public static string NomePasso(PassoDecisaoTerritorial p) => p switch
    {
        PassoDecisaoTerritorial.Universo => "universo do mapa",
        PassoDecisaoTerritorial.UnicoCandidato => "único candidato",
        PassoDecisaoTerritorial.Fixacao => "fixação (exceção)",
        PassoDecisaoTerritorial.Prioridade => "prioridade",
        PassoDecisaoTerritorial.Especificidade => "especificidade (o território mais abaixo na árvore)",
        PassoDecisaoTerritorial.NaoExclusivo => "mapa não exclusivo (todos os candidatos)",
        PassoDecisaoTerritorial.Conflito => "conflito: empate que a regra não resolve",
        PassoDecisaoTerritorial.Inconsistencia => "inconsistência nas exceções",
        _ => "nenhuma regra atende"
    };

    public static string NomeEstado(EstadoCandidatoTerritorial e) => e switch
    {
        EstadoCandidatoTerritorial.Vencedor => "vencedor",
        EstadoCandidatoTerritorial.RetiradoPorExcecao => "retirado por exceção",
        EstadoCandidatoTerritorial.PerdeuParaExcecao => "perdeu para a fixação",
        EstadoCandidatoTerritorial.PerdeuPorPrioridade => "perdeu por prioridade",
        EstadoCandidatoTerritorial.PerdeuPorEspecificidade => "perdeu por especificidade",
        EstadoCandidatoTerritorial.Empatado => "empatado",
        EstadoCandidatoTerritorial.FixacaoEmConflito => "fixação em conflito",
        EstadoCandidatoTerritorial.FixacaoInvalida => "fixação inválida",
        _ => e.ToString()
    };
}

/// <summary>
/// Comercial › Operações territoriais (Fase 2b-1b; plano, seção N): lista TE-, criação (mapa, efeito, motivo), mudanças
/// (regra com o painel de filtros de Pessoas em grupos de inclusão e exclusão, exceções, estrutura), simulação com o "por
/// quê?" de cada cliente, e aplicar / cancelar / desfazer com motivo. Toda regra de negócio fica no servidor; a tela só
/// monta os pedidos e mostra as respostas (conflito = "Nada foi gravado: ...").
/// </summary>
public sealed partial class OperacoesTerritoriaisViewModel : CadastroViewModelBase<LinhaOperacaoTerritorial>
{
    /// <summary>Lista em colunas (padrão de tela de cadastro, 03/10/2026).</summary>
    protected override GradeCadastro<LinhaOperacaoTerritorial> CriarGradeDaLista() => new(
        "Operação", l => l.Item.Id, l => l.Item.Numero, l => l.Item.Motivo,
        ColunaCadastro<LinhaOperacaoTerritorial>.Texto("mapa", "Mapa", l => l.Item.Mapa, 140),
        ColunaCadastro<LinhaOperacaoTerritorial>.Curto("efeito", "Efeito", l => l.Efeito, 190, ordem: l => l.Item.EfeitoEm),
        ColunaCadastro<LinhaOperacaoTerritorial>.Texto("resultado", "Mudanças", l => l.Resultado, 180, ordem: l => l.TotalMudancas),
        ColunaCadastro<LinhaOperacaoTerritorial>.Curto("por", "Por", l => l.Por, 150),
        ColunaCadastro<LinhaOperacaoTerritorial>.Selo("situacao", "Situação", l => l.Situacao, l => l.Item.Situacao switch
        {
            SituacaoOperacaoTerritorial.Aplicada => "Sucesso",
            SituacaoOperacaoTerritorial.Cancelada => "Neutro",
            _ => "Aviso"
        }, 130))
    {
        OrdemTitulo = l => l.Item.Numero // TE-9 antes de TE-10 (ordem natural)
    };

    public const int ItensPorPagina = 200;

    private readonly TerritoriosApi _api;
    private readonly PessoasApi _pessoas;
    private readonly MunicipiosApi _municipios;
    private readonly SessaoCliente _sessao;
    private OperacoesTerritoriaisOpcoesDto _opcoes = new();
    private CatalogoFiltrosPessoasDto _catalogo = new();
    private IReadOnlyList<TerritorioResumoDto> _arvore = [];
    private Dictionary<Guid, string> _nomesTerritorios = new();

    public OperacoesTerritoriaisViewModel(TerritoriosApi api, PessoasApi pessoas, MunicipiosApi municipios, SessaoCliente sessao, IDialogos dialogos)
        : base(dialogos)
    {
        _api = api;
        _pessoas = pessoas;
        _municipios = municipios;
        _sessao = sessao;
    }

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Now);

    public bool PodePlanejar => _opcoes.PodePlanejar;
    public override bool PodeCriar => PodePlanejar;
    public bool PodeAplicarPermissao => _opcoes.PodeAplicar;

    // ------------------------------------------------------------------ Lista

    [ObservableProperty] private Opcao<Guid?>[] _filtrosMapa = [];
    [ObservableProperty] private Opcao<Guid?>? _filtroMapa;

    protected override string TextoDeBusca(LinhaOperacaoTerritorial item) => $"{item.Titulo} {item.Detalhe} {item.Situacao}";

    protected override async Task AntesDeListarAsync()
    {
        _opcoes = await _api.OpcoesOperacoesAsync();
        _catalogo = new CatalogoFiltrosPessoasDto { Campos = _opcoes.CamposRegra };
        var anterior = FiltroMapa?.Valor;
        FiltrosMapa = [new Opcao<Guid?>(null, "Todos os mapas"), .. _opcoes.Mapas.Select(m => new Opcao<Guid?>(m.Id, m.Ativo ? m.Nome : m.Nome + " (desativado)"))];
        FiltroMapa = FiltrosMapa.FirstOrDefault(m => m.Valor == anterior) ?? FiltrosMapa[0];
        MapasNovos = [.. _opcoes.Mapas.Where(m => m.Ativo).Select(m => new Opcao<Guid?>(m.Id, m.Nome))];
        OnPropertyChanged(nameof(PodePlanejar));
        OnPropertyChanged(nameof(PodeCriar));
        OnPropertyChanged(nameof(PodeAplicarPermissao));
        OnPropertyChanged(nameof(TextoParametros));
    }

    protected override async Task<IReadOnlyList<LinhaOperacaoTerritorial>> ListarAsync()
    {
        var hoje = Hoje;
        return [.. (await _api.ListarOperacoesAsync(FiltroMapa?.Valor)).Select(o => new LinhaOperacaoTerritorial(o, hoje))];
    }

    partial void OnFiltroMapaChanged(Opcao<Guid?>? oldValue, Opcao<Guid?>? newValue)
    {
        if (oldValue is not null && oldValue.Valor != newValue?.Valor) _ = RecarregarAsync();
    }

    public string TextoParametros => $"Datas no passado: até {_opcoes.Parametros.DiasRetroativosMaximo} dia(s) antes de hoje (Parâmetros territoriais). " +
                                     "Mover, encerrar e reativar território: só com efeito até hoje.";

    // ------------------------------------------------------------------ Ficha: nova operação

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhNova), nameof(TextoSalvar), nameof(TemOperacao), nameof(Titulo), nameof(Subtitulo), nameof(PodeEditar), nameof(PodeSimular),
                              nameof(PodeAplicar), nameof(PodeCancelar), nameof(PodeDesfazer), nameof(Aplicada), nameof(TemSimulacao),
                              nameof(TemBloqueio), nameof(TemAvisos), nameof(Trilha), nameof(TextoSimulacao), nameof(TextoDesatualizada),
                              nameof(TextoAutoria), nameof(EditadaPorOutros))]
    private OperacaoTerritorialDto? _operacao;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(EhNova), nameof(TextoSalvar))] private bool _criando;
    [ObservableProperty] private Opcao<Guid?>[] _mapasNovos = [];
    [ObservableProperty] private Opcao<Guid?>? _mapaNovo;
    [ObservableProperty] private string _efeitoEm = string.Empty;
    [ObservableProperty] private string _motivo = string.Empty;
    [ObservableProperty] private string _observacao = string.Empty;

    public bool EhNova => Criando && Operacao is null;
    public bool TemOperacao => Operacao is not null;
    public string Titulo => Operacao?.Numero ?? "Nova operação territorial";
    public string Subtitulo => Operacao is { } o
        ? $"{o.SituacaoNome} · {o.Mapa} · efeito {ExplicacaoTerritorial.DataComDias(o.EfeitoEm, Hoje)} · criada por {o.CriadaPor}" +
          (o.AplicadaPor is null ? string.Empty : $" · aplicada por {o.AplicadaPor} em {o.AplicadaEm?.ToLocalTime():dd/MM/yyyy HH:mm}")
        : "O número TE- nasce quando a operação é criada. Depois, inclua as mudanças, simule e aplique.";

    /// <summary>
    /// Rascunho editado por outras pessoas além de quem criou (qualquer PLANEJAR edita qualquer rascunho): destaque com quem
    /// e a última edição, para ninguém simular ou aplicar sem saber que outra pessoa mexeu. O detalhe está na história.
    /// </summary>
    public bool EditadaPorOutros => Operacao?.EditadaTambemPor.Count > 0;

    public string TextoAutoria => Operacao is { } o && o.EditadaTambemPor.Count > 0
        ? $"Criada por {o.CriadaPor} e editada também por {string.Join(", ", o.EditadaTambemPor)}" +
          (o.UltimaEdicaoPor is { } u ? $" (última edição: {u}, em {o.UltimaEdicaoEm?.ToLocalTime():dd/MM/yyyy HH:mm})" : string.Empty) +
          ". O que cada um mudou está na história, no fim da página."
        : string.Empty;

    /// <summary>A trilha Planejar → Simular → Aplicar, com o passo atual.</summary>
    public string Trilha => Operacao?.Situacao switch
    {
        SituacaoOperacaoTerritorial.Rascunho => "● Planejar  →  ○ Simular  →  ○ Aplicar",
        SituacaoOperacaoTerritorial.Simulada => "✓ Planejar  →  ● Simular" + (Operacao.Desatualizada ? " (desatualizada)" : string.Empty) + "  →  ○ Aplicar",
        SituacaoOperacaoTerritorial.Aplicada => "✓ Planejar  →  ✓ Simular  →  ✓ " + (Operacao.Agendada ? "Aplicada (agendada)" : "Aplicada (em vigor)"),
        SituacaoOperacaoTerritorial.Cancelada => $"Cancelada: {Operacao.CanceladaMotivo}",
        SituacaoOperacaoTerritorial.Desfeita => $"Desfeita antes do efeito: {Operacao.DesfeitaMotivo}",
        _ => string.Empty
    };

    public bool PodeEditar => Operacao?.PodeEditar ?? false;
    public bool PodeSimular => Operacao?.PodeSimular ?? false;
    public bool PodeAplicar => Operacao?.PodeAplicar ?? false;
    public bool PodeCancelar => Operacao?.PodeCancelar ?? false;
    public bool PodeDesfazer => Operacao?.PodeDesfazer ?? false;
    public bool Aplicada => Operacao?.Situacao == SituacaoOperacaoTerritorial.Aplicada;
    public bool TemSimulacao => Operacao?.SimulacaoAtual is not null;
    public bool TemBloqueio => Operacao?.BloqueadaPorId is not null;
    public bool TemAvisos => Operacao?.Avisos.Count > 0;
    public string TextoDesatualizada => Operacao is { Desatualizada: true } o ? "Simulação desatualizada: " + string.Join(" ", o.MotivosDesatualizada) + " Simule de novo." : string.Empty;

    public string TextoSimulacao => Operacao?.SimulacaoAtual is { } s
        ? $"Simulada em {s.SimuladaEm.ToLocalTime():dd/MM/yyyy HH:mm} por {s.SimuladaPor}: {s.Entram} entram, {s.Saem} saem, {s.Mudam} mudam de território, " +
          $"{s.OrigemAtualizada} mudam só a origem, {s.EmConflito} em conflito, {s.Inconsistencias} inconsistência(s) (bloqueiam). " +
          $"Desta operação: {s.DaOperacao}; divergências que já existiam (a aplicação corrige também): {s.Divergencias}."
        : "Ainda não simulada.";

    public ObservableCollection<LinhaMudancaTerritorial> Mudancas { get; } = new();
    public string TextoSalvar => EhNova ? "Criar operação" : "Salvar";
    public ObservableCollection<string> Avisos { get; } = new();
    public ObservableCollection<string> Historico { get; } = new();
    public ObservableCollection<string> Simulacoes { get; } = new();

    protected override Task NovoItemAsync()
    {
        if (!PodePlanejar) throw new ValidacaoException(["Você não tem permissão para planejar operações territoriais (ou o seu alcance não é Tudo)."]);
        Operacao = null;
        Criando = true;
        MapaNovo = FiltroMapa?.Valor is { } m ? MapasNovos.FirstOrDefault(x => x.Valor == m) ?? MapasNovos.FirstOrDefault() : MapasNovos.FirstOrDefault();
        EfeitoEm = TextoTela.Data(Hoje);
        Motivo = string.Empty;
        Observacao = string.Empty;
        LimparDetalhe();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Abre a ficha de uma operação pedida por outra tela ("Criar operação com estas", nas divergências): relê a lista (a
    /// operação acabou de nascer) e marca a linha dela; fora do filtro de mapa atual, abre mesmo assim.
    /// </summary>
    public async Task AbrirOperacaoAsync(Guid id)
    {
        await RecarregarAsync();
        Selecionado = Itens.FirstOrDefault(i => i.Item.Id == id) ?? new LinhaOperacaoTerritorial(new OperacaoTerritorialResumoDto { Id = id }, Hoje);
    }

    protected override async Task AbrirAsync(LinhaOperacaoTerritorial item)
    {
        Criando = false;
        await CarregarOperacaoAsync(item.Item.Id);
    }

    protected override object? DadosDaFicha() => Operacao is null ? new { MapaNovo?.Valor, EfeitoEm, Motivo, Observacao } : new { Operacao.Id, EfeitoEm, Motivo, Observacao };
    protected override bool FichaNova => Operacao is null;

    /// <summary>Navegação por registros: a linha guarda a operação em Item; a ficha aberta, em Operacao.</summary>
    protected override Guid? IdDoItem(LinhaOperacaoTerritorial item) => item.Item.Id;
    protected override Guid? IdDaFicha => Operacao?.Id ?? base.IdDaFicha;

    protected override async Task RecarregarFichaAsync()
    {
        if (Operacao is { } o) await CarregarOperacaoAsync(o.Id);
    }

    private void LimparDetalhe()
    {
        Mudancas.Clear();
        Avisos.Clear();
        Historico.Clear();
        Simulacoes.Clear();
        ClientesOperacao.Clear();
        TotalItens = 0;
        ItemSelecionado = null;
        Grupos.Clear();
    }

    private async Task CarregarOperacaoAsync(Guid id)
    {
        var op = await _api.ObterOperacaoAsync(id) ?? throw new ValidacaoException(["Esta operação territorial não existe mais."]);
        await MostrarOperacaoAsync(op);
    }

    private async Task MostrarOperacaoAsync(OperacaoTerritorialDto op)
    {
        // A árvore é relida a cada operação aberta, mesmo no mesmo mapa: um território criado, movido ou encerrado depois
        // (na tela de Territórios ou por outra operação) tem de aparecer nas listas "Território", "Para" e "Novo pai".
        _arvore = (await _api.ListarDoMapaAsync(op.MapaId)).Territorios;
        _nomesTerritorios = _arvore.ToDictionary(t => t.Id, t => t.Nome);
        MontarOpcoesDeTerritorio();
        Operacao = op;
        Criando = false;
        EfeitoEm = TextoTela.Data(op.EfeitoEm);
        Motivo = op.Motivo;
        Observacao = op.Observacao ?? string.Empty;
        LimparDetalhe();
        foreach (var m in op.Mudancas) Mudancas.Add(new LinhaMudancaTerritorial(m, op.PodeEditar, l => RetirarMudancaAsync(l.Dto)));
        foreach (var a in op.Avisos) Avisos.Add(a);
        foreach (var h in op.Historico) Historico.Add($"{h.DataHora.ToLocalTime():dd/MM/yyyy HH:mm} · {h.Usuario} · {h.Resumo}");
        if (op.SimulacaoAtual is not null || op.Situacao == SituacaoOperacaoTerritorial.Aplicada)
        {
            foreach (var s in await _api.SimulacoesAsync(op.Id))
                Simulacoes.Add($"{(s.Atual ? "● atual" : "○ anterior")} · {s.SimuladaEm.ToLocalTime():dd/MM/yyyy HH:mm} · {s.SimuladaPor} · " +
                               $"{s.Entram} entram, {s.Saem} saem, {s.Mudam} mudam, {s.Inconsistencias} inconsistência(s)");
            await ListarItensAsync(0);
        }
        TipoMudanca = TiposMudanca[0];
        if (MostraRegra && Grupos.Count == 0 && op.PodeEditar) IncluirGrupo(exclusao: false);
        MarcarFichaSemAlteracoes();
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (!TextoTela.TentarData(EfeitoEm, out var efeito) || efeito is null)
        {
            Mostrar("Informe a data de efeito (dd/mm/aaaa): o primeiro dia em que as mudanças valem.", TipoMensagem.Erro);
            return;
        }
        if (Operacao is null)
        {
            if (MapaNovo?.Valor is not { } mapa)
            {
                Mostrar("Escolha o mapa territorial.", TipoMensagem.Erro);
                return;
            }
            OperacaoTerritorialDto? criada = null;
            if (!await ExecutarAsync(async () => criada = await _api.CriarOperacaoAsync(new CriarOperacaoTerritorialRequisicao
                { MapaId = mapa, EfeitoEm = efeito.Value, Motivo = Motivo.Trim(), Observacao = TextoTela.Nulo(Observacao?.Trim()) }))) return;
            await AtualizarListaAposGravarAsync();
            await MostrarOperacaoAsync(criada!);
            Mostrar($"Operação {criada!.Numero} criada. Agora inclua as mudanças.", TipoMensagem.Sucesso);
            return;
        }
        var tinhaSimulacao = Operacao.SimulacaoAtual is not null;
        OperacaoTerritorialDto? alterada = null;
        if (!await ExecutarAsync(async () => alterada = await _api.AlterarOperacaoAsync(Operacao.Id, new AlterarOperacaoTerritorialRequisicao
            { Versao = Operacao.Versao, EfeitoEm = efeito.Value, Motivo = Motivo.Trim(), Observacao = TextoTela.Nulo(Observacao?.Trim()) }))) return;
        await AtualizarListaAposGravarAsync();
        await MostrarOperacaoAsync(alterada!);
        Mostrar("Alterações salvas." + (tinhaSimulacao && alterada!.SimulacaoAtual is null
            ? " A data mudou: simule de novo." : string.Empty), TipoMensagem.Sucesso);
    }

    // ------------------------------------------------------------------ Adicionar mudança

    public static readonly Opcao<int>[] TiposMudanca =
    [
        new((int)TipoMudancaTerritorial.NovaVersaoRegra, "Nova versão da regra de um território"),
        new((int)TipoMudancaTerritorial.EncerrarRegra, "Encerrar a regra de um território"),
        new((int)TipoMudancaTerritorial.Fixar, "Fixar um cliente num território (exceção)"),
        new((int)TipoMudancaTerritorial.Retirar, "Retirar um cliente de um território (exceção)"),
        new(TipoMoverCliente, "Mover um cliente de um território para outro (Retirar + Fixar)"),
        new((int)TipoMudancaTerritorial.EncerrarExcecao, "Encerrar uma exceção"),
        new((int)TipoMudancaTerritorial.MoverTerritorio, "Mover um território na árvore (com uso)"),
        new((int)TipoMudancaTerritorial.EncerrarTerritorio, "Encerrar um território (com uso)"),
        new((int)TipoMudancaTerritorial.ReativarTerritorio, "Reativar um território (com uso)")
    ];

    public const int TipoMoverCliente = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraRegra), nameof(MostraCliente), nameof(MostraDestino), nameof(MostraNovoPai), nameof(MostraExcecao),
                              nameof(MostraMotivoExcecao), nameof(RotuloTerritorio))]
    private Opcao<int> _tipoMudanca = TiposMudanca[0];

    [ObservableProperty] private Opcao<Guid?>[] _territorios = [];
    [ObservableProperty] private Opcao<Guid?>? _territorio;
    [ObservableProperty] private Opcao<Guid?>? _destino;
    [ObservableProperty] private Opcao<Guid?>[] _novosPais = [];
    [ObservableProperty] private Opcao<Guid?>? _novoPai;
    [ObservableProperty] private string _prioridade = string.Empty;
    [ObservableProperty] private string _buscaCliente = string.Empty;
    [ObservableProperty] private Opcao<Guid?>[] _clientes = [];
    [ObservableProperty] private Opcao<Guid?>? _cliente;
    [ObservableProperty] private Opcao<Guid?>[] _excecoes = [];
    [ObservableProperty] private Opcao<Guid?>? _excecao;
    [ObservableProperty] private string _motivoExcecao = string.Empty;
    [ObservableProperty] private string _fimExcecao = string.Empty;

    public ObservableCollection<GrupoRegraEdicao> Grupos { get; } = new();

    private TipoMudancaTerritorial? Tipo => TipoMudanca.Valor == TipoMoverCliente ? null : (TipoMudancaTerritorial)TipoMudanca.Valor;
    public bool MostraRegra => Tipo == TipoMudancaTerritorial.NovaVersaoRegra;
    public bool MostraCliente => Tipo is TipoMudancaTerritorial.Fixar or TipoMudancaTerritorial.Retirar || TipoMudanca.Valor == TipoMoverCliente;
    public bool MostraDestino => TipoMudanca.Valor == TipoMoverCliente;
    public bool MostraNovoPai => Tipo == TipoMudancaTerritorial.MoverTerritorio;
    public bool MostraExcecao => Tipo == TipoMudancaTerritorial.EncerrarExcecao;
    public bool MostraMotivoExcecao => MostraCliente;
    public string RotuloTerritorio => TipoMudanca.Valor == TipoMoverCliente ? "De (território atual)" : "Território";

    private void MontarOpcoesDeTerritorio()
    {
        var caminhos = ArvoreTerritorios.Linhas(_arvore, incluirEncerrados: true).ToDictionary(l => l.Item.Id, l => l.Caminho);
        Opcao<Guid?> Opcao(TerritorioResumoDto t) => new(t.Id, (caminhos.GetValueOrDefault(t.Id) is { } c ? $"{c} › {t.Nome}" : t.Nome) +
                                                            (t.Situacao == SituacaoTerritorio.Encerrado ? " (encerrado)" : string.Empty));
        Territorios = [.. _arvore.Select(Opcao).OrderBy(o => o.Texto, StringComparer.CurrentCultureIgnoreCase)];
        NovosPais = [ArvoreTerritorios.PrimeiroNivel, .. _arvore.Where(t => t.Situacao == SituacaoTerritorio.Ativo).Select(Opcao)
            .OrderBy(o => o.Texto, StringComparer.CurrentCultureIgnoreCase)];
    }

    partial void OnTipoMudancaChanged(Opcao<int> value)
    {
        if (MostraRegra && Grupos.Count == 0) IncluirGrupo(exclusao: false);
    }

    partial void OnTerritorioChanged(Opcao<Guid?>? value)
    {
        if (MostraExcecao) _ = CarregarExcecoesAsync(value?.Valor);
    }

    private async Task CarregarExcecoesAsync(Guid? territorio)
    {
        Excecoes = [];
        Excecao = null;
        if (territorio is not { } id) return;
        TerritorioMotorDto? motor = null;
        if (!await ExecutarAsync(async () => motor = await _api.MotorDoTerritorioAsync(id))) return;
        Excecoes = [.. motor!.Excecoes.Where(x => x.Vigente).Select(x => new Opcao<Guid?>(x.Id,
            $"{(x.Tipo == TipoExcecaoTerritorio.Fixar ? "Fixar" : "Retirar")} {x.Pessoa} desde {TextoTela.Data(x.InicioEm)}" +
            (x.FimEm is { } f ? $" até {TextoTela.Data(f)}" : string.Empty) + $" ({x.Motivo})"))];
        Excecao = Excecoes.FirstOrDefault();
        if (Excecoes.Length == 0) Mostrar("Este território não tem exceção vigente.", TipoMensagem.Informacao);
    }

    [RelayCommand]
    private void AdicionarGrupoInclusao() => IncluirGrupo(exclusao: false);

    [RelayCommand]
    private void AdicionarGrupoExclusao() => IncluirGrupo(exclusao: true);

    private void IncluirGrupo(bool exclusao)
    {
        var grupo = new GrupoRegraEdicao(exclusao, _catalogo, uf => _municipios.ListarDaUfAsync(uf)) { AoRemover = g => Grupos.Remove(g) };
        Grupos.Add(grupo);
    }

    [RelayCommand]
    private async Task BuscarClienteAsync()
    {
        if (BuscaCliente.Trim().Length < 2)
        {
            Mostrar("Digite ao menos 2 letras (nome, código, CPF ou CNPJ) para buscar o cliente.", TipoMensagem.Aviso);
            return;
        }
        List<PessoaResumo>? achadas = null;
        if (!await ExecutarAsync(async () => achadas = await _pessoas.ListarAsync(new FiltroPessoas { Texto = BuscaCliente.Trim(), Limite = 30 }))) return;
        Clientes = [.. achadas!.Select(p => new Opcao<Guid?>(p.Id, $"{p.Codigo} · {p.Nome}"))];
        Cliente = Clientes.FirstOrDefault();
        if (Clientes.Length == 0) Mostrar("Nenhum cadastro encontrado.", TipoMensagem.Informacao);
    }

    [RelayCommand]
    private async Task IncluirMudancaAsync()
    {
        if (Operacao is not { PodeEditar: true } op) return;
        DateOnly? fim = null;
        if (MostraMotivoExcecao && !string.IsNullOrWhiteSpace(FimExcecao))
        {
            if (!TextoTela.TentarData(FimExcecao, out fim))
            {
                Mostrar("Fim da exceção: data inválida (dd/mm/aaaa) ou deixe vazio.", TipoMensagem.Erro);
                return;
            }
        }
        int? prioridade = null;
        if (MostraRegra && !string.IsNullOrWhiteSpace(Prioridade))
        {
            if (!int.TryParse(Prioridade.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var p) || p < 1)
            {
                Mostrar("Prioridade: um número a partir de 1 (1 = mais alta), ou vazia (sem prioridade).", TipoMensagem.Erro);
                return;
            }
            prioridade = p;
        }
        OperacaoTerritorialDto? resposta = null;
        var ok = await ExecutarAsync(async () =>
        {
            if (TipoMudanca.Valor == TipoMoverCliente)
                resposta = await _api.MoverClienteAsync(op.Id, new MoverClienteTerritorialRequisicao
                {
                    Versao = op.Versao, PessoaId = Cliente?.Valor ?? Guid.Empty, DeTerritorioId = Territorio?.Valor ?? Guid.Empty,
                    ParaTerritorioId = Destino?.Valor ?? Guid.Empty, Motivo = MotivoExcecao.Trim(), FimEm = fim
                });
            else
                resposta = await _api.IncluirMudancaAsync(op.Id, new IncluirMudancaTerritorialRequisicao
                {
                    Versao = op.Versao, Tipo = Tipo!.Value, TerritorioId = Territorio?.Valor,
                    PessoaId = MostraCliente ? Cliente?.Valor : null, ExcecaoId = MostraExcecao ? Excecao?.Valor : null,
                    Grupos = MostraRegra ? new GruposRegraTerritorioDto
                    {
                        Inclusao = [.. Grupos.Where(g => !g.Exclusao).Select(g => g.ParaDto())],
                        Exclusao = [.. Grupos.Where(g => g.Exclusao).Select(g => g.ParaDto())]
                    } : null,
                    Prioridade = prioridade, Motivo = MostraMotivoExcecao ? MotivoExcecao.Trim() : null, FimEm = fim,
                    NovoPaiId = MostraNovoPai ? NovoPai?.Valor : null
                });
        });
        if (!ok) return;
        await MostrarOperacaoAsync(resposta!);
        MotivoExcecao = string.Empty;
        FimExcecao = string.Empty;
        Prioridade = string.Empty;
        Mostrar("Mudança incluída. A operação voltou para Rascunho: simule para conferir o resultado.", TipoMensagem.Sucesso);
    }

    private async Task RetirarMudancaAsync(MudancaTerritorialDto mudanca)
    {
        if (Operacao is not { PodeEditar: true } op) return;
        if (!await ConfirmarAsync("Retirar mudança", $"Retirar \"{mudanca.Descricao}\" da operação {op.Numero}?", "Retirar", "Manter")) return;
        OperacaoTerritorialDto? resposta = null;
        if (!await ExecutarAsync(async () => resposta = await _api.RetirarMudancaAsync(op.Id, mudanca.Id, op.Versao))) return;
        await MostrarOperacaoAsync(resposta!);
        Mostrar("Mudança retirada.", TipoMensagem.Sucesso);
    }

    // ------------------------------------------------------------------ Simular, aplicar, cancelar, desfazer

    [RelayCommand]
    private async Task SimularAsync()
    {
        if (Operacao is not { PodeSimular: true } op) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações do cabeçalho antes de simular.", TipoMensagem.Aviso);
            return;
        }
        OperacaoTerritorialDto? resposta = null;
        if (!await ExecutarAsync(async () => resposta = await _api.SimularAsync(op.Id, op.Versao))) return;
        await AtualizarListaAposGravarAsync();
        await MostrarOperacaoAsync(resposta!);
        Mostrar("Simulação pronta: nada foi gravado nos clientes. Confira abaixo e aplique.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private async Task AplicarAsync()
    {
        if (Operacao is not { PodeAplicar: true, SimulacaoAtual: { } s } op) return;
        var futuro = op.EfeitoEm > Hoje
            ? " Efeito futuro: até lá, operações com efeito anterior neste mapa ficam bloqueadas (desfazer esta é o caminho para corrigi-las)."
            : " Depois do efeito, só se corrige com outra operação.";
        if (!await ConfirmarAsync($"Aplicar {op.Numero}",
                $"Efeito {ExplicacaoTerritorial.DataComDias(op.EfeitoEm, Hoje)}: {s.Entram} entram, {s.Saem} saem, {s.Mudam} mudam de território " +
                $"({s.DaOperacao} desta operação e {s.Divergencias} divergência(s) que já existiam).{futuro} O sistema confere tudo de novo e só grava se o " +
                "resultado for exatamente o simulado.", "Aplicar", "Cancelar")) return;
        OperacaoTerritorialDto? resposta = null;
        var ok = await ExecutarAsync(async () => resposta = await _api.AplicarAsync(op.Id, op.Versao, s.Id));
        await AtualizarListaAposGravarAsync();
        if (!ok)
        {
            var mensagem = Mensagem;
            await CarregarOperacaoAsync(op.Id); // a tentativa recusada aparece no histórico
            Mostrar(mensagem, TipoMensagem.Erro);
            return;
        }
        await MostrarOperacaoAsync(resposta!);
        Mostrar($"Operação {resposta!.Numero} aplicada.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private async Task CancelarOperacaoAsync()
    {
        if (Operacao is not { PodeCancelar: true } op) return;
        var motivo = await PerguntarAsync($"Cancelar {op.Numero}", "A operação é encerrada sem mudar nenhum cliente; o número TE- fica no histórico. Motivo (obrigatório):",
            "Cancelar operação", "Voltar", "Ex.: planejamento substituído pela TE-…", 250);
        if (string.IsNullOrWhiteSpace(motivo)) return;
        OperacaoTerritorialDto? resposta = null;
        if (!await ExecutarAsync(async () => resposta = await _api.CancelarAsync(op.Id, op.Versao, motivo.Trim()))) return;
        await AtualizarListaAposGravarAsync();
        await MostrarOperacaoAsync(resposta!);
        Mostrar("Operação cancelada.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private async Task DesfazerAsync() => await DesfazerOperacaoAsync(Operacao);

    /// <summary>RT-1: "Desfazer TE-XXXX" a partir da operação bloqueada.</summary>
    [RelayCommand]
    private async Task DesfazerBloqueioAsync()
    {
        if (Operacao?.BloqueadaPorId is not { } id) return;
        OperacaoTerritorialDto? bloqueio = null;
        if (!await ExecutarAsync(async () => bloqueio = await _api.ObterOperacaoAsync(id))) return;
        if (bloqueio is null) return;
        var atual = Operacao;
        if (await DesfazerOperacaoAsync(bloqueio) && atual is not null) await CarregarOperacaoAsync(atual.Id);
    }

    private async Task<bool> DesfazerOperacaoAsync(OperacaoTerritorialDto? op)
    {
        if (op is not { PodeDesfazer: true }) return false;
        var motivo = await PerguntarAsync($"Desfazer {op.Numero}",
            $"A operação ainda não entrou em vigor (efeito {ExplicacaoTerritorial.DataComDias(op.EfeitoEm, Hoje)}). Desfazer anula o que ela abriu e reabre o que ela " +
            "fechou, exatamente como era; nada é apagado. Motivo (obrigatório):", "Desfazer", "Voltar", "Ex.: efeito errado", 250);
        if (string.IsNullOrWhiteSpace(motivo)) return false;
        OperacaoTerritorialDto? resposta = null;
        if (!await ExecutarAsync(async () => resposta = await _api.DesfazerAsync(op.Id, op.Versao, motivo.Trim()))) return false;
        await AtualizarListaAposGravarAsync();
        if (Operacao?.Id == op.Id) await MostrarOperacaoAsync(resposta!);
        Mostrar($"Operação {op.Numero} desfeita.", TipoMensagem.Sucesso);
        return true;
    }

    // ------------------------------------------------------------------ Clientes da simulação / do resultado

    public ObservableCollection<LinhaItemTerritorial> ClientesOperacao { get; } = new();

    public static readonly Opcao<EfeitoNoCliente?>[] FiltrosEfeito =
    [
        new(null, "Todos os efeitos"), new(EfeitoNoCliente.Entra, "Entram"), new(EfeitoNoCliente.Sai, "Saem"), new(EfeitoNoCliente.Muda, "Mudam de território"),
        new(EfeitoNoCliente.OrigemAtualizada, "Mesma área, outra origem"), new(EfeitoNoCliente.Bloqueado, "Bloqueados (inconsistência)"),
        new(EfeitoNoCliente.Permanece, "Permanecem (em conflito)")
    ];

    [ObservableProperty] private Opcao<EfeitoNoCliente?> _filtroEfeito = FiltrosEfeito[0];
    [ObservableProperty] private bool _soProblemas;
    [ObservableProperty] private string _buscaItens = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TextoItens), nameof(TemMaisItens))] private int _totalItens;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemItemSelecionado))] private LinhaItemTerritorial? _itemSelecionado;
    private bool _filtradoPeloAlcance;

    public bool TemItemSelecionado => ItemSelecionado is not null;
    public bool TemMaisItens => ClientesOperacao.Count < TotalItens;
    public string TextoItens => $"{ClientesOperacao.Count} de {TotalItens} cliente(s)" + (_filtradoPeloAlcance ? " (só os do seu alcance)" : string.Empty);

    [RelayCommand]
    private Task FiltrarItensAsync() => ListarItensAsync(0);

    [RelayCommand]
    private Task MaisItensAsync() => ListarItensAsync(ClientesOperacao.Count);

    private async Task ListarItensAsync(int pular)
    {
        if (Operacao is not { } op) return;
        var filtro = new FiltroItensOperacaoTerritorialDto
        {
            Efeito = FiltroEfeito.Valor, SoProblemas = SoProblemas, Texto = TextoTela.Nulo(BuscaItens?.Trim()), Pular = pular, Quantidade = ItensPorPagina
        };
        PaginaItensOperacaoTerritorialDto? pagina = null;
        var ok = await ExecutarAsync(async () => pagina = op.Situacao == SituacaoOperacaoTerritorial.Aplicada
            ? await _api.ItensAplicadosAsync(op.Id, filtro)
            : op.SimulacaoAtual is { } s ? await _api.ItensSimulacaoAsync(s.Id, filtro) : new PaginaItensOperacaoTerritorialDto());
        if (!ok) return;
        if (pular == 0) ClientesOperacao.Clear();
        foreach (var i in pagina!.Itens) ClientesOperacao.Add(new LinhaItemTerritorial(i, _nomesTerritorios));
        _filtradoPeloAlcance = pagina.FiltradoPeloAlcance;
        TotalItens = pagina.Total;
        OnPropertyChanged(nameof(TextoItens));
        OnPropertyChanged(nameof(TemMaisItens));
    }
}
