namespace Lone.App.Views;

public partial class EmConstrucaoPage : ContentPage
{
    public EmConstrucaoPage()
    {
        InitializeComponent();
    }

    // O título vem do item do menu que abriu a página.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Title = Shell.Current?.CurrentItem?.Title ?? string.Empty;
        Titulo.Text = Title;
    }
}
