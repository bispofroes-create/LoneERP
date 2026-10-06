using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Contracts.Integracoes;
using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Reconferência de CEPs (F6): seleciona os endereços pelo filtro, mostra quantos são, pede confirmação para lote grande e
/// manda blocos pequenos para a API (que confere no ritmo da política). Cancelar para de mandar blocos e interrompe o
/// bloco em andamento; o que já terminou fica. Nunca altera endereço: só a situação da conferência é gravada.
/// </summary>
public sealed partial class ReconferenciaCepViewModel : ViewModelBase
{
    private readonly ConsultasApi _api;
    private readonly IDialogos _dialogos;
    private CancellationTokenSource? _cancelamento;
    private SelecaoReconferenciaCepDto? _selecao;
    private FiltroReconferenciaCepDto _filtroSelecao = new();
    private readonly List<ItemReconferenciaCepDto> _itens = [];

    public ReconferenciaCepViewModel(ConsultasApi api, IDialogos dialogos)
    {
        _api = api;
        _dialogos = dialogos;
    }

    // ---- Filtro ----
    [ObservableProperty] private bool _incluirNaoConferidos = true;
    [ObservableProperty] private bool _incluirDivergentes = true;
    [ObservableProperty] private bool _incluirNaoEncontrados = true;
    [ObservableProperty] private bool _incluirConferidosAntigos = true;
    [ObservableProperty] private string _uf = string.Empty;

    /// <summary>"240 endereços selecionados (12 sem CEP válido ficaram de fora)."</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemSelecao))] private string _textoSelecao = string.Empty;
    public bool TemSelecao => TextoSelecao.Length > 0;

    // ---- Progresso ----
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Parado), nameof(Progresso))] private bool _executando;
    public bool Parado => !Executando;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Progresso))] private int _total;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Progresso), nameof(TextoProgresso))] private int _processados;
    [ObservableProperty] private int _conferidos;
    [ObservableProperty] private int _divergentes;
    [ObservableProperty] private int _naoEncontrados;
    [ObservableProperty] private int _indisponiveis;
    [ObservableProperty] private int _alterados;
    [ObservableProperty] private int _naoProcessados;
    [ObservableProperty] private string _duracao = string.Empty;

    public double Progresso => Total == 0 ? 0 : (double)Processados / Total;
    public string TextoProgresso => Total == 0 ? string.Empty : $"{Processados} de {Total}";

    /// <summary>Ao final (e a cada bloco): os endereços separados por situação, para revisão humana.</summary>
    public ObservableCollection<GrupoReconferenciaCep> Grupos { get; } = new();

    [RelayCommand]
    private Task SelecionarAsync() => ExecutarAsync(async () =>
    {
        _filtroSelecao = Filtro();
        _selecao = await _api.SelecionarReconferenciaAsync(_filtroSelecao);
        TextoSelecao = TextoDaSelecao(_selecao);
    });

    public FiltroReconferenciaCepDto Filtro() => new()
    {
        NaoConferidos = IncluirNaoConferidos, Divergentes = IncluirDivergentes, NaoEncontrados = IncluirNaoEncontrados,
        ConferidosAntigos = IncluirConferidosAntigos, Uf = string.IsNullOrWhiteSpace(Uf) ? null : Uf.Trim().ToUpperInvariant()
    };

    public static string TextoDaSelecao(SelecaoReconferenciaCepDto s) =>
        (s.Total == 0 ? "Nenhum endereço a reconferir com estes filtros." : $"{s.Total} endereço(s) a reconferir")
        + (s.Truncada ? $"; nesta rodada, os {s.Enderecos.Count} primeiros (rode de novo para os demais)" : string.Empty)
        + (s.SemCepValido > 0 ? $". {s.SemCepValido} sem CEP válido ficaram de fora." : s.Total == 0 ? string.Empty : ".");

    [RelayCommand]
    private async Task IniciarAsync()
    {
        if (Executando) return;
        if (_selecao is null) await SelecionarAsync();
        if (_selecao is not { Enderecos.Count: > 0 } selecao)
        {
            Mostrar("Nenhum endereço a reconferir com estes filtros.", TipoMensagem.Aviso);
            return;
        }
        if (selecao.Enderecos.Count > selecao.ConfirmarAcimaDe
            && !await _dialogos.ConfirmarAsync("Reconferir CEPs",
                $"Serão reconferidos {selecao.Enderecos.Count} endereços, no ritmo da consulta externa (pode levar alguns minutos). " +
                "Nenhum endereço é alterado: só a situação da conferência é atualizada. Continuar?", "Reconferir", "Cancelar"))
            return;

        LimparMensagem();
        Zerar(selecao.Enderecos.Count);
        Executando = true;
        _cancelamento = new CancellationTokenSource();
        var inicio = DateTime.UtcNow;
        var execucao = Guid.NewGuid();
        var filtro = _filtroSelecao;
        var tamanho = Math.Max(1, selecao.ItensPorChamada);
        // Fila: os "adiados" (o tempo da chamada acabou antes da consulta) voltam para o fim, até o máximo de reenvios.
        var fila = new Queue<Guid>(selecao.Enderecos);
        var adiamentos = new Dictionary<Guid, int>();
        try
        {
            while (fila.Count > 0)
            {
                if (_cancelamento.IsCancellationRequested) break;
                var bloco = new List<Guid>();
                while (bloco.Count < tamanho && fila.Count > 0) bloco.Add(fila.Dequeue());
                ResumoReconferenciaCepDto resumo;
                try
                {
                    resumo = await _api.ProcessarReconferenciaAsync(new ProcessarReconferenciaCepRequisicao { Enderecos = bloco }, _cancelamento.Token);
                }
                catch (OperationCanceledException)
                {
                    break; // o bloco em andamento foi interrompido: nada dele conta como resultado postal
                }
                var finais = new List<ItemReconferenciaCepDto>();
                foreach (var item in resumo.Itens)
                {
                    if (item.Resultado != ResultadoItemReconferencia.Adiado)
                    {
                        finais.Add(item);
                        continue;
                    }
                    var vezes = adiamentos[item.EnderecoId] = adiamentos.GetValueOrDefault(item.EnderecoId) + 1;
                    if (vezes <= selecao.MaximoAdiamentos)
                        fila.Enqueue(item.EnderecoId);
                    else
                    {
                        item.Resultado = ResultadoItemReconferencia.NaoProcessado;
                        item.Detalhe = "Não processado: o tempo das chamadas acabou antes da consulta mais de uma vez (fonte lenta?). Rode de novo mais tarde.";
                        finais.Add(item);
                    }
                }
                Acumular(finais);
            }
        }
        catch (Exception ex)
        {
            MostrarErro(ex); // falha de comunicação: para aqui; o que já foi processado fica
        }
        finally
        {
            NaoProcessados += Total - Processados; // não executados nunca viram falha postal
            Processados = Total;
            Duracao = $"{(DateTime.UtcNow - inicio).TotalSeconds:0} s";
            Executando = false;
            var cancelada = _cancelamento.IsCancellationRequested;
            _cancelamento.Dispose();
            _cancelamento = null;
            _selecao = null; // a próxima rodada seleciona de novo (o que mudou sai ou entra)
            MontarGrupos();
            // Um evento na Auditoria por execução (só metadados). Falhar aqui não desfaz nada do que foi conferido.
            var registrado = await RegistrarExecucaoAsync(execucao, filtro, cancelada, (int)(DateTime.UtcNow - inicio).TotalSeconds);
            if (!TemMensagem)
                Mostrar(registrado ? Resumo() : Resumo() + " (Não foi possível registrar o resumo da execução na auditoria.)",
                    registrado ? TipoMensagem.Sucesso : TipoMensagem.Aviso);
        }
    }

    [RelayCommand]
    private void Cancelar() => _cancelamento?.Cancel();

    private async Task<bool> RegistrarExecucaoAsync(Guid execucao, FiltroReconferenciaCepDto filtro, bool cancelada, int segundos)
    {
        try
        {
            await _api.ConcluirReconferenciaAsync(new ConcluirReconferenciaCepRequisicao
            {
                ExecucaoId = execucao, Filtro = filtro, Cancelada = cancelada, Total = Total, Conferidos = Conferidos, Divergentes = Divergentes,
                NaoEncontrados = NaoEncontrados, Indisponiveis = Indisponiveis, Alterados = Alterados, NaoProcessados = NaoProcessados,
                DuracaoSegundos = segundos
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [RelayCommand]
    private async Task LimparHistoricoAsync()
    {
        if (!await _dialogos.ConfirmarAsync("Limpar histórico técnico",
                "Apaga do histórico técnico de consultas de CEP o que tem mais de 90 dias. Endereços, cache e histórico dos cadastros não mudam.",
                "Limpar", "Cancelar"))
            return;
        LimpezaHistoricoCepDto? r = null;
        if (await ExecutarAsync(async () => r = await _api.LimparHistoricoCepAsync()))
            Mostrar($"Histórico técnico: {r!.Removidos} consulta(s) com mais de {r.RetencaoDias} dias removida(s).", TipoMensagem.Sucesso);
    }

    private void Zerar(int total)
    {
        _itens.Clear();
        Grupos.Clear();
        Total = total;
        Processados = Conferidos = Divergentes = NaoEncontrados = Indisponiveis = Alterados = NaoProcessados = 0;
        Duracao = string.Empty;
    }

    private void Acumular(IEnumerable<ItemReconferenciaCepDto> itens)
    {
        foreach (var item in itens)
        {
            _itens.Add(item);
            Processados++;
            switch (item.Resultado)
            {
                case ResultadoItemReconferencia.Conferido: Conferidos++; break;
                case ResultadoItemReconferencia.Divergente: Divergentes++; break;
                case ResultadoItemReconferencia.NaoEncontrado: NaoEncontrados++; break;
                case ResultadoItemReconferencia.Indisponivel: Indisponiveis++; break;
                case ResultadoItemReconferencia.AlteradoDuranteAReconferencia: Alterados++; break;
                default: NaoProcessados++; break;
            }
        }
        MontarGrupos();
    }

    public string Resumo() =>
        $"Reconferência concluída: {Conferidos} conferido(s), {Divergentes} com divergência, {NaoEncontrados} não encontrado(s), " +
        $"{Indisponiveis} não consultado(s) agora, {Alterados} alterado(s) durante, {NaoProcessados} não processado(s).";

    /// <summary>Separados por situação, na ordem de quem precisa de atenção primeiro.</summary>
    private void MontarGrupos()
    {
        Grupos.Clear();
        foreach (var (resultado, titulo) in OrdemGrupos)
        {
            var itens = _itens.Where(i => i.Resultado == resultado).Select(LinhaReconferenciaCep.De).ToList();
            if (itens.Count > 0) Grupos.Add(new GrupoReconferenciaCep($"{titulo} ({itens.Count})", itens));
        }
    }

    private static readonly (ResultadoItemReconferencia, string)[] OrdemGrupos =
    [
        (ResultadoItemReconferencia.Divergente, "CEP não corresponde ao endereço"),
        (ResultadoItemReconferencia.NaoEncontrado, "CEP não encontrado"),
        (ResultadoItemReconferencia.Indisponivel, "Não foi possível consultar agora"),
        (ResultadoItemReconferencia.AlteradoDuranteAReconferencia, "Alterados durante a reconferência"),
        (ResultadoItemReconferencia.NaoProcessado, "Não processados"),
        (ResultadoItemReconferencia.Cancelado, "Cancelados em andamento"),
        (ResultadoItemReconferencia.Conferido, "Conferidos")
    ];
}

public sealed record GrupoReconferenciaCep(string Titulo, IReadOnlyList<LinhaReconferenciaCep> Itens);

/// <summary>Uma linha do resultado: quem, onde, o que aconteceu e (quando houver) o que diverge e os CEPs compatíveis.</summary>
public sealed record LinhaReconferenciaCep(string Titulo, string Detalhe)
{
    public static LinhaReconferenciaCep De(ItemReconferenciaCepDto i)
    {
        var quem = i.PessoaCodigo is { } codigo ? $"{codigo} · {i.PessoaNome}" : "(endereço)";
        var onde = string.Join(" · ", new[] { i.Endereco, i.Cep is { Length: 8 } c ? $"CEP {c[..5]}-{c[5..]}" : i.Cep }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        var divergentes = i.Componentes.Where(c => c.Situacao == SituacaoComponenteCep.Divergente).Select(c => c.Motivo);
        // Candidatos só para ver: nenhum é escolhido, recomendado ou aplicado (a correção é na ficha, pelo usuário).
        var candidatos = i.Candidatos.Count == 0 ? string.Empty
            : " CEPs compatíveis (para conferir na ficha): " + string.Join(", ", i.Candidatos.Select(c => c.Cep.Length == 8 ? $"{c.Cep[..5]}-{c.Cep[5..]}" : c.Cep)) + ".";
        var detalhe = string.Join(" ", new[] { i.Detalhe }.Concat(divergentes.Where(d => !i.Detalhe.Contains(d, StringComparison.Ordinal))))
                      + candidatos;
        return new LinhaReconferenciaCep(string.IsNullOrWhiteSpace(onde) ? quem : $"{quem} — {onde}", detalhe.Trim());
    }
}
