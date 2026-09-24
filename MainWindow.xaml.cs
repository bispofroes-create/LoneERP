using System;
using System.Threading.Tasks;
using Lone.Aplicacao.Infraestrutura;
using Lone.Servicos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Lone
{
    public sealed partial class MainWindow : Window
    {
        private bool _inicializado;

        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(BarraTitulo);
            AppWindow.Resize(new SizeInt32(1280, 800));
        }

        private async void Raiz_Loaded(object sender, RoutedEventArgs e)
        {
            if (_inicializado) return;
            _inicializado = true;
            await IniciarAsync();
        }

        /// <summary>Prepara o banco e abre o primeiro acesso ou o login.</summary>
        private async Task IniciarAsync()
        {
            Carregando.Visibility = Visibility.Visible;

            try
            {
                await App.Services.GetRequiredService<IBancoDeDados>().PrepararAsync();

                var navegacao = App.Services.GetRequiredService<INavegacao>();
                navegacao.Registrar(Principal);
                await navegacao.IniciarAsync();

                Carregando.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                var dialogo = new ContentDialog
                {
                    XamlRoot = Raiz.XamlRoot,
                    Title = "Não foi possível preparar o banco de dados",
                    Content = new ScrollViewer
                    {
                        MaxHeight = 300,
                        Content = new TextBlock
                        {
                            Text = ex.GetBaseException().Message +
                                   "\n\nConfira a conexão em appsettings.json (ConnectionStrings:Lone).",
                            TextWrapping = TextWrapping.Wrap,
                            IsTextSelectionEnabled = true
                        }
                    },
                    PrimaryButtonText = "Tentar de novo",
                    CloseButtonText = "Fechar",
                    DefaultButton = ContentDialogButton.Primary
                };

                if (await dialogo.ShowAsync() == ContentDialogResult.Primary)
                    await IniciarAsync();
                else
                    Close();
            }
        }
    }
}
