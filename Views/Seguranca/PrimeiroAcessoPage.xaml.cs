using Lone.ViewModels.Seguranca;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lone.Views.Seguranca
{
    public sealed partial class PrimeiroAcessoPage : Page
    {
        public PrimeiroAcessoViewModel ViewModel { get; }

        public PrimeiroAcessoPage()
        {
            ViewModel = App.Services.GetRequiredService<PrimeiroAcessoViewModel>();
            InitializeComponent();
            Loaded += (_, _) => CampoNome.Focus(FocusState.Programmatic);
        }
    }
}
