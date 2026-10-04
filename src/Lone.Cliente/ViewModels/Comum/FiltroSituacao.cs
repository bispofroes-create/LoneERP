using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.Api;

namespace Lone.Cliente.ViewModels.Comum;

/// <summary>Situação mostrada na lista de um cadastro que é desativado em vez de excluído.</summary>
public enum SituacaoLista { Ativos, Inativos, Todos }

/// <summary>
/// Filtro "Situação" das listas de cadastro (pedido do usuário, 04/10/2026): Ativos, Inativos ou Todos. A última escolha
/// fica guardada para o usuário (preferência da tela na API: acompanha em qualquer aparelho). Começa em Ativos, como nos
/// ERPs de referência. Sem resposta da API, o filtro funciona e só não fica guardado.
/// </summary>
public sealed partial class FiltroSituacao : ObservableObject
{
    public static readonly Opcao<SituacaoLista>[] Opcoes =
    [
        new(SituacaoLista.Ativos, "Ativos"), new(SituacaoLista.Inativos, "Inativos"), new(SituacaoLista.Todos, "Todos")
    ];

    /// <summary>
    /// Onde as listas de cadastro guardam a escolha (definido pelo aplicativo ao iniciar; nulo nos testes = não guarda).
    /// As telas são criadas pela injeção de dependência com construtores próprios; assim nenhuma precisa mudar.
    /// </summary>
    public static MenuUsuarioApi? PreferenciasPadrao { get; set; }

    private readonly MenuUsuarioApi? _preferencias;
    private readonly string _tela;
    private bool _lendo;

    /// <param name="tela">Rota da tela (ex.: "prazos-periodo"): a chave da preferência.</param>
    public FiltroSituacao(string tela, MenuUsuarioApi? preferencias)
    {
        _tela = tela;
        _preferencias = preferencias;
    }

    public Opcao<SituacaoLista>[] Itens => Opcoes;

    [ObservableProperty] private Opcao<SituacaoLista> _selecionada = Opcoes[0];

    /// <summary>A escolha mudou (a tela refaz a lista).</summary>
    public event Action? Mudou;

    public SituacaoLista Valor => Selecionada.Valor;

    public bool Inclui(bool ativo) => Valor switch
    {
        SituacaoLista.Ativos => ativo,
        SituacaoLista.Inativos => !ativo,
        _ => true
    };

    partial void OnSelecionadaChanged(Opcao<SituacaoLista> value)
    {
        Mudou?.Invoke();
        if (_lendo || _preferencias is null) return;
        _ = GuardarAsync(value.Valor);
    }

    /// <summary>Lê a última escolha do usuário (chamado quando a tela abre).</summary>
    public async Task CarregarAsync()
    {
        if (_preferencias is null) return;
        try
        {
            var guardado = await _preferencias.ObterTelaAsync(_tela);
            if (Ler(guardado) is { } opcao && opcao != Selecionada)
            {
                _lendo = true;
                try { Selecionada = opcao; }
                finally { _lendo = false; }
            }
        }
        catch (Exception)
        {
            // Sem a preferência, fica em Ativos.
        }
    }

    private async Task GuardarAsync(SituacaoLista valor)
    {
        try { await _preferencias!.SalvarTelaAsync(_tela, Gravar(valor)); }
        catch (Exception) { /* a lista já mudou; só não fica guardado */ }
    }

    /// <summary>Conteúdo guardado: {"situacao":"Inativos"} (JSON, para caber outras preferências da tela depois).</summary>
    public static string Gravar(SituacaoLista valor) => $"{{\"situacao\":\"{valor}\"}}";

    public static Opcao<SituacaoLista>? Ler(string? conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo)) return null;
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(conteudo);
            return json.RootElement.TryGetProperty("situacao", out var s) && Enum.TryParse<SituacaoLista>(s.GetString(), out var v)
                ? Opcoes.First(o => o.Valor == v)
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
