using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lone.Cliente.ViewModels.Comum;

/// <summary>Um campo da ficha: o id estável (<c>CamposFichaPessoa</c>) e, em cartões de lista, o Id do registro.</summary>
public readonly record struct ChaveCampo(string Campo, Guid? Item = null);

/// <summary>
/// Um campo alterado desde a última gravação: de onde veio o valor (<c>"Receita"</c>, ou nulo = digitado) e como estava
/// antes. O texto já vem pronto para a tela ("● Veio da Receita às 19:40 · antes: (vazio)").
/// </summary>
public sealed record DestaqueCampo(ChaveCampo Chave, string? Origem, string Antes, DateTime? Quando = null)
{
    public bool DaReceita => Origem is not null;

    public string Texto => (DaReceita
        ? Quando is { } quando ? $"● Veio da {Origem} às {quando.ToLocalTime():HH:mm}" : $"● Veio da {Origem}"
        : "● Alterado") + " · antes: " + (Antes.Length == 0 ? "(vazio)" : Antes);
}

/// <summary>
/// "Destacar alterações" da ficha: o que mudou desde a última gravação, cada campo com a origem do valor. Liga e desliga
/// pelo botão da barra da ficha; a tela (os campos) ouve <see cref="Mudou"/> e se pinta. Cor neutra (não é erro nem
/// pendência). Quem calcula os campos é a tela da ficha (<see cref="Definir"/>); aqui só se guarda e se responde.
/// </summary>
public sealed partial class DestaquesFicha : ObservableObject
{
    private Dictionary<ChaveCampo, DestaqueCampo> _porChave = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBotao), nameof(DicaBotao))]
    private bool _ligado;

    /// <summary>Avisa a tela: mudaram os campos destacados ou o botão foi ligado/desligado.</summary>
    public event Action? Mudou;

    public IReadOnlyCollection<DestaqueCampo> Itens => _porChave.Values;
    public int Quantidade => _porChave.Count;
    public int QuantidadeDaReceita => _porChave.Values.Count(d => d.DaReceita);

    /// <summary>O botão só aparece com algo a destacar.</summary>
    public bool TemAlteracoes => _porChave.Count > 0;

    public string TextoBotao => Ligado ? $"Ocultar destaque ({Quantidade})" : $"Destacar alterações ({Quantidade})";

    public string DicaBotao => Ligado
        ? "Volta os campos ao normal"
        : "Destaca os campos alterados desde a última gravação e diz de onde veio cada valor (Receita ou digitado)";

    /// <summary>O destaque do campo, se o botão está ligado e o campo mudou; nulo, nada a mostrar.</summary>
    public DestaqueCampo? De(string campo, Guid? item) =>
        Ligado && _porChave.TryGetValue(new ChaveCampo(campo, item), out var destaque) ? destaque : null;

    /// <summary>Os campos alterados agora (a tela chama a cada alteração da ficha). Igual ao anterior: nada muda na tela.</summary>
    public void Definir(IEnumerable<DestaqueCampo> destaques)
    {
        var novos = destaques.GroupBy(d => d.Chave).ToDictionary(g => g.Key, g => g.First());
        if (novos.Count == _porChave.Count && novos.All(n => _porChave.TryGetValue(n.Key, out var d) && d == n.Value)) return;
        _porChave = novos;
        OnPropertyChanged(nameof(Itens));
        OnPropertyChanged(nameof(Quantidade));
        OnPropertyChanged(nameof(QuantidadeDaReceita));
        OnPropertyChanged(nameof(TemAlteracoes));
        OnPropertyChanged(nameof(TextoBotao));
        Mudou?.Invoke();
    }

    partial void OnLigadoChanged(bool value) => Mudou?.Invoke();

    [RelayCommand]
    private void Alternar() => Ligado = !Ligado;
}
