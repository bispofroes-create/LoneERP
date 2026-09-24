using Lone.App.Controles;
using Lone.Cliente.ViewModels.Seguranca;

namespace Lone.App.Views;

/// <summary>Só aparência: arrumação lista/ficha e repasse do "Remover" de cada perfil ao ViewModel.</summary>
public partial class UsuariosPage : ContentPage
{
    private readonly UsuariosViewModel _viewModel;
    private readonly LayoutMestreDetalhe _layout;
    private bool _carregado;

    public UsuariosPage(UsuariosViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _layout = new LayoutMestreDetalhe(this, Grade, viewModel);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_carregado) return;
        _carregado = true;
        _viewModel.CarregarCommand.Execute(null);
    }

    protected override bool OnBackButtonPressed() => _layout.TratarVoltar() || base.OnBackButtonPressed();

    private void RemoverPerfil_Clicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: PerfilAtribuido perfil })
            _viewModel.RemoverPerfilCommand.Execute(perfil);
    }
}
