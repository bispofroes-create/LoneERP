using Lone.Cliente.ViewModels.Comercial;

namespace Lone.App.Views;

/// <summary>Só aparência e a ligação de "Abrir ficha" com o Shell.</summary>
public partial class CarteiraEmDataPage : ContentPage
{
    public CarteiraEmDataPage(CarteiraEmDataViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.AbrirTela = rota => Shell.Current.GoToAsync("//" + rota);
    }
}
