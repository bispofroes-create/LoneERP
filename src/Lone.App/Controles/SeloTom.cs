namespace Lone.App.Controles;

/// <summary>
/// Selo pequeno com texto e tom ("Informacao", "Aviso", "Erro", "Sucesso", "Grupo" ou "Neutro"), nos temas claro e escuro. O
/// texto sempre aparece: a cor só ajuda (nunca é a única indicação). Usado nos papéis da lista de pessoas.
/// </summary>
public sealed class SeloTom : Border
{
    public static readonly BindableProperty TextoProperty = BindableProperty.Create(
        nameof(Texto), typeof(string), typeof(SeloTom), string.Empty,
        propertyChanged: (b, _, novo) => ((SeloTom)b)._rotulo.Text = novo as string ?? string.Empty);

    public static readonly BindableProperty TomProperty = BindableProperty.Create(
        nameof(Tom), typeof(string), typeof(SeloTom), "Neutro",
        propertyChanged: (b, _, _) => ((SeloTom)b).Pintar());

    private readonly Label _rotulo;

    public SeloTom()
    {
        if (Recurso("Selo") is Style selo) Style = selo;
        _rotulo = new Label();
        if (Recurso("SeloTexto") is Style texto) _rotulo.Style = texto;
        Content = _rotulo;
        Pintar();
    }

    public string Texto
    {
        get => (string)GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    public string Tom
    {
        get => (string)GetValue(TomProperty);
        set => SetValue(TomProperty, value);
    }

    private void Pintar()
    {
        var (fundo, fundoEscuro, texto, textoEscuro) = Tom switch
        {
            "Informacao" => ("InformacaoFundo", "InformacaoFundoEscuro", "Informacao", "PrimariaEscuro"),
            "Aviso" => ("AvisoFundo", "AvisoFundoEscuro", "Aviso", "TextoEscuro"),
            "Erro" => ("ErroFundo", "ErroFundoEscuro", "Erro", "TextoEscuro"),
            "Sucesso" => ("SucessoFundo", "SucessoFundoEscuro", "Sucesso", "TextoEscuro"),
            "Grupo" => ("GrupoFundo", "GrupoFundoEscuro", "Grupo", "GrupoEscuro"),
            _ => ("SeloNeutroFundo", "SeloNeutroFundoEscuro", "TextoSecundario", "TextoSecundarioEscuro")
        };
        this.SetAppThemeColor(BackgroundColorProperty, Cor(fundo), Cor(fundoEscuro));
        _rotulo.SetAppThemeColor(Label.TextColorProperty, Cor(texto), Cor(textoEscuro));
    }

    private static object? Recurso(string chave) =>
        Application.Current?.Resources.TryGetValue(chave, out var valor) == true ? valor : null;

    private static Color Cor(string chave) => Recurso(chave) as Color ?? Colors.Gray;
}
