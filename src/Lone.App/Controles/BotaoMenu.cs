using System.Windows.Input;
using Lone.App.Plataforma;
using static Lone.App.Controles.RecursosTela;

namespace Lone.App.Controles;

/// <summary>
/// Botão com menu (ex.: "Exportar ▾" › Imprimir / PDF, Excel (CSV); 03/10/2026): um botão só na barra da lista, e as
/// opções num popover — novas formas de exportar entram no menu, sem mais botões. Sem popover na plataforma, o botão
/// executa a primeira opção.
/// </summary>
public sealed class BotaoMenu : ContentView
{
    public static readonly BindableProperty TextoProperty = BindableProperty.Create(
        nameof(Texto), typeof(string), typeof(BotaoMenu), string.Empty, propertyChanged: (b, _, n) => ((BotaoMenu)b)._botao.Text = (string)n);

    public static readonly BindableProperty DicaProperty = BindableProperty.Create(
        nameof(Dica), typeof(string), typeof(BotaoMenu), string.Empty, propertyChanged: (b, _, n) => ToolTipProperties.SetText(((BotaoMenu)b)._botao, (string)n));

    public static readonly BindableProperty Texto1Property = BindableProperty.Create(nameof(Texto1), typeof(string), typeof(BotaoMenu), string.Empty);
    public static readonly BindableProperty Comando1Property = BindableProperty.Create(nameof(Comando1), typeof(ICommand), typeof(BotaoMenu));
    public static readonly BindableProperty Texto2Property = BindableProperty.Create(nameof(Texto2), typeof(string), typeof(BotaoMenu), string.Empty);
    public static readonly BindableProperty Comando2Property = BindableProperty.Create(nameof(Comando2), typeof(ICommand), typeof(BotaoMenu));
    public static readonly BindableProperty Texto3Property = BindableProperty.Create(nameof(Texto3), typeof(string), typeof(BotaoMenu), string.Empty);
    public static readonly BindableProperty Comando3Property = BindableProperty.Create(nameof(Comando3), typeof(ICommand), typeof(BotaoMenu));

    private readonly Button _botao = new();
    private Action? _fechar;

    public BotaoMenu()
    {
        Estilo(_botao, "BotaoSecundario");
        _botao.Clicked += (_, _) => Abrir();
        Content = _botao;
    }

    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public string Dica { get => (string)GetValue(DicaProperty); set => SetValue(DicaProperty, value); }
    public string Texto1 { get => (string)GetValue(Texto1Property); set => SetValue(Texto1Property, value); }
    public ICommand? Comando1 { get => (ICommand?)GetValue(Comando1Property); set => SetValue(Comando1Property, value); }
    public string Texto2 { get => (string)GetValue(Texto2Property); set => SetValue(Texto2Property, value); }
    public ICommand? Comando2 { get => (ICommand?)GetValue(Comando2Property); set => SetValue(Comando2Property, value); }
    public string Texto3 { get => (string)GetValue(Texto3Property); set => SetValue(Texto3Property, value); }
    public ICommand? Comando3 { get => (ICommand?)GetValue(Comando3Property); set => SetValue(Comando3Property, value); }

    private void Abrir()
    {
        var opcoes = new[] { (Texto1, Comando1), (Texto2, Comando2), (Texto3, Comando3) }
            .Where(o => !string.IsNullOrEmpty(o.Item1) && o.Item2 is not null).ToList();
        if (opcoes.Count == 0) return;
        if (!Popover.Disponivel)
        {
            opcoes[0].Item2!.Execute(null);
            return;
        }
        var lista = new VerticalStackLayout { Padding = 4, WidthRequest = 200 };
        foreach (var (texto, comando) in opcoes)
        {
            var item = new Button
            {
                Text = texto, FontSize = 14, Padding = new Thickness(12, 0), HeightRequest = 36, MinimumHeightRequest = 0,
                BackgroundColor = Colors.Transparent, BorderWidth = 0, HorizontalOptions = LayoutOptions.Fill
            };
            Cor(item, Button.TextColorProperty, "Texto");
            item.Clicked += (_, _) =>
            {
                _fechar?.Invoke();
                if (comando!.CanExecute(null)) comando.Execute(null);
            };
            lista.Add(item);
        }
        _fechar = Popover.Mostrar(_botao, lista);
    }
}
