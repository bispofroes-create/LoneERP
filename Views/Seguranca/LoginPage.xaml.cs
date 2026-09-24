using Lone.ViewModels.Seguranca;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Lone.Views.Seguranca
{
    public sealed partial class LoginPage : Page
    {
        public LoginViewModel ViewModel { get; }

        public LoginPage()
        {
            ViewModel = App.Services.GetRequiredService<LoginViewModel>();
            InitializeComponent();
            Loaded += (_, _) => CampoLogin.Focus(FocusState.Programmatic);
        }

        // Enter na senha = Entrar.
        private void Senha_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter) return;
            e.Handled = true;
            ViewModel.EntrarCommand.Execute(null);
        }
    }
}
