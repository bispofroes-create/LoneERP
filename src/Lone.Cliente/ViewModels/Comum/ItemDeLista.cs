using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lone.Cliente.ViewModels.Comum;

/// <summary>
/// Item de uma lista editável da ficha (endereço, contato...). O botão "Remover" do item chama quem é dono
/// da lista, que decide o que mais muda junto (ex.: endereço fiscal de um estabelecimento).
/// </summary>
public abstract partial class ItemDeLista : ObservableObject
{
    /// <summary>Definido pela ficha ao incluir o item na lista.</summary>
    public Action? AoRemover { get; set; }

    [RelayCommand]
    private void Remover() => AoRemover?.Invoke();
}
