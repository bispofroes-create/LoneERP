using Lone.App.Plataforma;
using Lone.Cliente.Formularios;
using Lone.Cliente.ViewModels.Comum;
using static Lone.App.Controles.RecursosTela;

namespace Lone.App.Controles;

/// <summary>
/// Campo Prazo entre Início e Fim (pedido do usuário, 03/10/2026; conta em <see cref="CalculoPrazo"/>, testada):
/// digitar os dias ou escolher um prazo pronto no ▾ (30 dias, 60 dias, 1 ano...) calcula o Fim, contando o início; e
/// "Não terminar em fim de semana" leva o Fim de sábado ou domingo para a segunda, dizendo quantos dias foram somados.
/// Mudar o Fim à mão recalcula os dias; mudar o Início move o Fim só depois que o prazo foi escolhido aqui (abrir um
/// registro não muda nada). Liga-se aos textos de Início e Fim do formulário: <c>Inicio</c> e <c>Fim</c> (TwoWay).
/// A opção do fim de semana é uma ajuda da tela: não é gravada (a data gravada é o Fim que aparece).
/// </summary>
public sealed class CampoPrazo : ContentView
{
    public static readonly BindableProperty InicioProperty = BindableProperty.Create(
        nameof(Inicio), typeof(string), typeof(CampoPrazo), string.Empty, propertyChanged: (b, _, _) => ((CampoPrazo)b).AoMudarInicio());

    public static readonly BindableProperty FimProperty = BindableProperty.Create(
        nameof(Fim), typeof(string), typeof(CampoPrazo), string.Empty, BindingMode.TwoWay,
        propertyChanged: (b, _, _) => ((CampoPrazo)b).AoMudarFim());

    /// <summary>
    /// O registro aberto (ex.: <c>{Binding Formulario}</c>): ao trocar de registro, o prazo escolhido no anterior é
    /// esquecido, para o Início do novo registro não mover o Fim dele.
    /// </summary>
    public static readonly BindableProperty RegistroProperty = BindableProperty.Create(
        nameof(Registro), typeof(object), typeof(CampoPrazo), null, propertyChanged: (b, _, _) => ((CampoPrazo)b).Esquecer());

    public static readonly BindableProperty SomenteLeituraProperty = BindableProperty.Create(
        nameof(SomenteLeitura), typeof(bool), typeof(CampoPrazo), false, propertyChanged: (b, _, n) => ((CampoPrazo)b).TrocarLeitura((bool)n));

    private readonly Label _rotulo = new() { Text = "Prazo" };
    private readonly Entry _dias = new() { Placeholder = "dias", Keyboard = Keyboard.Numeric, MaxLength = 5 };
    private readonly Button _prontos = new() { Text = "▾", FontSize = 15, Padding = 0, WidthRequest = 36, MinimumWidthRequest = 0, MinimumHeightRequest = 0,
        BackgroundColor = Colors.Transparent, BorderWidth = 0, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Fill };
    private readonly CaixaMarcar _fimDeSemana = new() { Texto = "Não terminar em fim de semana", Padding = 0, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Label _resumo = new() { LineBreakMode = LineBreakMode.WordWrap };

    /// <summary>O prazo escolhido aqui (dias digitados ou pronto); nulo = o Fim veio do formulário ou foi digitado.</summary>
    private PrazoPronto? _escolha;
    private bool _aplicando;
    /// <summary>Último ajuste de fim de semana feito aqui (desmarcar a opção devolve o Fim original).</summary>
    private AjusteFimDeSemana? _ajusteFeito;
    private Action? _fecharLista;

    public CampoPrazo()
    {
        Estilo(_rotulo, "Rotulo");
        Estilo(_resumo, "Secundario");
        _fimDeSemana.Detalhe = _resumo; // o resumo fica embaixo do texto da opção, alinhado com ele
        _prontos.SetAppThemeColor(Button.TextColorProperty, Cor("Primaria"), Cor("PrimariaEscuro"));
        SemanticProperties.SetDescription(_prontos, "Prazos prontos");
        ToolTipProperties.SetText(_prontos, "Escolher um prazo pronto (30 dias, 60 dias, 1 ano...)");
        _prontos.Clicked += (_, _) => AbrirProntos();
        _prontos.IsVisible = Popover.Disponivel;
        _dias.TextChanged += (_, e) => AoDigitarDias(e.NewTextValue);
        _dias.HandlerChanged += (_, _) => AjustarTextoNativo();
        _fimDeSemana.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CaixaMarcar.Marcado) && !_aplicando) Aplicar(); };

        Content = new VerticalStackLayout
        {
            Spacing = 2,
            Children = { _rotulo, new Grid { Children = { _dias, _prontos } }, _fimDeSemana }
        };
        Atualizar();
    }

    public string Inicio { get => (string)GetValue(InicioProperty); set => SetValue(InicioProperty, value); }
    public string Fim { get => (string)GetValue(FimProperty); set => SetValue(FimProperty, value); }
    public object? Registro { get => GetValue(RegistroProperty); set => SetValue(RegistroProperty, value); }
    public bool SomenteLeitura { get => (bool)GetValue(SomenteLeituraProperty); set => SetValue(SomenteLeituraProperty, value); }

    private DateOnly? DataInicio => TextoTela.DataCompleta(Inicio);
    private DateOnly? DataFim => TextoTela.DataCompleta(Fim);

    private void Esquecer()
    {
        _escolha = null;
        _ajusteFeito = null;
        _aplicando = true;
        try { _fimDeSemana.Marcado = false; }
        finally { _aplicando = false; }
        Atualizar();
    }

    private void AoMudarInicio()
    {
        if (_escolha is not null) Aplicar(); // o prazo foi escolhido aqui: o Fim acompanha o Início
        else Atualizar();
    }

    private void AoMudarFim()
    {
        if (_aplicando) return;
        // O Fim mudou fora daqui (digitado, calendário ou outro registro): os dias seguem o Fim.
        _escolha = null;
        _ajusteFeito = null;
        Atualizar();
    }

    private void AoDigitarDias(string? texto)
    {
        if (_aplicando) return;
        _escolha = CalculoPrazo.LerDias(texto) is { } n ? new PrazoPronto(n.ToString(), Dias: n) : null;
        if (_escolha is not null) Aplicar();
        else Atualizar(manterDigitado: true);
    }

    private void Escolher(PrazoPronto prazo)
    {
        _fecharLista?.Invoke();
        _escolha = prazo;
        Aplicar();
    }

    /// <summary>Calcula o Fim pelo prazo escolhido (e, marcado, tira do fim de semana) e mostra o resumo.</summary>
    private void Aplicar()
    {
        if (SomenteLeitura || DataInicio is not { } inicio) { Atualizar(); return; }
        DateOnly? fimBase = _escolha is not null ? CalculoPrazo.Fim(inicio, _escolha)
            : _ajusteFeito is { } feito && DataFim == feito.Ajustado ? feito.Original
            : DataFim;
        if (fimBase is not { } fb) { Atualizar(); return; }
        var ajuste = _fimDeSemana.Marcado ? CalculoPrazo.ForaDoFimDeSemana(fb) : null;
        var fim = ajuste?.Ajustado ?? fb;
        _ajusteFeito = ajuste;
        _aplicando = true;
        try { Fim = CalculoPrazo.Texto(fim); }
        finally { _aplicando = false; }
        Atualizar(ajuste: ajuste);
    }

    /// <summary>Mostra os dias do período e o resumo ("30 dias (03/10 a 01/11)" ou o aviso do fim de semana).</summary>
    private void Atualizar(bool manterDigitado = false, AjusteFimDeSemana? ajuste = null)
    {
        var inicio = DataInicio;
        var fim = DataFim;
        if (!manterDigitado)
        {
            int? dias = _escolha?.Dias is { } escolhidos ? escolhidos // o que foi digitado fica (o resumo mostra o total)
                : inicio is { } i && fim is { } f ? CalculoPrazo.Dias(i, f) : null;
            _aplicando = true;
            try { _dias.Text = dias?.ToString() ?? string.Empty; }
            finally { _aplicando = false; }
        }
        var aviso = ajuste is not null;
        _resumo.Text = aviso ? CalculoPrazo.Aviso(ajuste!) + " " + CalculoPrazo.Resumo(inicio!.Value, fim!.Value)
            : inicio is null ? "Informe o início para calcular o fim."
            : fim is { } ff ? CalculoPrazo.Resumo(inicio.Value, ff)
            : "Digite os dias ou escolha um prazo pronto no ▾.";
        _resumo.SetAppThemeColor(Label.TextColorProperty, Cor(aviso ? "Aviso" : "TextoSecundario"), Cor(aviso ? "AvisoFundo" : "TextoSecundarioEscuro"));
        _resumo.FontAttributes = aviso ? FontAttributes.Bold : FontAttributes.None;
    }

    private void TrocarLeitura(bool somenteLeitura)
    {
        _dias.IsReadOnly = somenteLeitura;
        _prontos.IsEnabled = !somenteLeitura;
        _fimDeSemana.IsEnabled = !somenteLeitura;
    }

    /// <summary>
    /// Prazos prontos do cadastro "Prazos de período" (menu do usuário › Administração), lidos pelo
    /// <see cref="Lone.Cliente.Api.PrazosProntos"/>; sem ele (ou sem resposta), os de antes do cadastro.
    /// </summary>
    private async void AbrirProntos()
    {
        if (SomenteLeitura) return;
        IReadOnlyList<PrazoPronto> prazos = CalculoPrazo.Padrao;
        if (Handler?.MauiContext?.Services.GetService(typeof(Lone.Cliente.Api.PrazosProntos)) is Lone.Cliente.Api.PrazosProntos fonte)
        {
            try { prazos = await fonte.ObterAsync(); }
            catch (Exception) { /* fica com os de antes do cadastro */ }
        }
        if (Handler is null) return; // a tela fechou enquanto lia
        var lista = new VerticalStackLayout { Spacing = 0, Padding = 4, WidthRequest = 180 };
        foreach (var prazo in prazos)
        {
            var botao = new Button
            {
                Text = prazo.Nome, FontSize = 14, Padding = new Thickness(12, 0), HeightRequest = 36, MinimumHeightRequest = 0,
                BackgroundColor = Colors.Transparent, BorderWidth = 0, HorizontalOptions = LayoutOptions.Fill
            };
            botao.SetAppThemeColor(Button.TextColorProperty, Cor("Texto"), Cor("TextoEscuro"));
            botao.Clicked += (_, _) => Escolher(prazo);
            lista.Add(botao);
        }
        _fecharLista = Popover.Mostrar(_prontos, lista);
    }

    /// <summary>No Windows: espaço à direita do texto para o ▾ e sem o "X" de apagar do WinUI (como no campo de data).</summary>
    private void AjustarTextoNativo()
    {
#if WINDOWS
        if (_dias.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.TextBox texto) return;
        void Ajustar()
        {
            var p = texto.Padding;
            texto.Padding = new Microsoft.UI.Xaml.Thickness(p.Left, p.Top, 36, p.Bottom);
            texto.TextAlignment = Microsoft.UI.Xaml.TextAlignment.Left;
        }
        if (texto.IsLoaded) Ajustar();
        else texto.Loaded += (_, _) => Ajustar();
#endif
    }

    private static Color Cor(string chave) =>
        Application.Current?.Resources.TryGetValue(chave, out var c) == true && c is Color cor ? cor : Colors.Gray;
}
