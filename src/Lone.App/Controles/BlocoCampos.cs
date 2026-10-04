using Lone.Cliente.Formularios;
using Microsoft.Maui.Layouts;

namespace Lone.App.Controles;

/// <summary>
/// Bloco de campos das fichas: os campos lado a lado na ordem, quebrando a linha quando o próximo não cabe; cada linha
/// com a altura do seu campo mais alto (o cálculo fica em <see cref="CalculoBlocoCampos"/>, testado).
/// Substitui o <c>FlexLayout Wrap="Wrap"</c> nas fichas: aquele repartia a altura do bloco igualmente entre as linhas e
/// cortava os campos mais altos (diagnóstico de 03/10/2026). Mantém o que o XAML já usa: a largura de cada campo vem de
/// <c>FlexLayout.Basis</c> (50% = 1 coluna da grade; 100% = linha inteira; sem base = largura natural) e
/// <see cref="AlignItems"/> alinha os campos de uma linha. Item escondido não ocupa espaço.
/// Grade do Lone (03/10/2026): 1 coluna até 600 de largura, 2 até 1300, 3 daí em diante. Campo de 2 colunas:
/// <c>c:BlocoCampos.Colunas="2"</c> (vale mais que a base).
/// </summary>
public sealed class BlocoCampos : Layout
{
    public static readonly BindableProperty AlignItemsProperty = BindableProperty.Create(
        nameof(AlignItems), typeof(FlexAlignItems), typeof(BlocoCampos), FlexAlignItems.Stretch,
        propertyChanged: (b, _, _) => ((BlocoCampos)b).InvalidateMeasure());

    /// <summary>Alinhamento vertical dos campos de uma linha (padrão: ocupam a altura da linha).</summary>
    public FlexAlignItems AlignItems
    {
        get => (FlexAlignItems)GetValue(AlignItemsProperty);
        set => SetValue(AlignItemsProperty, value);
    }

    /// <summary>Colunas da grade que o campo ocupa (1 ou 2; 0 = usa <c>FlexLayout.Basis</c>).</summary>
    public static readonly BindableProperty ColunasProperty = BindableProperty.CreateAttached(
        "Colunas", typeof(int), typeof(BlocoCampos), 0,
        propertyChanged: (b, _, _) => ((b as Element)?.Parent as BlocoCampos)?.InvalidateMeasure());

    /// <summary>
    /// Peso na divisão da sobra da linha (barra de filtros, 03/10/2026): com <c>FlexLayout.Basis</c> fixa como largura
    /// mínima, os itens com peso esticam até a linha ficar cheia (0 = sem peso).
    /// </summary>
    public static readonly BindableProperty PesoProperty = BindableProperty.CreateAttached(
        "Peso", typeof(double), typeof(BlocoCampos), 0d,
        propertyChanged: (b, _, _) => ((b as Element)?.Parent as BlocoCampos)?.InvalidateMeasure());

    public static double GetPeso(BindableObject campo) => (double)campo.GetValue(PesoProperty);

    public static void SetPeso(BindableObject campo, double valor) => campo.SetValue(PesoProperty, valor);

    public static int GetColunas(BindableObject campo) => (int)campo.GetValue(ColunasProperty);

    public static void SetColunas(BindableObject campo, int valor) => campo.SetValue(ColunasProperty, valor);

    protected override ILayoutManager CreateLayoutManager() => new Gerente(this);

    private sealed class Gerente(BlocoCampos bloco) : ILayoutManager
    {
        private IReadOnlyList<PosicaoItem> _posicoes = [];
        private double _largura = double.NaN;

        public Size Measure(double widthConstraint, double heightConstraint)
        {
            var padding = bloco.Padding;
            var largura = widthConstraint - padding.HorizontalThickness;
            if (double.IsInfinity(largura) || double.IsNaN(largura))
                largura = bloco.Where(v => v.Visibility != Visibility.Collapsed)
                    .Sum(v => v.Measure(double.PositiveInfinity, double.PositiveInfinity).Width);
            var altura = Calcular(Math.Max(0, largura));
            return new Size(largura + padding.HorizontalThickness, altura + padding.VerticalThickness);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            var padding = bloco.Padding;
            var largura = Math.Max(0, bounds.Width - padding.HorizontalThickness);
            // Normalmente a largura é a mesma da medição; se mudou, recalcula com a largura final.
            if (Math.Abs(largura - _largura) > 0.01) Calcular(largura);
            for (var i = 0; i < bloco.Count && i < _posicoes.Count; i++)
            {
                var filho = bloco[i];
                if (filho.Visibility == Visibility.Collapsed) continue;
                var p = _posicoes[i];
                filho.Arrange(new Rect(bounds.X + padding.Left + p.X, bounds.Y + padding.Top + p.Y, p.Largura, p.Altura));
            }
            return bounds.Size;
        }

        private double Calcular(double largura)
        {
            var filhos = bloco.ToList();
            var itens = filhos.Select(Item).ToList();
            _posicoes = CalculoBlocoCampos.Calcular(
                largura,
                itens,
                i => filhos[i].Measure(double.PositiveInfinity, double.PositiveInfinity).Width,
                (i, li) => filhos[i].Measure(li, double.PositiveInfinity).Height,
                bloco.AlignItems switch
                {
                    FlexAlignItems.Start => AlinhamentoLinha.Inicio,
                    FlexAlignItems.Center => AlinhamentoLinha.Centro,
                    FlexAlignItems.End => AlinhamentoLinha.Fim,
                    _ => AlinhamentoLinha.Esticar
                },
                out var altura);
            _largura = largura;
            return altura;
        }

        /// <summary>Visível e largura pedida (FlexLayout.Basis: relativa, fixa ou automática).</summary>
        private static ItemBloco Item(IView filho)
        {
            var visivel = filho.Visibility != Visibility.Collapsed;
            if (filho is not BindableObject b) return new ItemBloco(visivel);
            if (GetColunas(b) is var colunas and > 0) return new ItemBloco(visivel, Colunas: colunas);
            var basis = FlexLayout.GetBasis(b);
            var relativa = !basis.Equals(FlexBasis.Auto) && basis.Length <= 1 && basis.Equals(new FlexBasis(basis.Length, isRelative: true));
            if (GetPeso(b) is var peso and > 0 && !relativa)
                return new ItemBloco(visivel, LarguraFixa: basis.Equals(FlexBasis.Auto) ? 160 : basis.Length, Peso: peso);
            if (basis.Equals(FlexBasis.Auto)) return new ItemBloco(visivel);
            // IsRelative não é público no FlexBasis: a igualdade do struct compara valor e tipo da base. Base relativa só vai
            // de 0 a 1 (criar uma relativa com 220 lança exceção — fechava a Carteira vencendo, 03/10/2026): acima de 1 é fixa.
            return basis.Length <= 1 && basis.Equals(new FlexBasis(basis.Length, isRelative: true))
                ? new ItemBloco(visivel, Fracao: basis.Length)
                : new ItemBloco(visivel, LarguraFixa: basis.Length);
        }
    }
}
