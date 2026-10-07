using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Contracts.Municipios;
using Lone.Domain.Comum;

namespace Lone.Cliente.ViewModels.Comum;

/// <summary>
/// Escolha de município com autocompletar: primeiro a UF, depois o nome ("Jura..." → "Juramento - MG").
/// Só vale o que for escolhido da lista (o Id do IBGE); texto digitado sem escolher fica pendente e a ficha
/// não deixa salvar. A lista da UF é lida uma vez e filtrada no aparelho (sem acento e sem diferença de maiúsculas).
/// </summary>
public sealed partial class SeletorMunicipio : ObservableObject
{
    public const int MaximoSugestoes = 8;

    /// <summary>
    /// As 27 UFs em ordem alfabética (array: a lista de escolha precisa de IList). A fonte é a do domínio
    /// (<see cref="Lone.Domain.Validacao.Ufs.Todas"/>): uma lista só para validar e para escolher.
    /// </summary>
    public static readonly string[] Ufs = [.. global::Lone.Domain.Validacao.Ufs.Todas.Order(StringComparer.Ordinal)];

    private IReadOnlyList<(MunicipioDto Municipio, string Busca)> _lista = [];
    private string? _listaDaUf;
    private bool _definindo;
    private int _versaoBusca;

    /// <summary>Definida pela tela: lê os municípios de uma UF (a API guarda a lista em memória).</summary>
    public Func<string, Task<IReadOnlyList<MunicipioDto>>>? Fonte { get; set; }

    public IReadOnlyList<string> ListaUfs => Ufs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDigitar), nameof(Dica))]
    private string? _uf;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pendente))]
    private string _texto = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MunicipioId), nameof(Pendente), nameof(Escolhido))]
    private MunicipioDto? _selecionado;

    [ObservableProperty] private bool _carregando;

    /// <summary>Explica por que não há sugestão (não encontrado, tabela ainda não carregada, falha de rede).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso))]
    private string _aviso = string.Empty;

    public ObservableCollection<MunicipioDto> Sugestoes { get; } = new();

    public bool PodeDigitar => Uf is { Length: 2 };
    public bool MostrarSugestoes => Sugestoes.Count > 0;
    public bool TemAviso => Aviso.Length > 0;
    public int? MunicipioId => Selecionado?.Id;
    public bool Escolhido => Selecionado is not null;

    /// <summary>Digitou algo mas não escolheu da lista.</summary>
    public bool Pendente => Selecionado is null && !string.IsNullOrWhiteSpace(Texto);

    public string Dica => PodeDigitar ? "Digite parte do nome e escolha na lista" : "Escolha a UF primeiro";

    /// <summary>Mostra o município gravado sem consultar nada (a lista só é lida quando o usuário digitar).</summary>
    public void Definir(int? id, string? nome, string? uf)
    {
        _definindo = true;
        try
        {
            Uf = string.IsNullOrWhiteSpace(uf) ? null : uf.Trim().ToUpperInvariant();
            Selecionado = id is { } codigo && !string.IsNullOrWhiteSpace(nome)
                ? new MunicipioDto { Id = codigo, Nome = nome.Trim(), Uf = Uf ?? string.Empty }
                : null;
            Texto = Selecionado?.Nome ?? string.Empty;
            LimparSugestoes();
            Aviso = string.Empty;
        }
        finally
        {
            _definindo = false;
        }
    }

    [RelayCommand]
    public void Escolher(MunicipioDto? municipio)
    {
        if (municipio is null) return;
        _definindo = true;
        try
        {
            Uf = municipio.Uf;
            Selecionado = municipio;
            Texto = municipio.Nome;
        }
        finally
        {
            _definindo = false;
        }
        LimparSugestoes();
        Aviso = string.Empty;
    }

    /// <summary>Erro para a ficha (nulo = ok). Vazio é aceito: quem exige preenchimento é quem usa.</summary>
    public string? Validar(string rotulo) =>
        Pendente ? $"{rotulo}: \"{Texto.Trim()}\" não foi escolhido na lista. Digite e escolha o município (ou apague o texto)." : null;

    partial void OnUfChanged(string? value)
    {
        if (_definindo) return;
        if (Selecionado is not null && Selecionado.Uf != value)
        {
            Selecionado = null;
            _definindo = true;
            try { Texto = string.Empty; }
            finally { _definindo = false; }
        }
        LimparSugestoes();
        Aviso = string.Empty;
        if (PodeDigitar && Texto.Trim().Length > 0) _ = FiltrarAsync();
    }

    partial void OnTextoChanged(string value)
    {
        if (_definindo) return;
        if (Selecionado is not null && value == Selecionado.Nome) return;
        Selecionado = null;
        _ = FiltrarAsync();
    }

    /// <summary>Sugestões para o texto atual. Nome exato e único na UF é escolhido sozinho.</summary>
    internal async Task FiltrarAsync()
    {
        var versao = ++_versaoBusca;
        var uf = Uf;
        if (!PodeDigitar || TextoBusca.Normalizar(Texto).Length == 0)
        {
            LimparSugestoes();
            Aviso = PodeDigitar || Texto.Trim().Length == 0 ? string.Empty : "Escolha a UF primeiro.";
            return;
        }

        if (_listaDaUf != uf)
        {
            if (Fonte is null) return;
            Carregando = true;
            try
            {
                var lista = await Fonte(uf!);
                _lista = lista.Select(m => (m, TextoBusca.Normalizar(m.Nome))).ToList();
                _listaDaUf = lista.Count > 0 ? uf : null;
            }
            catch (Exception ex)
            {
                Aviso = $"Não foi possível carregar os municípios: {ex.GetBaseException().Message}";
                return;
            }
            finally
            {
                Carregando = false;
            }
            if (versao != _versaoBusca) return; // o usuário continuou digitando: vale a busca mais nova
        }

        if (_lista.Count == 0)
        {
            LimparSugestoes();
            Aviso = "A tabela de municípios ainda não foi carregada no servidor (é baixada do IBGE). Tente em alguns minutos.";
            return;
        }

        var termo = TextoBusca.Normalizar(Texto);
        var exatos = _lista.Where(x => x.Busca == termo).ToList();
        if (exatos.Count == 1)
        {
            Escolher(exatos[0].Municipio);
            return;
        }

        var encontrados = _lista
            .Where(x => x.Busca.Contains(termo, StringComparison.Ordinal))
            .OrderBy(x => x.Busca.StartsWith(termo, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(x => x.Busca, StringComparer.Ordinal)
            .Take(MaximoSugestoes)
            .Select(x => x.Municipio)
            .ToList();

        Sugestoes.Clear();
        foreach (var m in encontrados) Sugestoes.Add(m);
        OnPropertyChanged(nameof(MostrarSugestoes));
        Aviso = encontrados.Count == 0 ? $"Nenhum município com \"{Texto.Trim()}\" em {uf}." : string.Empty;
    }

    private void LimparSugestoes()
    {
        if (Sugestoes.Count == 0) return;
        Sugestoes.Clear();
        OnPropertyChanged(nameof(MostrarSugestoes));
    }
}
