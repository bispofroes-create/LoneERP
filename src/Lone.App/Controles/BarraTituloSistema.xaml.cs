namespace Lone.App.Controles;

/// <summary>Conteúdo à direita da barra de título do Windows (as ações estão no MenuViewModel; o toque no usuário abre o MenuDoUsuario).</summary>
public partial class BarraTituloSistema : ContentView
{
    public BarraTituloSistema() => InitializeComponent();

    private void Usuario_Tapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is Lone.Cliente.ViewModels.MenuViewModel menu) MenuDoUsuario.Abrir(Usuario, menu);
    }
}
