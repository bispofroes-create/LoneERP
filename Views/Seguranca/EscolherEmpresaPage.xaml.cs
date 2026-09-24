using Lone.Aplicacao.Empresas;
using Lone.ViewModels.Seguranca;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace Lone.Views.Seguranca
{
    public sealed partial class EscolherEmpresaPage : Page
    {
        public EscolherEmpresaViewModel ViewModel { get; }

        public EscolherEmpresaPage()
        {
            ViewModel = App.Services.GetRequiredService<EscolherEmpresaViewModel>();
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CarregarAsync();
            Lista.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }

        private void Lista_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.Selecionada = Lista.SelectedItem as EmpresaAtiva;
        }

        // Duplo clique ou Enter na empresa = Entrar.
        private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => Entrar();

        private void Lista_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter) return;
            e.Handled = true;
            Entrar();
        }

        private void Entrar()
        {
            if (ViewModel.EntrarCommand.CanExecute(null))
                ViewModel.EntrarCommand.Execute(null);
        }
    }
}
