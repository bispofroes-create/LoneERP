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
        propertyChanged: (b, _, n) => ((Campo)b)._rotulo.Text = (string)n);

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

    private readonly Label _rotulo = new();
    private readonly Entry _entrada = new();

    public Campo()
    {
        _rotulo.SetDynamicResource(StyleProperty, "Rotulo");
        _entrada.TextChanged += Entrada_TextChanged;
        Content = new VerticalStackLayout { Spacing = 2, Children = { _rotulo, _entrada } };
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
        // Só quando mudou: definir localmente o valor de uma propriedade ligada em OneWay (ex.: a idade calculada)
        // faz o MAUI desfazer a ligação, e o campo congelava no primeiro valor recebido.
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
