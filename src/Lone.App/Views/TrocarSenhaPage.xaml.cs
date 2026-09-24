using Lone.Cliente.ViewModels;

namespace Lone.App.Views;

public partial class TrocarSenhaPage : ContentPage
{
    public TrocarSenhaPage(TrocarSenhaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // Troca obrigatória: o botão "voltar" do Android não pode pular esta tela.
    protected override bool OnBackButtonPressed() => ((TrocarSenhaViewModel)BindingContext).Obrigatoria || base.OnBackButtonPressed();
}
