using Lone.Cliente.ViewModels.Cadastros;

namespace Lone.App.Controles;

/// <summary>
/// Arruma uma tela "lista + ficha" conforme a largura: larga, lista fixa à esquerda e ficha ao lado;
/// estreita (celular ou janela pequena), uma coluna só, mostrando a lista ou a ficha. Só aparência:
/// quem decide o que está aberto é o ViewModel.
/// </summary>
public sealed class LayoutMestreDetalhe
{
    /// <summary>Abaixo desta largura (em pontos) a tela passa a mostrar um painel de cada vez.</summary>
    private const double LarguraCompacta = 760;
    private const double LarguraListaPadrao = 340;

    private readonly ContentPage _pagina;
    private readonly Grid _grade;
    private readonly IMestreDetalhe _viewModel;
    private readonly double _larguraLista;

    /// <param name="grade">Grade com duas colunas: 0 = lista, 1 = ficha.</param>
    /// <param name="larguraLista">Largura da lista no computador (ex.: a árvore de territórios precisa de mais espaço).</param>
    public LayoutMestreDetalhe(ContentPage pagina, Grid grade, IMestreDetalhe viewModel, double larguraLista = LarguraListaPadrao)
    {
        _pagina = pagina;
        _grade = grade;
        _viewModel = viewModel;
        _larguraLista = larguraLista;

        pagina.SizeChanged += (_, _) => Aplicar();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IMestreDetalhe.MostrarLista) or nameof(IMestreDetalhe.MostrarFicha))
                Aplicar();
        };
    }

    /// <summary>Botão voltar do Android com a ficha aberta no celular: volta para a lista.</summary>
    public bool TratarVoltar()
    {
        if (!_viewModel.ModoCompacto || !_viewModel.Editando) return false;
        _viewModel.FecharFichaCommand.Execute(null);
        return true;
    }

    private void Aplicar()
    {
        if (_pagina.Width <= 0) return;

        var compacto = _pagina.Width < LarguraCompacta;
        if (_viewModel.ModoCompacto != compacto)
            _viewModel.ModoCompacto = compacto;

        _grade.ColumnDefinitions[0].Width = compacto
            ? (_viewModel.MostrarLista ? GridLength.Star : new GridLength(0))
            : new GridLength(_larguraLista);
        _grade.ColumnDefinitions[1].Width = compacto && !_viewModel.MostrarFicha ? new GridLength(0) : GridLength.Star;
    }
}
