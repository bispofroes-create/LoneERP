using Lone.ViewModels.Seguranca;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lone.Views.Seguranca
{
    public sealed partial class TrocarSenhaDialog : ContentDialog
    {
        public TrocarSenhaViewModel ViewModel { get; }

        /// <param name="obrigatoria">Troca exigida no login: o botão de fechar vira "Sair do sistema".</param>
        public TrocarSenhaDialog(bool obrigatoria)
        {
            ViewModel = App.Services.GetRequiredService<TrocarSenhaViewModel>();
            InitializeComponent();

            if (obrigatoria)
            {
                CloseButtonText = "Sair do sistema";
                AvisoObrigatorio.Visibility = Visibility.Visible;
            }
        }

        // O diálogo só fecha se a troca der certo; senão mostra o erro e continua aberto.
        private async void Trocar_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var adiamento = args.GetDeferral();
            args.Cancel = !await ViewModel.TrocarAsync();
            adiamento.Complete();
        }
    }
}
