using Lone.Aplicacao.Seguranca;
using Lone.ViewModels.Seguranca;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Lone.Views.Seguranca
{
    public sealed partial class PerfisPage : Page
    {
        public PerfisViewModel ViewModel { get; }

        public PerfisPage()
        {
            ViewModel = App.Services.GetRequiredService<PerfisViewModel>();
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CarregarAsync();
        }

        private void Lista_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            ViewModel.Selecionado = Lista.SelectedItem as PerfilResumo;
    }
}
