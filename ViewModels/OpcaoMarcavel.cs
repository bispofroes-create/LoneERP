using CommunityToolkit.Mvvm.ComponentModel;

namespace Lone.ViewModels
{
    /// <summary>Item de lista com caixa de seleção (perfis de um usuário, permissões de um perfil...).</summary>
    public partial class OpcaoMarcavel : ObservableObject
    {
        public int Id { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public string Texto { get; init; } = string.Empty;
        public string Detalhe { get; init; } = string.Empty;

        // bool? porque é o tipo do CheckBox.IsChecked (ligação direta, sem conversão).
        [ObservableProperty] private bool? _marcado = false;

        public bool EstaMarcado => Marcado == true;
    }
}
