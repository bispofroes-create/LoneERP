using Lone.Aplicacao.Seguranca;
using Lone.ViewModels.Seguranca;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Lone.Views.Seguranca
{
    public sealed partial class UsuariosPage : Page
    {
        public UsuariosViewModel ViewModel { get; }

        public UsuariosPage()
        {
            ViewModel = App.Services.GetRequiredService<UsuariosViewModel>();
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CarregarAsync();
        }

        private void Lista_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            ViewModel.Selecionado = Lista.SelectedItem as UsuarioResumo;

        private void RemoverPerfil_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: AtribuicaoPerfil linha })
                ViewModel.RemoverPerfil(linha);
        }
    }
}
