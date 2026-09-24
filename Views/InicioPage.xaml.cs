using System;
using Lone.Aplicacao.Pessoas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Lone.Views
{
    public sealed partial class InicioPage : Page
    {
        public InicioPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            try
            {
                var total = await App.Services.GetRequiredService<IPessoaAppService>().ContarClientesAtivosAsync();
                TotalClientes.Text = total.ToString("N0");
            }
            catch (Exception)
            {
                TotalClientes.Text = "–";
            }
        }
    }
}
