using Lone.Cliente.ViewModels.Comum;

namespace Lone.App.Controles;

/// <summary>
/// Rótulo + caixa de texto. Usado em todas as fichas para os campos ficarem iguais.
/// Com Mascara, formata enquanto o usuário digita (data, CPF, CNPJ, CEP, telefone).
/// </summary>
public sealed class Campo : ContentView
{
    public static readonly BindableProperty RotuloProperty = BindableProperty.Create(
        nameof(Rotulo), typeof(string), typeof(Campo), string.Empty,
        propertyChanged: (b, _, n) =>
        {
            var campo = (Campo)b;
            campo._rotulo.Text = (string)n;
            SemanticProperties.SetDescription(campo._entrada, (string)n); // leitor de tela: o rótulo nomeia a caixa
        });

    public static readonly BindableProperty TextoProperty = BindableProperty.Create(
        nameof(Texto), typeof(string), typeof(Campo), string.Empty, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((Campo)b)._entrada.Text = (string?)n ?? string.Empty);

    public static readonly BindableProperty DicaProperty = BindableProperty.Create(
        nameof(Dica), typeof(string), typeof(Campo), string.Empty,
        propertyChanged: (b, _, n) => ((Campo)b)._entrada.Placeholder = (string)n);

    public static readonly BindableProperty TecladoProperty = BindableProperty.Create(
        nameof(Teclado), typeof(Keyboard), typeof(Campo), Keyboard.Default,
        propertyChanged: (b, _, n) => ((Campo)b)._entrada.Keyboard = (Keyboard)n);

    public static readonly BindableProperty SomenteLeituraProperty = BindableProperty.Create(
        nameof(SomenteLeitura), typeof(bool), typeof(Campo), false,
        propertyChanged: (b, _, n) => ((Campo)b)._entrada.IsReadOnly = (bool)n);

    public static readonly BindableProperty MascaraProperty = BindableProperty.Create(
        nameof(Mascara), typeof(TipoMascara), typeof(Campo), TipoMascara.Nenhuma,
        propertyChanged: (b, o, n) => ((Campo)b).TrocarMascara((TipoMascara)o, (TipoMascara)n));

    /// <summary>Mensagem de erro do campo (vazio = sem erro): aparece logo abaixo, em vermelho, com o rótulo em vermelho.</summary>
    public static readonly BindableProperty ErroProperty = BindableProperty.Create(
        nameof(Erro), typeof(string), typeof(Campo), string.Empty,
        propertyChanged: (b, _, _) => ((Campo)b).MostrarEstado());

    /// <summary>Conferido e correto (ex.: CPF válido): mostra ✓ ao lado do rótulo.</summary>
    public static readonly BindableProperty ValidoProperty = BindableProperty.Create(
        nameof(Valido), typeof(bool), typeof(Campo), false,
        propertyChanged: (b, _, _) => ((Campo)b).MostrarEstado());

    /// <summary>Executado quando o foco sai do campo (validação imediata: CPF, CNPJ, e-mail...).</summary>
    public static readonly BindableProperty AoSairProperty = BindableProperty.Create(
        nameof(AoSair), typeof(System.Windows.Input.ICommand), typeof(Campo));

    private readonly Label _rotulo = new();
    private readonly Label _ok = new() { Text = "✓", FontAttributes = FontAttributes.Bold, IsVisible = false, Margin = new Thickness(6, 0, 0, 0) };
    private readonly Entry _entrada = new();
    private readonly Label _erro = new() { FontSize = 12, IsVisible = false, LineBreakMode = LineBreakMode.WordWrap };

    public Campo()
    {
        _rotulo.SetDynamicResource(StyleProperty, "Rotulo");
        _ok.SetDynamicResource(Label.TextColorProperty, "Sucesso");
        _erro.SetDynamicResource(Label.TextColorProperty, "Erro");
        _entrada.TextChanged += Entrada_TextChanged;
        _entrada.Unfocused += (_, _) => { if (AoSair?.CanExecute(null) == true) AoSair.Execute(null); };
        MostrarEstado();
        Content = new VerticalStackLayout
        {
            Spacing = 2,
            Children = { new HorizontalStackLayout { Children = { _rotulo, _ok } }, _entrada, _erro }
        };
    }

    public string Erro { get => (string)GetValue(ErroProperty); set => SetValue(ErroProperty, value); }
    public bool Valido { get => (bool)GetValue(ValidoProperty); set => SetValue(ValidoProperty, value); }
    public System.Windows.Input.ICommand? AoSair { get => (System.Windows.Input.ICommand?)GetValue(AoSairProperty); set => SetValue(AoSairProperty, value); }

    /// <summary>Rótulo vermelho e a mensagem logo abaixo (erro) ou ✓ (válido); o erro também é lido pelo leitor de tela.</summary>
    private void MostrarEstado()
    {
        var erro = Erro ?? string.Empty;
        _erro.Text = erro;
        _erro.IsVisible = erro.Length > 0;
        _ok.IsVisible = Valido && erro.Length == 0;
        if (erro.Length > 0) _rotulo.SetDynamicResource(Label.TextColorProperty, "Erro");
        else _rotulo.ClearValue(Label.TextColorProperty); // volta à cor do estilo "Rotulo"
        SemanticProperties.SetHint(_entrada, erro);
    }

    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }
    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public string Dica { get => (string)GetValue(DicaProperty); set => SetValue(DicaProperty, value); }
    public Keyboard Teclado { get => (Keyboard)GetValue(TecladoProperty); set => SetValue(TecladoProperty, value); }
    public bool SomenteLeitura { get => (bool)GetValue(SomenteLeituraProperty); set => SetValue(SomenteLeituraProperty, value); }
    public TipoMascara Mascara { get => (TipoMascara)GetValue(MascaraProperty); set => SetValue(MascaraProperty, value); }

    private void Entrada_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var texto = e.NewTextValue ?? string.Empty;
        var formatado = global::Lone.Cliente.ViewModels.Comum.Mascara.Aplicar(Mascara, texto);
        if (formatado != texto)
        {
            // Reescreve com a máscara; o novo TextChanged chega aqui já formatado e só atualiza o valor.
            _entrada.Text = formatado;
            _entrada.CursorPosition = formatado.Length;
            return;
        }
        // Só quando mudou: definir o valor de uma propriedade ligada em OneWay (ex.: a idade calculada) faz o MAUI
        // desfazer a ligação, e o campo congelaria no primeiro valor.
        if (!string.Equals(Texto, texto, StringComparison.Ordinal)) Texto = texto;
    }

    /// <summary>Máscaras que mudam o texto (Número e E-mail só escolhem o teclado).</summary>
    private static bool Formata(TipoMascara m) => m is not (TipoMascara.Nenhuma or TipoMascara.Numero or TipoMascara.Email);

    /// <summary>
    /// Teclado adequado à máscara (números no celular), a menos que a tela tenha escolhido outro.
    /// Só reformata o que já estava mascarado (ex.: telefone → celular). Texto livre (e-mail, identificação
    /// estrangeira) não é convertido, para não virar outra coisa nem ser apagado ao trocar o tipo.
    /// </summary>
    private void TrocarMascara(TipoMascara anterior, TipoMascara mascara)
    {
        if (IsSet(TecladoProperty) is false)
            _entrada.Keyboard = mascara switch
            {
                TipoMascara.Data or TipoMascara.Cpf or TipoMascara.Cep or TipoMascara.Hora or TipoMascara.DataHora
                    or TipoMascara.Numero => Keyboard.Numeric,
                TipoMascara.Telefone => Keyboard.Telephone,
                TipoMascara.Email => Keyboard.Email,
                _ => Keyboard.Default
            };
        if (Formata(anterior) && Formata(mascara))
            _entrada.Text = global::Lone.Cliente.ViewModels.Comum.Mascara.Aplicar(mascara, _entrada.Text);
    }
}
