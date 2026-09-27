using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Domain.Comum;

namespace Lone.Cliente.ViewModels;

/// <summary>
/// Uma tela que o menu abre. <see cref="Rota"/> é a rota do Shell; <see cref="RotasRelacionadas"/> são telas abertas a
/// partir dela que continuam destacando este item (ex.: "Papéis" destaca "Configurações" de Pessoas).
/// Dentro do módulo aparece <see cref="Texto"/>, curto (o nome do módulo já está no cabeçalho); em Favoritos, Recentes e
/// na busca aparece <see cref="Descricao"/>, completo, com o <see cref="Caminho"/> de onde a tela fica.
/// </summary>
public sealed partial class ItemMenu : ObservableObject
{
    public const string RotaInicio = "inicio";

    public ItemMenu(string texto, string rota, bool configuracao = false, IReadOnlyList<string>? rotasRelacionadas = null,
        string? descricao = null, string? caminho = null)
    {
        Texto = texto;
        Rota = rota;
        Configuracao = configuracao;
        RotasRelacionadas = rotasRelacionadas ?? [];
        Descricao = descricao ?? texto;
        Caminho = caminho ?? string.Empty;
        ChaveBusca = TextoBusca.Normalizar($"{Descricao} {Texto} {Caminho}");
        DescricaoBusca = TextoBusca.Normalizar(Descricao);
    }

    public string Texto { get; }
    public string Rota { get; }

    /// <summary>Nome completo (ex.: "Configurações de Pessoas"): atalhos, busca e leitor de tela.</summary>
    public string Descricao { get; }

    /// <summary>Onde a tela fica no menu (ex.: "Pessoas › Configurações"); vazio para Início.</summary>
    public string Caminho { get; }
    public bool TemCaminho => Caminho.Length > 0;

    /// <summary>Item "⚙ Configurações" (desenhado com a engrenagem).</summary>
    public bool Configuracao { get; }
    public IReadOnlyList<string> RotasRelacionadas { get; }

    /// <summary>Texto dentro do módulo, com a engrenagem nos itens de configuração.</summary>
    public string TextoExibido => Configuracao ? "⚙  " + Texto : Texto;

    /// <summary>Descrição, caminho e texto sem acento e em maiúsculas (a busca compara nessa forma).</summary>
    public string ChaveBusca { get; }

    /// <summary>Só o nome completo, na mesma forma: decide a ordem dos resultados.</summary>
    public string DescricaoBusca { get; }

    [ObservableProperty] private bool _ativo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Estrela), nameof(DescricaoFavorito))]
    private bool _favorito;

    /// <summary>Início já é o primeiro item do menu: não entra nos favoritos.</summary>
    public bool PodeFavoritar => Rota != RotaInicio;

    public string Estrela => Favorito ? "★" : "☆";
    public string DescricaoFavorito => Favorito ? $"Remover {Descricao} dos favoritos" : $"Adicionar {Descricao} aos favoritos";

    /// <summary>A tela aberta é deste item (a própria rota ou uma relacionada).</summary>
    public bool Corresponde(string rotaAtual) => rotaAtual == Rota || RotasRelacionadas.Contains(rotaAtual);
}

/// <summary>
/// Seção do menu: um módulo (Pessoas, Organização, Metas) que abre e fecha ao tocar no nome, ou uma entrada solta sem
/// cabeçalho (Configurações do sistema), sempre visível. Início fica fora das seções, no topo (MenuViewModel.Inicio). Um módulo novo (Vendas, Estoque...) é só mais uma seção.
/// </summary>
public sealed partial class SecaoMenu : ObservableObject
{
    public SecaoMenu(string? titulo, IReadOnlyList<ItemMenu> itens)
    {
        Titulo = titulo;
        Itens = itens;
    }

    public string? Titulo { get; }
    public bool TemTitulo => Titulo is not null;
    public IReadOnlyList<ItemMenu> Itens { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarItens), nameof(RotacaoSeta), nameof(DescricaoCabecalho))]
    private bool _expandida;

    /// <summary>A tela aberta está dentro deste módulo (o cabeçalho fica destacado mesmo com o módulo fechado).</summary>
    [ObservableProperty] private bool _contemAtivo;

    /// <summary>Entradas soltas não têm cabeçalho e ficam sempre visíveis.</summary>
    public bool MostrarItens => !TemTitulo || Expandida;

    /// <summary>A seta "›" gira para baixo quando o módulo está aberto.</summary>
    public double RotacaoSeta => Expandida ? 90 : 0;
    public string DescricaoCabecalho => $"{Titulo}, {(Expandida ? "aberto" : "fechado")}";

    [RelayCommand]
    private void Alternar()
    {
        if (TemTitulo) Expandida = !Expandida;
    }
}

/// <summary>Favoritos ou Recentes: atalhos para telas de qualquer módulo, com o nome completo; abrem e fecham como os módulos.</summary>
public sealed partial class GrupoAtalhosMenu : ObservableObject
{
    public GrupoAtalhosMenu(string titulo) => Titulo = titulo;

    public string Titulo { get; }
    public ObservableCollection<ItemMenu> Itens { get; } = new();
    public bool TemItens => Itens.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RotacaoSeta), nameof(DescricaoCabecalho))]
    private bool _expandida = true;

    public double RotacaoSeta => Expandida ? 90 : 0;
    public string DescricaoCabecalho => $"{Titulo}, {(Expandida ? "aberto" : "fechado")}";

    [RelayCommand]
    private void Alternar() => Expandida = !Expandida;

    /// <summary>Troca os itens de uma vez (a lista é pequena) e avisa se ficou vazia ou deixou de ficar.</summary>
    public void Definir(IEnumerable<ItemMenu> itens)
    {
        Itens.Clear();
        foreach (var item in itens) Itens.Add(item);
        OnPropertyChanged(nameof(TemItens));
    }
}
