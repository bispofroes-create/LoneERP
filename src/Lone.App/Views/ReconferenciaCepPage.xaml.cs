using Lone.Cliente.ViewModels.Pessoas;

namespace Lone.App.Views;

public partial class ReconferenciaCepPage : ContentPage
{
    public ReconferenciaCepPage(ReconferenciaCepViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
