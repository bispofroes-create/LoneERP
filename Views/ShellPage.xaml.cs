using System;
using System.Threading.Tasks;
using Lone.ViewModels;
using Lone.Views.Seguranca;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lone.Views
{
    /// <summary>O sistema depois do login: menu lateral e área de conteúdo.</summary>
    public sealed partial class ShellPage : Page
    {
        private bool _carregado;

        public ShellViewModel ViewModel { get; }

        public ShellPage()
        {
            ViewModel = App.Services.GetRequiredService<ShellViewModel>();
            InitializeComponent();
            Unloaded += (_, _) => ViewModel.Desligar();
        }

        private async void Navegacao_Loaded(object sender, RoutedEventArgs e)
        {
            if (_carregado) return;
            _carregado = true;

            Navegacao.SelectedItem = Navegacao.MenuItems[0];

            // Senha definida pelo administrador: troca obrigatória antes de usar o sistema.
            if (ViewModel.DeveTrocarSenha)
                await TrocarSenhaAsync(obrigatoria: true);
        }

        private async void TrocarSenha_Click(object sender, RoutedEventArgs e) => await TrocarSenhaAsync(obrigatoria: false);

        private async Task TrocarSenhaAsync(bool obrigatoria)
        {
            var dialogo = new TrocarSenhaDialog(obrigatoria) { XamlRoot = XamlRoot };
            var resultado = await dialogo.ShowAsync();

            if (obrigatoria && resultado != ContentDialogResult.Primary)
                ViewModel.SairCommand.Execute(null);
        }

        private void Navegacao_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem item) return;

            switch (item.Tag as string)
            {
                case "inicio":
                    Conteudo.Navigate(typeof(InicioPage));
                    break;
                case "pessoas":
                    Conteudo.Navigate(typeof(PessoasPage));
                    break;
                case "usuarios":
                    Conteudo.Navigate(typeof(UsuariosPage));
                    break;
                case "perfis":
                    Conteudo.Navigate(typeof(PerfisPage));
                    break;
                default:
                    Conteudo.Navigate(typeof(EmConstrucaoPage), item.Content?.ToString());
                    break;
            }
        }
    }
}
