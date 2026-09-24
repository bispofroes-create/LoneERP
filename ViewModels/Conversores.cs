using Microsoft.UI.Xaml;

namespace Lone.ViewModels
{
    /// <summary>Funções usadas nas ligações x:Bind da tela (ex.: {x:Bind vm:Conversores.Nao(...)}).</summary>
    public static class Conversores
    {
        public static bool Nao(bool valor) => !valor;

        public static Visibility VisivelSeFalso(bool valor) => valor ? Visibility.Collapsed : Visibility.Visible;

        public static Visibility VisivelSeZero(int valor) => valor == 0 ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>CheckBox.IsChecked é bool?; estas duas funções fazem a ida e a volta com um bool.</summary>
        public static bool? ParaMarcado(bool valor) => valor;

        public static bool DeMarcado(bool? valor) => valor == true;

        public static string TextoMatriz(bool ehMatriz) => ehMatriz ? " · matriz" : " · filial";

        public static string OuTraco(string? texto) => string.IsNullOrWhiteSpace(texto) ? "—" : texto;
    }
}
