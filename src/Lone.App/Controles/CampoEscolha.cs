using System.Collections;
using System.Collections.Specialized;

namespace Lone.App.Controles;

/// <summary>Rótulo + lista de escolha. O texto de cada item é o ToString() dele.</summary>
public sealed class CampoEscolha : ContentView, ICampoValidavel
{
    public static readonly BindableProperty RotuloProperty = BindableProperty.Create(
        nameof(Rotulo), typeof(string), typeof(CampoEscolha), string.Empty,
        propertyChanged: (b, _, n) => ((CampoEscolha)b)._rotulo.Text = (string)n);

    public static readonly BindableProperty ItensProperty = BindableProperty.Create(
        nameof(Itens), typeof(IList), typeof(CampoEscolha), null,
        propertyChanged: (b, o, n) => ((CampoEscolha)b).TrocarItens(o as IList, n as IList));

    public static readonly BindableProperty SelecionadoProperty = BindableProperty.Create(
        nameof(Selecionado), typeof(object), typeof(CampoEscolha), null, BindingMode.TwoWay,
        propertyChanged: (b, _, _) => ((CampoEscolha)b).MostrarSelecionado());

    public static readonly BindableProperty DicaProperty = BindableProperty.Create(
        nameof(Dica), typeof(string), typeof(CampoEscolha), string.Empty,
        propertyChanged: (b, _, n) => ((CampoEscolha)b)._lista.Title = (string)n);

    private readonly Label _rotulo = new();
    private readonly Picker _lista = new();

    /// <summary>Ligado enquanto o próprio controle ajusta o Picker (não é escolha do usuário).</summary>
    private bool _sincronizando;

    public CampoEscolha()
    {
        _rotulo.SetDynamicResource(StyleProperty, "Rotulo");
        _lista.SelectedIndexChanged += (_, _) =>
        {
            // -1 acontece ao trocar ou mexer na lista de itens; não apaga a escolha feita.
            if (!_sincronizando && _lista.SelectedIndex >= 0) Selecionado = _lista.SelectedItem;
        };
        _marca = new MarcaDeErro(_lista);
        Content = new VerticalStackLayout { Spacing = 2, Children = { _rotulo, _lista, _marca.Rotulo } };
    }

    private readonly MarcaDeErro _marca;

    /// <summary>Marca de erro (Lone Contextual, Fase 1): borda e "⚠ mensagem" embaixo; nulo tira.</summary>
    public void MostrarErro(string? mensagem) => _marca.Mostrar(mensagem);

    public bool Focar() => _lista.IsEnabled && _lista.Focus();

    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }
    public IList? Itens { get => (IList?)GetValue(ItensProperty); set => SetValue(ItensProperty, value); }
    public object? Selecionado { get => GetValue(SelecionadoProperty); set => SetValue(SelecionadoProperty, value); }
    public string Dica { get => (string)GetValue(DicaProperty); set => SetValue(DicaProperty, value); }

    private void TrocarItens(IList? antigos, IList? novos)
    {
        if (antigos is INotifyCollectionChanged a) a.CollectionChanged -= Itens_CollectionChanged;
        if (novos is INotifyCollectionChanged n) n.CollectionChanged += Itens_CollectionChanged;

        _sincronizando = true;
        try { _lista.ItemsSource = novos; }
        finally { _sincronizando = false; }
        MostrarSelecionado();
    }

    /// <summary>
    /// Item incluído ou removido: o Picker reposiciona a seleção por índice e poderia marcar outro item.
    /// Este aviso chega antes do Picker (a inscrição é feita antes de entregar a lista a ele), então a mudança
    /// de índice que vem em seguida é ignorada; depois a seleção volta a ser a do ViewModel.
    /// </summary>
    private void Itens_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _sincronizando = true;
        Dispatcher.Dispatch(() =>
        {
            _sincronizando = false;
            MostrarSelecionado();
        });
    }

    private void MostrarSelecionado()
    {
        _sincronizando = true;
        try { _lista.SelectedItem = Selecionado; }
        finally { _sincronizando = false; }
    }
}
