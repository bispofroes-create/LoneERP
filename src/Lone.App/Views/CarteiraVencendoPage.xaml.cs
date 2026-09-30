using Lone.Cliente.Navegacao;
using Lone.Cliente.ViewModels.Comercial;

namespace Lone.App.Views;

public partial class CarteiraVencendoPage : ContentPage
{
    private readonly CarteiraVencendoViewModel _viewModel;

    public CarteiraVencendoPage(CarteiraVencendoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.AbrirTela = rota => GerenciadorNavegacao.Padrao.IrParaTelaAsync(rota, OrigemNavegacao.Link);
    }

    /// <summary>Relê a cada vez que a tela aparece (os dias diminuem e a carteira muda).</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.CarregarCommand.Execute(null);
    }
}
