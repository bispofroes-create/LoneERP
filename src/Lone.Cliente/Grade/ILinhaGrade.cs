using System.ComponentModel;

namespace Lone.Cliente.Grade;

/// <summary>
/// O mínimo que a grade precisa de uma linha, sem conhecer a tela (Pessoas, Produtos...). A seleção é do Lone, pela
/// <see cref="Chave"/> (não pela seleção nativa do motor), e o destaque e a seleção vêm do modelo.
/// </summary>
public interface ILinhaGrade : INotifyPropertyChanged
{
    /// <summary>Identificação estável do registro (sobrevive a releituras e à reciclagem do visual).</summary>
    Guid Chave { get; }

    /// <summary>Uma célula por coluna, na ordem das colunas da grade.</summary>
    IReadOnlyList<CelulaGrade> Celulas { get; }

    /// <summary>Mouse em cima (a parte fixa e as células acendem juntas).</summary>
    bool Destacada { get; }

    /// <summary>A linha marcada (registro selecionado ou na prévia).</summary>
    bool Selecionada { get; }
}
