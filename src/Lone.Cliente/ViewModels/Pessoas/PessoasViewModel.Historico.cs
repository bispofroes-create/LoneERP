using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Mensagens;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Auditoria;
using Lone.Domain.Auditoria;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Aba Histórico (03/10/2026): filtro por tipo (parte do cadastro), período e quem alterou, aplicado no servidor sobre o
/// histórico inteiro; e impressão do histórico filtrado (página HTML aberta no navegador, que imprime ou salva em PDF).
/// </summary>
public sealed partial class PessoasViewModel
{
    /// <summary>Primeira opção das listas do filtro: sem filtro.</summary>
    public static readonly Opcao<string> TodosHistorico = new(string.Empty, "Todos");

    /// <summary>Limite da impressão (registros): o histórico de um cadastro raramente passa disso.</summary>
    public const int LimiteImpressaoHistorico = 5000;

    public ObservableCollection<Opcao<string>> HistoricoTipos { get; } = [TodosHistorico];
    public ObservableCollection<Opcao<string>> HistoricoUsuarios { get; } = [TodosHistorico];

    [ObservableProperty] private Opcao<string>? _historicoTipo = TodosHistorico;
    [ObservableProperty] private Opcao<string>? _historicoUsuario = TodosHistorico;

    /// <summary>Período (dd/mm/aaaa); vazio = sem limite.</summary>
    [ObservableProperty] private string _historicoPeriodoDe = string.Empty;
    [ObservableProperty] private string _historicoPeriodoAte = string.Empty;

    /// <summary>Filtro em uso na lista mostrada (o "Filtrar" aplica; mudar os campos sem filtrar não muda a lista).</summary>
    private FiltroHistorico? _filtroHistorico;

    /// <summary>Opções do filtro já lidas para esta ficha.</summary>
    private Guid? _opcoesHistoricoDe;

    /// <summary>Há filtro aplicado (mostra "Limpar filtro" e o aviso na impressão).</summary>
    public bool HistoricoFiltrado => _filtroHistorico is { Vazio: false };

    [RelayCommand]
    private Task FiltrarHistoricoAsync()
    {
        if (Formulario is not { Existente: true } f) return Task.CompletedTask;
        if (MontarFiltroHistorico() is not { } filtro) return Task.CompletedTask;
        _filtroHistorico = filtro.Vazio ? null : filtro;
        OnPropertyChanged(nameof(HistoricoFiltrado));
        return CarregarHistoricoAsync(f.Id);
    }

    [RelayCommand]
    private Task LimparFiltroHistoricoAsync()
    {
        HistoricoTipo = TodosHistorico;
        HistoricoUsuario = TodosHistorico;
        HistoricoPeriodoDe = string.Empty;
        HistoricoPeriodoAte = string.Empty;
        _filtroHistorico = null;
        OnPropertyChanged(nameof(HistoricoFiltrado));
        return Formulario is { Existente: true } f ? CarregarHistoricoAsync(f.Id) : Task.CompletedTask;
    }

    /// <summary>Filtro dos campos da tela; nulo (com aviso) se a data não vale ou o período está invertido.</summary>
    private FiltroHistorico? MontarFiltroHistorico()
    {
        if (!TextoTela.TentarData(HistoricoPeriodoDe, out var de) || !TextoTela.TentarData(HistoricoPeriodoAte, out var ate))
        {
            Mostrar("Período do histórico: data inválida (use dd/mm/aaaa).", TipoMensagem.Erro);
            return null;
        }
        if (de is { } d && ate is { } a && a < d)
        {
            Mostrar("Período do histórico: a data final é anterior à inicial.", TipoMensagem.Erro);
            return null;
        }
        return FiltroDoPeriodo(HistoricoTipo?.Valor, HistoricoUsuario?.Valor, de, ate);
    }

    /// <summary>Dias locais viram instantes UTC (início do dia "de" até o início do dia seguinte a "até").</summary>
    public static FiltroHistorico FiltroDoPeriodo(string? entidade, string? usuario, DateOnly? de, DateOnly? ate) => new()
    {
        Entidades = string.IsNullOrEmpty(entidade) ? new() : [entidade],
        Usuario = string.IsNullOrEmpty(usuario) ? null : usuario,
        DeUtc = de is { } d ? d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime() : null,
        AteUtc = ate is { } a ? a.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime() : null
    };

    /// <summary>Listas do filtro: as partes do cadastro e os usuários que aparecem no histórico desta pessoa.</summary>
    private async Task CarregarOpcoesHistoricoAsync(Guid pessoaId)
    {
        if (_opcoesHistoricoDe == pessoaId) return;
        _opcoesHistoricoDe = pessoaId;
        try
        {
            var opcoes = await _pessoas.OpcoesHistoricoAsync(pessoaId);
            if (Formulario?.Id != pessoaId) return;
            Repor(HistoricoTipos, opcoes.Entidades
                .Select(e => new Opcao<string>(e, DescritorCampos.Entidade(e)))
                .OrderBy(o => o.Texto, StringComparer.Create(TextoTela.Brasil, ignoreCase: true)));
            Repor(HistoricoUsuarios, opcoes.Usuarios.Select(u => new Opcao<string>(u, u)));
        }
        catch (SessaoExpiradaException)
        {
        }
        catch (Exception)
        {
            _opcoesHistoricoDe = null; // sem as listas o histórico continua; tenta de novo na próxima abertura
        }

        static void Repor(ObservableCollection<Opcao<string>> lista, IEnumerable<Opcao<string>> novas)
        {
            lista.Clear();
            lista.Add(TodosHistorico);
            foreach (var o in novas) lista.Add(o);
        }
    }

    /// <summary>Volta o filtro ao padrão ao abrir outra ficha.</summary>
    private void ReiniciarFiltroHistorico()
    {
        _filtroHistorico = null;
        _opcoesHistoricoDe = null;
        HistoricoTipo = TodosHistorico;
        HistoricoUsuario = TodosHistorico;
        HistoricoPeriodoDe = string.Empty;
        HistoricoPeriodoAte = string.Empty;
        while (HistoricoTipos.Count > 1) HistoricoTipos.RemoveAt(1);
        while (HistoricoUsuarios.Count > 1) HistoricoUsuarios.RemoveAt(1);
        OnPropertyChanged(nameof(HistoricoFiltrado));
    }

    /// <summary>Imprime o histórico com o filtro em uso: lê tudo (até o limite) e abre a página no navegador.</summary>
    [RelayCommand]
    private async Task ImprimirHistoricoAsync()
    {
        if (Formulario is not { Existente: true } f) return;
        var itens = new List<HistoricoItem>();
        var cortado = false;
        var ok = await ExecutarAsync(async () =>
        {
            long? antes = null;
            while (true)
            {
                var pagina = await _pessoas.ListarHistoricoAsync(f.Id, antes, 500, filtro: _filtroHistorico);
                itens.AddRange(pagina.Select(HistoricoItem.De));
                if (pagina.Count < 500) break;
                if (itens.Count >= LimiteImpressaoHistorico) { cortado = true; break; }
                antes = pagina[^1].Id;
            }
        });
        if (!ok) return;
        var html = HistoricoImpressao.Html(f.Titulo, DescreverFiltroHistorico(), itens, cortado, DateTime.Now);
        await _arquivos.AbrirAsync($"historico-{f.Codigo:000000}.html", Encoding.UTF8.GetBytes(html));
    }

    /// <summary>"Tipo: Endereço · De 01/09/2026 até 30/09/2026 · Usuário: Rafael" (vazio sem filtro).</summary>
    private string DescreverFiltroHistorico()
    {
        if (_filtroHistorico is not { Vazio: false }) return string.Empty;
        var partes = new List<string>();
        if (HistoricoTipo is { Valor.Length: > 0 } t) partes.Add("Tipo: " + t.Texto);
        if (HistoricoPeriodoDe.Length > 0 || HistoricoPeriodoAte.Length > 0)
            partes.Add($"Período: {(HistoricoPeriodoDe.Length > 0 ? HistoricoPeriodoDe : "início")} a {(HistoricoPeriodoAte.Length > 0 ? HistoricoPeriodoAte : "hoje")}");
        if (HistoricoUsuario is { Valor.Length: > 0 } u) partes.Add("Usuário: " + u.Texto);
        return string.Join(" · ", partes);
    }
}
