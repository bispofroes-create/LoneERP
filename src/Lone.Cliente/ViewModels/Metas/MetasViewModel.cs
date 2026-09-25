using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.Sessao;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Metas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Metas;

public sealed class LinhaMeta
{
    public LinhaMeta(MetaResumoDto item) => Item = item;
    public MetaResumoDto Item { get; }
    public string Nome => Item.Nome;
    public string Detalhe => $"{TextoTela.Data(Item.InicioEm)} a {TextoTela.Data(Item.FimEm)} · {Item.Participantes} participante(s)";
    public string Situacao => Item.Ativo ? OpcoesMetas.Nome(Item.Situacao) : "Cancelada";
}

/// <summary>
/// Ficha da meta. A estrutura (período, itens, faixas, participantes e alvos) só muda no rascunho; publicada ou em
/// apuração, só o realizado dos itens informados é lançado; fechada, tudo fica congelado (reabrir exige motivo).
/// </summary>
public sealed partial class MetaEdicao : ObservableObject
{
    private readonly MetaOpcoesDto _opcoes;

    private MetaEdicao(MetaDto d, bool novo, MetaOpcoesDto opcoes, bool podeGerenciar, bool podeLancar)
    {
        _opcoes = opcoes;
        Id = d.Id;
        Novo = novo;
        Versao = d.Versao;
        Situacao = d.Situacao;
        Ativo = d.Ativo;
        FechadaTexto = d.FechadaEm is { } f ? $"Fechada em {TextoTela.DataHora(f.ToLocalTime())} por {d.FechadaPor}." : string.Empty;
        EditarEstrutura = podeGerenciar && Ativo && Situacao == SituacaoMeta.Rascunho;
        Lancavel = podeLancar && Ativo && Situacao is SituacaoMeta.Publicada or SituacaoMeta.EmApuracao;
        _nome = d.Nome;
        _descricao = d.Descricao ?? string.Empty;
        _inicioEm = TextoTela.Data(d.InicioEm == default ? null : d.InicioEm);
        _fimEm = TextoTela.Data(d.FimEm == default ? null : d.FimEm);
        _limite = TextoTela.Decimal(d.LimiteAtingimento);

        foreach (var i in d.Itens.OrderBy(i => i.Ordem))
            IncluirItem(new MetaItemFormulario(i.Id, opcoes.Indicadores, i.IndicadorId, i.Peso, EditarEstrutura));
        foreach (var f2 in d.Faixas.OrderBy(f2 => f2.InicioPercentual)) IncluirFaixa(new MetaFaixaFormulario(f2, EditarEstrutura));
        foreach (var p in d.Participantes) IncluirParticipante(new MetaParticipanteFormulario(p, opcoes.Participantes, EditarEstrutura), d.Alvos);
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; }
    public SituacaoMeta Situacao { get; }
    public bool Ativo { get; }
    public string FechadaTexto { get; }
    public bool EditarEstrutura { get; }
    public bool Lancavel { get; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome;
    [ObservableProperty] private string _descricao;
    [ObservableProperty] private string _inicioEm;
    [ObservableProperty] private string _fimEm;
    [ObservableProperty] private string _limite;

    public ObservableCollection<MetaItemFormulario> Itens { get; } = new();
    public ObservableCollection<MetaFaixaFormulario> Faixas { get; } = new();
    public ObservableCollection<MetaParticipanteFormulario> Participantes { get; } = new();

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Nova meta" : Nome;
    public string SituacaoTexto => !Ativo ? "Cancelada" : Situacao switch
    {
        SituacaoMeta.Rascunho => "Rascunho: monte a meta e publique. Depois de publicada, a estrutura não muda.",
        SituacaoMeta.Publicada => "Publicada: a equipe já pode acompanhar. Lance o realizado dos itens informados.",
        SituacaoMeta.EmApuracao => "Em apuração: confira o resultado e feche (aprovação) para congelar.",
        SituacaoMeta.Fechada => "Fechada: resultado congelado (base das comissões). Reabrir exige permissão e motivo.",
        _ => string.Empty
    };

    public IReadOnlyList<MetaItemFormulario> ItensInformados => Itens.Where(i => i.Informado && i.Indicador.Valor is not null).ToList();

    public static MetaEdicao Criar(MetaOpcoesDto opcoes, bool podeGerenciar)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicio = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1);
        var dto = new MetaDto
        {
            Id = IdSequencial.Novo(), InicioEm = inicio, FimEm = inicio.AddMonths(1).AddDays(-1),
            Faixas =
            [
                new() { InicioPercentual = 80, Nome = "Bronze", PercentualPremio = 50 },
                new() { InicioPercentual = 100, Nome = "Prata", PercentualPremio = 100 },
                new() { InicioPercentual = 120, Nome = "Ouro", PercentualPremio = 150 }
            ]
        };
        return new MetaEdicao(dto, true, opcoes, podeGerenciar, false);
    }

    public static MetaEdicao De(MetaDto d, MetaOpcoesDto opcoes, bool podeGerenciar, bool podeLancar) => new(d, false, opcoes, podeGerenciar, podeLancar);

    // ---- Listas ----

    public void AdicionarItem() => IncluirItem(new MetaItemFormulario(IdSequencial.Novo(), _opcoes.Indicadores, null, null, true));
    public void AdicionarFaixa() => IncluirFaixa(new MetaFaixaFormulario(new MetaFaixaDto(), true));

    public void AdicionarParticipante() => IncluirParticipante(
        new MetaParticipanteFormulario(new MetaParticipanteDto { Nivel = NivelParticipante.Colaborador }, _opcoes.Participantes, true), []);

    private void IncluirItem(MetaItemFormulario item)
    {
        item.AoRemover = () => { Itens.Remove(item); SincronizarAlvos(); };
        item.AoMudar = SincronizarAlvos;
        Itens.Add(item);
        SincronizarAlvos();
    }

    private void IncluirFaixa(MetaFaixaFormulario faixa)
    {
        faixa.AoRemover = () => Faixas.Remove(faixa);
        Faixas.Add(faixa);
    }

    private void IncluirParticipante(MetaParticipanteFormulario p, IReadOnlyList<MetaAlvoDto> gravados)
    {
        p.AoRemover = () => Participantes.Remove(p);
        foreach (var item in Itens)
            p.Alvos.Add(NovoAlvo(item, gravados.FirstOrDefault(a => a.ParticipanteId == p.Id && a.ItemId == item.Id)));
        Participantes.Add(p);
    }

    private MetaAlvoFormulario NovoAlvo(MetaItemFormulario item, MetaAlvoDto? gravado)
    {
        var alvo = new MetaAlvoFormulario(gravado?.Id ?? IdSequencial.Novo(), item)
        {
            PodeEditarAlvo = EditarEstrutura,
            PodeLancar = Lancavel && item.Informado,
            Alvo = TextoTela.Numero(gravado?.Alvo),
            Realizado = TextoTela.Numero(gravado?.Realizado)
        };
        if (gravado?.Realizado is { } r)
            alvo.RealizadoInfo = $"Realizado {TextoTela.Numero(r)} ({OpcoesMetas.Nome(gravado.OrigemRealizado)}" +
                                 (gravado.RealizadoEm is { } em ? $" em {TextoTela.DataHora(em.ToLocalTime())} por {gravado.RealizadoPor})" : ")");
        else if (!item.Informado && item.Indicador.Valor is not null)
            alvo.RealizadoInfo = "Realizado calculado pelo cadastro (veja a apuração).";
        return alvo;
    }

    /// <summary>Um alvo por item em cada participante (mantém o que já foi digitado).</summary>
    private void SincronizarAlvos()
    {
        foreach (var p in Participantes)
        {
            var atuais = p.Alvos.ToDictionary(a => a.ItemId);
            p.Alvos.Clear();
            foreach (var item in Itens)
            {
                var novo = NovoAlvo(item, null);
                if (atuais.TryGetValue(item.Id, out var antigo))
                {
                    novo = new MetaAlvoFormulario(antigo.Id, item)
                    {
                        PodeEditarAlvo = antigo.PodeEditarAlvo, PodeLancar = Lancavel && item.Informado,
                        Alvo = antigo.Alvo, Realizado = antigo.Realizado, RealizadoInfo = antigo.RealizadoInfo
                    };
                }
                p.Alvos.Add(novo);
            }
        }
        OnPropertyChanged(nameof(ItensInformados));
    }

    // ---- Validação e conversão ----

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome da meta.");
        if (!TextoTela.TentarData(InicioEm, out var i) || i is null) erros.Add("Informe o início do período (dd/mm/aaaa).");
        if (!TextoTela.TentarData(FimEm, out var f) || f is null) erros.Add("Informe o fim do período (dd/mm/aaaa).");
        if (!TextoTela.TentarDecimal(Limite, out _)) erros.Add("Limite de atingimento inválido.");
        if (Itens.Any(x => x.Indicador.Valor is null)) erros.Add("Escolha o indicador de cada item.");
        if (Itens.Any(x => !TextoTela.TentarDecimal(x.Peso, out var p) || p is null)) erros.Add("Informe o peso de cada item.");
        if (Faixas.Any(x => !TextoTela.TentarDecimal(x.Inicio, out var v) || v is null || !TextoTela.TentarDecimal(x.Premio, out _)))
            erros.Add("Confira o início e o prêmio (%) de cada faixa.");
        if (Participantes.Any(x => x.Referencia.Valor is null)) erros.Add("Escolha cada participante.");
        if (Participantes.SelectMany(p => p.Alvos).Any(a => !TextoTela.TentarDecimal(a.Alvo, out _) || !TextoTela.TentarDecimal(a.Realizado, out _)))
            erros.Add("Há alvo ou realizado inválido (use vírgula para decimais).");
        return erros;
    }

    public MetaDto ParaDto()
    {
        TextoTela.TentarData(InicioEm, out var inicio);
        TextoTela.TentarData(FimEm, out var fim);
        TextoTela.TentarDecimal(Limite, out var limite);
        return new MetaDto
        {
            Id = Id, Versao = Versao, Nome = Nome.Trim(), Descricao = TextoTela.Nulo(Descricao)?.Trim(),
            InicioEm = inicio ?? default, FimEm = fim ?? default, LimiteAtingimento = limite ?? 150, Situacao = Situacao, Ativo = Ativo,
            Itens = Itens.Select((x, n) =>
            {
                TextoTela.TentarDecimal(x.Peso, out var peso);
                return new MetaItemDto { Id = x.Id, IndicadorId = x.Indicador.Valor ?? Guid.Empty, Peso = peso ?? 0, Ordem = n };
            }).ToList(),
            Faixas = Faixas.Select(x =>
            {
                TextoTela.TentarDecimal(x.Inicio, out var ini);
                TextoTela.TentarDecimal(x.Premio, out var premio);
                return new MetaFaixaDto { Id = x.Id, InicioPercentual = ini ?? 0, Nome = x.Nome.Trim(), PercentualPremio = premio ?? 0 };
            }).ToList(),
            Participantes = Participantes.Select(p => new MetaParticipanteDto { Id = p.Id, Nivel = p.Nivel.Valor, ReferenciaId = p.ReferenciaId }).ToList(),
            Alvos = Participantes.SelectMany(p => p.Alvos.Select(a =>
            {
                TextoTela.TentarDecimal(a.Alvo, out var alvo);
                TextoTela.TentarDecimal(a.Realizado, out var realizado);
                return new MetaAlvoDto { Id = a.Id, ParticipanteId = p.Id, ItemId = a.ItemId, Alvo = alvo ?? 0, Realizado = realizado };
            })).ToList()
        };
    }

    /// <summary>Lançamentos dos itens informados (o que está na tela).</summary>
    public List<LancamentoRealizadoDto> Lancamentos() =>
        Participantes.SelectMany(p => p.Alvos.Where(a => a.PodeLancar).Select(a =>
        {
            TextoTela.TentarDecimal(a.Realizado, out var valor);
            return new LancamentoRealizadoDto { ParticipanteId = p.Id, ItemId = a.ItemId, Valor = valor };
        })).ToList();
}

/// <summary>Linha da apuração (um participante).</summary>
public sealed class LinhaApuracao
{
    public LinhaApuracao(ApuracaoParticipanteDto d)
    {
        Nome = $"{d.Nome} ({OpcoesMetas.Nome(d.Nivel)})";
        Nota = $"{TextoTela.Decimal(d.Nota)}%";
        Faixa = d.Faixa is null ? "Abaixo das faixas" : $"{d.Faixa} · prêmio {TextoTela.Decimal(d.PercentualPremio ?? 0)}%";
        Itens = string.Join("  ·  ", d.Itens.Select(i =>
            $"{i.Indicador}: {(i.Realizado is null ? "—" : TextoTela.Numero(i.Realizado))} / {TextoTela.Numero(i.Alvo)}" +
            (i.Atingimento is { } a ? $" ({TextoTela.Decimal(a)}%)" : string.Empty)));
    }

    public string Nome { get; }
    public string Nota { get; }
    public string Faixa { get; }
    public string Itens { get; }
}

/// <summary>Metas: montagem, publicação, realizado (manual ou CSV), apuração e fechamento aprovado.</summary>
public sealed partial class MetasViewModel : CadastroViewModelBase<LinhaMeta>
{
    private readonly MetasApi _api;
    private readonly SessaoCliente _sessao;
    private MetaOpcoesDto _opcoes = new();

    public MetasViewModel(MetasApi api, SessaoCliente sessao, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
        _sessao = sessao;
    }

    private bool PodeGerenciar => _sessao.Possui(Permissoes.Metas.Gerenciar);
    private bool PodeLancarRealizado => _sessao.Possui(Permissoes.Metas.LancarRealizado);
    private bool PodeFechar => _sessao.Possui(Permissoes.Metas.Fechar);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeSalvar), nameof(PodePublicar), nameof(PodeIniciarApuracao), nameof(PodeFecharMeta),
                              nameof(PodeReabrir), nameof(PodeVoltarRascunho), nameof(PodeCancelar), nameof(PodeLancar), nameof(PodeNovo), nameof(MostrarApuracao))]
    private MetaEdicao? _formulario;

    public bool PodeNovo => PodeGerenciar;
    public bool MostrarApuracao => Formulario is { Novo: false };
    public bool PodeSalvar => Formulario is { EditarEstrutura: true };
    public bool PodeLancar => Formulario is { Lancavel: true };
    public bool PodePublicar => PodeGerenciar && Formulario is { Novo: false, Ativo: true, Situacao: SituacaoMeta.Rascunho };
    public bool PodeVoltarRascunho => PodeGerenciar && Formulario is { Ativo: true, Situacao: SituacaoMeta.Publicada };
    public bool PodeIniciarApuracao => PodeGerenciar && Formulario is { Ativo: true, Situacao: SituacaoMeta.Publicada };
    public bool PodeFecharMeta => PodeFechar && Formulario is { Ativo: true, Situacao: SituacaoMeta.EmApuracao };
    public bool PodeReabrir => PodeFechar && Formulario is { Ativo: true, Situacao: SituacaoMeta.Fechada };
    public bool PodeCancelar => PodeGerenciar && Formulario is { Novo: false, Ativo: true, Situacao: SituacaoMeta.Rascunho };

    // ---- Apuração ----
    public ObservableCollection<LinhaApuracao> Apuracao { get; } = new();
    [ObservableProperty] private string _apuracaoAvisos = string.Empty;

    // ---- Importação ----
    [ObservableProperty] private MetaItemFormulario? _itemImportacao;
    [ObservableProperty] private string _conteudoImportacao = string.Empty;
    public ObservableCollection<string> LinhasImportacao { get; } = new();
    [ObservableProperty] private bool _importacaoConferida;

    protected override string TextoDeBusca(LinhaMeta item) => item.Nome + " " + item.Detalhe + " " + item.Situacao;

    protected override async Task AntesDeListarAsync() => _opcoes = await _api.ListarOpcoesAsync();

    protected override async Task<IReadOnlyList<LinhaMeta>> ListarAsync() =>
        (await _api.ListarAsync(incluirInativas: true)).OrderBy(m => !m.Ativo).ThenByDescending(m => m.InicioEm).Select(m => new LinhaMeta(m)).ToList();

    protected override async Task AbrirAsync(LinhaMeta item) => Abrir(await ObterAsync(item.Item.Id));

    protected override Task NovoItemAsync()
    {
        Abrir(MetaEdicao.Criar(_opcoes, PodeGerenciar));
        return Task.CompletedTask;
    }

    private void Abrir(MetaEdicao f)
    {
        Formulario = f;
        Apuracao.Clear();
        ApuracaoAvisos = string.Empty;
        LinhasImportacao.Clear();
        ImportacaoConferida = false;
        ConteudoImportacao = string.Empty;
        ItemImportacao = f.ItensInformados.FirstOrDefault();
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is { Novo: false } f) Abrir(await ObterAsync(f.Id));
    }

    private async Task<MetaEdicao> ObterAsync(Guid id) =>
        MetaEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Esta meta não existe mais."]), _opcoes, PodeGerenciar, PodeLancarRealizado);

    private void Gravado(MetaDto meta, string mensagem)
    {
        Abrir(MetaEdicao.De(meta, _opcoes, PodeGerenciar, PodeLancarRealizado));
        MarcarFichaSemAlteracoes();
        Mostrar(mensagem, TipoMensagem.Sucesso);
    }

    [RelayCommand] private void AdicionarItem() => Formulario?.AdicionarItem();
    [RelayCommand] private void AdicionarFaixa() => Formulario?.AdicionarFaixa();
    [RelayCommand] private void AdicionarParticipante() => Formulario?.AdicionarParticipante();

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { EditarEstrutura: true } f) return;
        if (f.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }
        MetaDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(f.ParaDto()))) return;
        await AtualizarListaAposGravarAsync();
        Gravado(salvo!, f.Novo ? "Meta criada (rascunho)." : "Rascunho salvo.");
    }

    [RelayCommand]
    private async Task LancarAsync()
    {
        if (Formulario is not { Lancavel: true } f) return;
        if (f.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }
        MetaDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.LancarRealizadoAsync(f.Id,
                new LancarRealizadoRequisicao { Versao = f.Versao, Lancamentos = f.Lancamentos() })))
            return;
        Gravado(salvo!, "Realizado gravado.");
    }

    [RelayCommand] private Task PublicarAsync() => MudarSituacaoAsync(SituacaoMeta.Publicada, "Meta publicada.", perguntarMotivo: false,
        "Publicar a meta? Depois disso itens, pesos, faixas, participantes e alvos não mudam mais.");
    [RelayCommand] private Task VoltarRascunhoAsync() => MudarSituacaoAsync(SituacaoMeta.Rascunho, "A meta voltou a rascunho.", perguntarMotivo: false, null);
    [RelayCommand] private Task IniciarApuracaoAsync() => MudarSituacaoAsync(SituacaoMeta.EmApuracao, "Apuração iniciada.", perguntarMotivo: false, null);
    [RelayCommand] private Task FecharMetaAsync() => MudarSituacaoAsync(SituacaoMeta.Fechada, "Meta fechada: resultado congelado.", perguntarMotivo: false,
        "Fechar (aprovar) a apuração? O realizado calculado, a nota, a faixa e o prêmio de cada participante ficam congelados.");
    [RelayCommand] private Task ReabrirAsync() => MudarSituacaoAsync(SituacaoMeta.EmApuracao, "Apuração reaberta.", perguntarMotivo: true, null);

    private async Task MudarSituacaoAsync(SituacaoMeta para, string mensagem, bool perguntarMotivo, string? confirmar)
    {
        if (Formulario is not { Novo: false } f) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes.", TipoMensagem.Aviso);
            return;
        }
        if (confirmar is not null && !await ConfirmarAsync("Meta", confirmar, "Confirmar", "Cancelar")) return;
        string? motivo = null;
        if (perguntarMotivo)
        {
            motivo = await PerguntarAsync("Reabrir apuração", "Informe o motivo (fica na auditoria):", "Reabrir", "Cancelar", tamanhoMaximo: 250);
            if (string.IsNullOrWhiteSpace(motivo)) return;
        }
        MetaDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.AlterarSituacaoAsync(f.Id,
                new AlterarSituacaoMetaRequisicao { Versao = f.Versao, Situacao = para, Motivo = motivo })))
            return;
        await AtualizarListaAposGravarAsync();
        Gravado(salvo!, mensagem);
    }

    [RelayCommand]
    private async Task CancelarMetaAsync()
    {
        if (Formulario is not { Novo: false } f) return;
        if (!await ConfirmarAsync("Cancelar meta", "Cancelar este rascunho? Ele deixa de aparecer, mas continua no histórico.", "Cancelar meta", "Voltar"))
            return;
        MetaDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.CancelarAsync(f.Id))) return;
        await AtualizarListaAposGravarAsync();
        Gravado(salvo!, "Meta cancelada.");
    }

    [RelayCommand]
    private async Task ApurarAsync()
    {
        if (Formulario is not { Novo: false } f) return;
        ApuracaoDto? apuracao = null;
        if (!await ExecutarAsync(async () => apuracao = await _api.ApurarAsync(f.Id))) return;
        Apuracao.Clear();
        foreach (var p in apuracao!.Participantes) Apuracao.Add(new LinhaApuracao(p));
        ApuracaoAvisos = string.Join(Environment.NewLine,
            (apuracao.Congelada ? new[] { "Resultado congelado no fechamento." } : Array.Empty<string>()).Concat(apuracao.Avisos));
    }

    [RelayCommand] private Task ConferirImportacaoAsync() => ImportarAsync(confirmar: false);
    [RelayCommand] private Task GravarImportacaoAsync() => ImportarAsync(confirmar: true);

    private async Task ImportarAsync(bool confirmar)
    {
        if (Formulario is not { Lancavel: true } f) return;
        if (ItemImportacao is null)
        {
            Mostrar("Escolha o indicador (informado) da importação.", TipoMensagem.Aviso);
            return;
        }
        if (string.IsNullOrWhiteSpace(ConteudoImportacao))
        {
            Mostrar("Cole as linhas \"participante;valor\" (pode ser copiado de uma planilha salva em CSV).", TipoMensagem.Aviso);
            return;
        }
        if (confirmar && TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de gravar a importação.", TipoMensagem.Aviso);
            return;
        }
        ResultadoImportacaoRealizadoDto? resultado = null;
        if (!await ExecutarAsync(async () => resultado = await _api.ImportarRealizadoAsync(f.Id, new ImportarRealizadoRequisicao
            {
                Versao = f.Versao, ItemId = ItemImportacao.Id, Conteudo = ConteudoImportacao, Confirmar = confirmar
            })))
            return;

        LinhasImportacao.Clear();
        foreach (var l in resultado!.Linhas)
            LinhasImportacao.Add($"Linha {l.Linha}: {l.Participante} = {TextoTela.Numero(l.Valor)}" + (l.Erro is null ? " ✓" : $" — {l.Erro}"));
        ImportacaoConferida = !confirmar && resultado.Linhas.All(l => l.Erro is null) && resultado.Validas > 0;

        if (resultado.Gravado)
        {
            Abrir(await ObterAsync(f.Id));
            MarcarFichaSemAlteracoes();
            Mostrar($"{resultado.Validas} realizado(s) importado(s).", TipoMensagem.Sucesso);
        }
        else
            Mostrar(ImportacaoConferida ? $"{resultado.Validas} linha(s) conferida(s). Clique em \"Gravar importação\"." : "Corrija as linhas com erro.",
                    ImportacaoConferida ? TipoMensagem.Informacao : TipoMensagem.Aviso);
    }
}
