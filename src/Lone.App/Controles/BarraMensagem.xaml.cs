using Lone.Cliente.ViewModels;

namespace Lone.App.Controles;

public partial class BarraMensagem : ContentView
{
    public static readonly BindableProperty MensagemProperty = BindableProperty.Create(
        nameof(Mensagem), typeof(string), typeof(BarraMensagem), string.Empty,
        propertyChanged: (b, _, _) => ((BarraMensagem)b).Atualizar());

    public static readonly BindableProperty TipoProperty = BindableProperty.Create(
        nameof(Tipo), typeof(TipoMensagem), typeof(BarraMensagem), TipoMensagem.Informacao,
        propertyChanged: (b, _, _) => ((BarraMensagem)b).Atualizar());

    public BarraMensagem()
    {
        InitializeComponent();

        // Recolore ao trocar o tema claro/escuro do sistema com a tela aberta.
        Loaded += (_, _) => { if (Application.Current is { } app) app.RequestedThemeChanged += TemaMudou; };
        Unloaded += (_, _) => { if (Application.Current is { } app) app.RequestedThemeChanged -= TemaMudou; };
    }

    private void TemaMudou(object? sender, AppThemeChangedEventArgs e) => Atualizar();

    public string Mensagem
    {
        get => (string)GetValue(MensagemProperty);
        set => SetValue(MensagemProperty, value);
    }

    public TipoMensagem Tipo
    {
        get => (TipoMensagem)GetValue(TipoProperty);
        set => SetValue(TipoProperty, value);
    }

    private void Atualizar()
    {
        Texto.Text = Mensagem;
        IsVisible = !string.IsNullOrEmpty(Mensagem);

        var escuro = Application.Current?.RequestedTheme == AppTheme.Dark;
        var (texto, fundo) = Tipo switch
        {
            TipoMensagem.Erro => ("Erro", escuro ? "ErroFundoEscuro" : "ErroFundo"),
            TipoMensagem.Aviso => ("Aviso", escuro ? "AvisoFundoEscuro" : "AvisoFundo"),
            TipoMensagem.Sucesso => ("Sucesso", escuro ? "SucessoFundoEscuro" : "SucessoFundo"),
            _ => ("Informacao", escuro ? "InformacaoFundoEscuro" : "InformacaoFundo")
        };

        Moldura.BackgroundColor = Cor(fundo);
        Texto.TextColor = escuro ? Cor("TextoEscuro") : Cor(texto);

        // Leitores de tela anunciam a mensagem assim que ela aparece.
        if (IsVisible)
            SemanticScreenReader.Announce(Mensagem);
    }

    private static Color Cor(string chave) =>
        Application.Current?.Resources.TryGetValue(chave, out var valor) == true && valor is Color cor ? cor : Colors.Gray;
}
