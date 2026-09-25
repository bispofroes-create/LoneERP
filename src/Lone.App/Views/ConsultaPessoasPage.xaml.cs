using Lone.Cliente.ViewModels.Pessoas;

namespace Lone.App.Views;

/// <summary>Só aparência: carrega as opções na primeira vez que a tela aparece.</summary>
public partial class ConsultaPessoasPage : ContentPage
{
    private readonly ConsultaPessoasViewModel _viewModel;

    public ConsultaPessoasPage(ConsultaPessoasViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.CarregarCommand.Execute(null);
    }
}
