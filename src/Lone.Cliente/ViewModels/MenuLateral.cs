using CommunityToolkit.Mvvm.ComponentModel;

namespace Lone.Cliente.ViewModels;

/// <summary>
/// Um item do menu lateral. <see cref="Rota"/> é a rota do Shell; <see cref="RotasRelacionadas"/> são telas abertas a
/// partir dele que continuam destacando este item (ex.: "Papéis" destaca "Configurações de Pessoas").
/// </summary>
public sealed partial class ItemMenu : ObservableObject
{
    public ItemMenu(string texto, string rota, bool configuracao = false, IReadOnlyList<string>? rotasRelacionadas = null)
    {
        Texto = texto;
        Rota = rota;
        Configuracao = configuracao;
        RotasRelacionadas = rotasRelacionadas ?? [];
    }

    public string Texto { get; }
    public string Rota { get; }

    /// <summary>Item "⚙ Configurações de ..." do módulo (desenhado com a engrenagem).</summary>
    public bool Configuracao { get; }
    public IReadOnlyList<string> RotasRelacionadas { get; }

    /// <summary>Texto com a engrenagem nos itens de configuração.</summary>
    public string TextoExibido => Configuracao ? "⚙  " + Texto : Texto;

    [ObservableProperty] private bool _ativo;

    /// <summary>A tela aberta é deste item (a própria rota ou uma relacionada).</summary>
    public bool Corresponde(string rotaAtual) => rotaAtual == Rota || RotasRelacionadas.Contains(rotaAtual);
}

/// <summary>
/// Seção do menu: um módulo com título (PESSOAS, ORGANIZAÇÃO, METAS) e seus itens, ou uma entrada solta sem título
/// (Início, Configurações do sistema). Um módulo novo (Vendas, Estoque...) é só mais uma seção.
/// </summary>
public sealed class SecaoMenu
{
    public SecaoMenu(string? titulo, IReadOnlyList<ItemMenu> itens)
    {
        Titulo = titulo;
        Itens = itens;
    }

    public string? Titulo { get; }
    public bool TemTitulo => Titulo is not null;
    public IReadOnlyList<ItemMenu> Itens { get; }
}
