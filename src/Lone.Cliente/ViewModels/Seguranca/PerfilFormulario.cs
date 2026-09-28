using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Seguranca;

/// <summary>Uma permissão do catálogo com a caixa de marcar da tela.</summary>
public sealed partial class PermissaoMarcavel : ObservableObject
{
    public PermissaoMarcavel(DefinicaoPermissao definicao, bool marcada)
    {
        Codigo = definicao.Codigo;
        Descricao = definicao.Descricao;
        _marcada = marcada;
    }

    public string Codigo { get; }
    public string Descricao { get; }

    [ObservableProperty] private bool _marcada;
}

/// <summary>Permissões de um módulo (Pessoas, Segurança...), mostradas juntas.</summary>
public sealed record GrupoPermissoes(string Modulo, IReadOnlyList<PermissaoMarcavel> Permissoes);

/// <summary>Ficha de um perfil em edição: nome, tipo (administrador ou não) e permissões marcadas.</summary>
public sealed partial class PerfilFormulario : ObservableObject
{
    private PerfilFormulario(Guid id, byte[]? versao, bool novo, IReadOnlyList<GrupoPermissoes> grupos)
    {
        Id = id;
        Versao = versao;
        Novo = novo;
        Grupos = grupos;
    }

    public Guid Id { get; }
    public byte[]? Versao { get; }
    public bool Novo { get; }
    public IReadOnlyList<GrupoPermissoes> Grupos { get; }

    public string Titulo => Novo ? "Novo perfil" : Nome;

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private bool _ativo = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarPermissoes))]
    private bool _administrador;

    /// <summary>Administrador tem todas as permissões: a lista some para não confundir.</summary>
    public bool MostrarPermissoes => !Administrador;

    /// <summary>Opções do alcance (array: o Picker precisa de IList).</summary>
    public static readonly Opcao<AlcanceComercial>[] Alcances =
    [
        new(AlcanceComercial.Tudo, "Tudo (toda a base)"),
        new(AlcanceComercial.MinhaEquipe, "Minha equipe e as de baixo"),
        new(AlcanceComercial.MinhaCarteira, "Minha carteira"),
        new(AlcanceComercial.Nenhum, "Nenhum cadastro de Pessoas")
    ];

    public IReadOnlyList<Opcao<AlcanceComercial>> ListaAlcances => Alcances;

    /// <summary>Até onde quem tem o perfil enxerga em Pessoas e no Comercial (vale a partir da Fase 2a-2).</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(AjudaAlcance))] private Opcao<AlcanceComercial> _alcance = Alcances[0];

    public string AjudaAlcance => Alcance.Valor switch
    {
        AlcanceComercial.MinhaEquipe => "Vê os clientes da própria carteira e da carteira de quem está nas equipes que lidera (e nas equipes abaixo delas). Precisa da pessoa ligada ao usuário.",
        AlcanceComercial.MinhaCarteira => "Vê só os clientes da própria carteira (e os de quem cobre numa ausência). Precisa da pessoa ligada ao usuário.",
        AlcanceComercial.Nenhum => "Não vê cadastros de Pessoas (perfis só de configuração ou de outros módulos).",
        _ => "Vê toda a base, como antes."
    };

    public static PerfilFormulario NovoPerfil(IReadOnlyList<DefinicaoPermissao> catalogo) =>
        new(IdSequencial.Novo(), null, novo: true, Agrupar(catalogo, new HashSet<string>()));

    public static PerfilFormulario De(PerfilDto dto, IReadOnlyList<DefinicaoPermissao> catalogo) =>
        new(dto.Id, dto.Versao, novo: false, Agrupar(catalogo, dto.Permissoes.ToHashSet()))
        {
            Nome = dto.Nome,
            Descricao = dto.Descricao ?? string.Empty,
            Administrador = dto.Administrador,
            Ativo = dto.Ativo,
            Alcance = Opcao.De(Alcances, dto.AlcanceComercial)
        };

    public PerfilDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome,
        Descricao = string.IsNullOrWhiteSpace(Descricao) ? null : Descricao,
        Administrador = Administrador,
        Ativo = Ativo,
        AlcanceComercial = Administrador ? AlcanceComercial.Tudo : Alcance.Valor,
        Permissoes = Administrador
            ? []
            : Grupos.SelectMany(g => g.Permissoes).Where(p => p.Marcada).Select(p => p.Codigo).ToList()
    };

    private static IReadOnlyList<GrupoPermissoes> Agrupar(IReadOnlyList<DefinicaoPermissao> catalogo, IReadOnlySet<string> marcadas) =>
        catalogo.GroupBy(p => p.Modulo)
                .Select(g => new GrupoPermissoes(g.Key, g.Select(p => new PermissaoMarcavel(p, marcadas.Contains(p.Codigo))).ToList()))
                .ToList();
}
