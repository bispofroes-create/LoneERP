using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Conferência "atual × Receita" (consulta de CNPJ num cadastro já gravado): os campos em que a Receita traz um valor
/// diferente do atual. Nada é trocado sem o usuário marcar: "Aplicar marcados" troca só os marcados; "Manter os atuais"
/// fecha sem trocar. Campos vazios já foram preenchidos direto pela consulta.
/// </summary>
public sealed partial class ConferenciaReceita : ObservableObject
{
    public ObservableCollection<ItemConferenciaReceita> Itens { get; } = new();

    [ObservableProperty] private bool _visivel;
    [ObservableProperty] private string _fonte = "Receita Federal";

    /// <summary>Depois de aplicar: os campos trocados (para o destaque saber que vieram da consulta).</summary>
    public Action<IReadOnlyList<ChaveCampo>>? AoAplicar { get; set; }

    public string Titulo => Itens.Count == 1
        ? "A Receita trouxe 1 valor diferente do atual"
        : $"A Receita trouxe {Itens.Count} valores diferentes dos atuais";

    public string Explicacao => "Marque o que deve ser trocado pelo valor da Receita. O que não marcar continua como está.";

    public void Abrir(IEnumerable<MudancaReceita> mudancas, string? fonte)
    {
        Itens.Clear();
        foreach (var m in mudancas) Itens.Add(new ItemConferenciaReceita(m));
        if (!string.IsNullOrWhiteSpace(fonte)) Fonte = fonte;
        OnPropertyChanged(nameof(Titulo));
        Visivel = Itens.Count > 0;
    }

    [RelayCommand]
    private void AplicarMarcados()
    {
        var aplicados = new List<ChaveCampo>();
        foreach (var item in Itens.Where(i => i.Aceitar))
        {
            item.Mudanca.Aplicar();
            aplicados.Add(item.Mudanca.Chave);
        }
        Fechar();
        if (aplicados.Count > 0) AoAplicar?.Invoke(aplicados);
    }

    [RelayCommand]
    private void ManterAtuais() => Fechar();

    [RelayCommand]
    private void MarcarTodos()
    {
        var todos = Itens.Any(i => !i.Aceitar);
        foreach (var item in Itens) item.Aceitar = todos;
    }

    private void Fechar()
    {
        Visivel = false;
        Itens.Clear();
    }
}

/// <summary>Uma linha da conferência: o campo, o valor atual, o da Receita e a marca "trocar".</summary>
public sealed partial class ItemConferenciaReceita : ObservableObject
{
    public ItemConferenciaReceita(MudancaReceita mudanca) => Mudanca = mudanca;

    public MudancaReceita Mudanca { get; }
    public string Rotulo => Mudanca.Rotulo;
    public string Atual => Mudanca.Atual;
    public string DaReceita => Mudanca.DaReceita;

    [ObservableProperty] private bool _aceitar;
}
