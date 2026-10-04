using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Domain.Comum;

namespace Lone.Cliente.ViewModels.Comum;

/// <summary>Um item escolhível: a chave gravada (Id ou código) e o texto mostrado.</summary>
/// <param name="BuscaExtra">Termos a mais para a busca (ex.: o código sem hífen), que não aparecem no texto.</param>
/// <param name="Detalhe">Texto secundário mostrado à direita na sugestão (ex.: "CBO 2521-05").</param>
/// <param name="Grupo">Ordem dos grupos nas sugestões (0 primeiro: ex.: profissões cadastradas antes das da CBO).</param>
public sealed record ItemSeletor(string Chave, string Texto, string? BuscaExtra = null, string? Detalhe = null, int Grupo = 0)
{
    public bool TemDetalhe => !string.IsNullOrEmpty(Detalhe);

    /// <summary>Texto completo para a dica do mouse e o Narrador (a sugestão pode cortar o nome com reticências).</summary>
    public string Descricao => TemDetalhe ? $"{Texto} ({Detalhe})" : Texto;

    /// <summary>Sem acento e em maiúsculas, para a busca.</summary>
    public string Busca { get; } = TextoBusca.Normalizar(BuscaExtra is null ? Texto : Texto + " " + BuscaExtra);

    public override string ToString() => Texto;
}

/// <summary>
/// Escolha de um item de uma lista já carregada (profissão, ocupação CBO...), com autocompletar: digita parte do
/// nome e escolhe. Só vale o que for escolhido da lista; texto digitado sem escolher fica pendente e a ficha não
/// deixa salvar. Filtra no aparelho, sem acento e sem diferença de maiúsculas. Mesmo comportamento do município.
/// </summary>
public sealed partial class SeletorDeLista : ObservableObject
{
    public const int MaximoSugestoes = 8;

    private IReadOnlyList<ItemSeletor> _itens = [];
    private bool _definindo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pendente))]
    private string _texto = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Chave), nameof(Pendente), nameof(Escolhido))]
    private ItemSeletor? _selecionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso))]
    private string _aviso = string.Empty;

    public ObservableCollection<ItemSeletor> Sugestoes { get; } = new();

    public string Dica { get; set; } = "Digite parte do nome e escolha na lista";
    public bool MostrarSugestoes => Sugestoes.Count > 0;
    public bool TemAviso => Aviso.Length > 0;
    public string? Chave => Selecionado?.Chave;
    public bool Escolhido => Selecionado is not null;
    public bool Vazio => _itens.Count == 0;
    public IReadOnlyList<ItemSeletor> Itens => _itens;

    /// <summary>Digitou algo mas não escolheu da lista.</summary>
    public bool Pendente => Selecionado is null && !string.IsNullOrWhiteSpace(Texto);

    /// <summary>Troca a lista oferecida (mantém o escolhido, que pode estar fora dela, ex.: desativado).</summary>
    public void DefinirItens(IReadOnlyList<ItemSeletor> itens)
    {
        _itens = itens;
        OnPropertyChanged(nameof(Vazio));
    }

    /// <summary>Mostra o item gravado sem abrir sugestões.</summary>
    public void Definir(ItemSeletor? item)
    {
        _definindo = true;
        try
        {
            Selecionado = item;
            Texto = item?.Texto ?? string.Empty;
            LimparSugestoes();
            Aviso = string.Empty;
        }
        finally
        {
            _definindo = false;
        }
    }

    [RelayCommand]
    public void Escolher(ItemSeletor? item)
    {
        if (item is null) return;
        Definir(item);
    }

    /// <summary>Erro para a ficha (nulo = ok). Vazio é aceito: quem exige preenchimento é quem usa.</summary>
    public string? Validar(string rotulo) =>
        Pendente ? $"{rotulo}: \"{Texto.Trim()}\" não foi escolhido na lista. Digite e escolha (ou apague o texto)." : null;

    partial void OnTextoChanged(string value)
    {
        if (_definindo) return;
        if (Selecionado is not null && value == Selecionado.Texto) return;
        Selecionado = null;
        Filtrar();
    }

    /// <summary>Sugestões para o texto atual. Texto exato e único é escolhido sozinho.</summary>
    internal void Filtrar()
    {
        var termo = TextoBusca.Normalizar(Texto);
        if (termo.Length == 0)
        {
            LimparSugestoes();
            Aviso = string.Empty;
            return;
        }

        var exatos = _itens.Where(i => TextoBusca.Normalizar(i.Texto) == termo).ToList();
        if (exatos.Count == 1)
        {
            Escolher(exatos[0]);
            return;
        }

        var encontrados = _itens
            .Where(i => i.Busca.Contains(termo, StringComparison.Ordinal))
            .OrderBy(i => i.Grupo)
            .ThenBy(i => i.Busca.StartsWith(termo, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(i => i.Busca, StringComparer.Ordinal)
            .Take(MaximoSugestoes)
            .ToList();

        Sugestoes.Clear();
        foreach (var item in encontrados) Sugestoes.Add(item);
        OnPropertyChanged(nameof(MostrarSugestoes));
        Aviso = encontrados.Count > 0 ? string.Empty
            : _itens.Count == 0 ? "A lista está vazia."
            : $"Nada encontrado com \"{Texto.Trim()}\".";
    }

    private void LimparSugestoes()
    {
        if (Sugestoes.Count == 0) return;
        Sugestoes.Clear();
        OnPropertyChanged(nameof(MostrarSugestoes));
    }
}
