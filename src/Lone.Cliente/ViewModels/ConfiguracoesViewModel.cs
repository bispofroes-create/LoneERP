using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Sessao;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Seguranca;

namespace Lone.Cliente.ViewModels;

/// <summary>Um cadastro de apoio numa página de configurações. <see cref="Rota"/> é a rota da tela no Shell.</summary>
public sealed record ItemConfiguracao(string Titulo, string Descricao, string Rota);

/// <summary>Grupo de uma página de configurações (ex.: "Cadastros auxiliares").</summary>
public sealed record GrupoConfiguracao(string Titulo, IReadOnlyList<ItemConfiguracao> Itens);

/// <summary>
/// Decisão de UX do Lone: cada módulo tem as SUAS configurações ("⚙ Configurações de Pessoas", "de Metas"...); a página
/// "Configurações do sistema" fica só com o que é transversal. Um módulo novo (Vendas, Estoque...) entra aqui com uma
/// constante, o título e os itens — nada de catálogo único de configurações.
/// </summary>
public static class ModulosConfiguracao
{
    public const string Pessoas = "pessoas";
    public const string Organizacao = "organizacao";
    public const string Metas = "metas";
    public const string Sistema = "sistema";

    public static IReadOnlyList<string> Todos { get; } = [Pessoas, Organizacao, Metas, Sistema];

    /// <summary>Rota da página de configurações do módulo no Shell (ex.: "configuracoes-pessoas").</summary>
    public static string Rota(string modulo) => "configuracoes-" + modulo;

    /// <summary>Módulo a partir da rota atual do Shell (ex.: "//configuracoes-pessoas" → "pessoas"); nulo se não for uma.</summary>
    public static string? DaRota(string? rota)
    {
        if (string.IsNullOrEmpty(rota)) return null;
        var ultimo = rota.TrimEnd('/').Split('/').LastOrDefault() ?? string.Empty;
        var modulo = ultimo.StartsWith("configuracoes-", StringComparison.Ordinal) ? ultimo["configuracoes-".Length..] : null;
        return modulo is not null && Todos.Contains(modulo) ? modulo : null;
    }

    public static string Titulo(string modulo) => modulo switch
    {
        Pessoas => "Configurações de Pessoas",
        Organizacao => "Configurações de Organização",
        Metas => "Configurações de Metas",
        _ => "Configurações do sistema"
    };

    public static string Descricao(string modulo) => modulo switch
    {
        Pessoas => "Cadastros de apoio e personalização usados na ficha de pessoas.",
        Organizacao => "Estrutura da empresa usada nos vínculos e lotações dos colaboradores.",
        Metas => "Equipes e indicadores usados nas metas.",
        _ => "Configurações que valem para o sistema inteiro."
    };
}

/// <summary>
/// Página de configurações de um módulo (ou do sistema). Cada item aparece com a mesma permissão que o cadastro exige;
/// as telas e as rotas dos cadastros não mudaram.
/// </summary>
public partial class ConfiguracoesViewModel : ViewModelBase
{
    private readonly SessaoCliente _sessao;

    public ConfiguracoesViewModel(SessaoCliente sessao) => _sessao = sessao;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titulo), nameof(Descricao))]
    private string _modulo = ModulosConfiguracao.Sistema;

    public string Titulo => ModulosConfiguracao.Titulo(Modulo);
    public string Descricao => ModulosConfiguracao.Descricao(Modulo);

    public ObservableCollection<GrupoConfiguracao> Grupos { get; } = new();
    public bool Vazio => Grupos.Count == 0;

    /// <summary>Definido pela página: abre a tela da rota (o Shell pergunta antes se houver alterações não salvas).</summary>
    public Func<string, Task>? Navegar { get; set; }

    /// <summary>Refaz a página com as permissões atuais (chamado quando a página aparece).</summary>
    [RelayCommand]
    private void Atualizar()
    {
        Grupos.Clear();
        foreach (var grupo in Montar(_sessao.Possui, Modulo)) Grupos.Add(grupo);
        OnPropertyChanged(nameof(Vazio));
    }

    [RelayCommand]
    private Task AbrirAsync(ItemConfiguracao? item) =>
        item is null || Navegar is null ? Task.CompletedTask : Navegar(item.Rota);

    private static readonly (string Modulo, string Grupo, string Permissao, ItemConfiguracao Item)[] Catalogo =
    [
        // ---- Pessoas ----
        (ModulosConfiguracao.Pessoas, "Cadastros auxiliares", Permissoes.Cadastros.Papeis, new ItemConfiguracao("Papéis", "Cliente, fornecedor, colaborador...", "papeis")),
        (ModulosConfiguracao.Pessoas, "Cadastros auxiliares", Permissoes.Cadastros.Profissoes, new ItemConfiguracao("Profissões", "Profissões com a ocupação da CBO", "profissoes")),
        (ModulosConfiguracao.Pessoas, "Cadastros auxiliares", Permissoes.Cadastros.Tipos, new ItemConfiguracao("Tipos de documento", "RG, CNH, alvará... com validade e aviso", "tipos-documento")),
        (ModulosConfiguracao.Pessoas, "Cadastros auxiliares", Permissoes.Cadastros.Tipos, new ItemConfiguracao("Tipos de endereço", "Sede, filial, depósito, residência...", "tipos-endereco")),
        (ModulosConfiguracao.Pessoas, "Cadastros auxiliares", Permissoes.Cadastros.Tipos, new ItemConfiguracao("Tipos de telefone e e-mail", "Comercial, residencial, pessoal...", "tipos-meio-contato")),
        (ModulosConfiguracao.Pessoas, "Personalização", Permissoes.Cadastros.CamposPersonalizados,
            new ItemConfiguracao("Campos personalizados", "Informações adicionais das pessoas e dos documentos", "campos-personalizados")),
        (ModulosConfiguracao.Pessoas, "Personalização", Permissoes.Cadastros.Etiquetas, new ItemConfiguracao("Etiquetas", "Marcadores livres para agrupar e filtrar pessoas", "etiquetas")),
        (ModulosConfiguracao.Pessoas, "Privacidade", Permissoes.Cadastros.Tipos,
            new ItemConfiguracao("Finalidades de tratamento", "Para quê a pessoa pode ser contatada (LGPD)", "finalidades-tratamento")),
        // Usados na aba Comercial da ficha (cliente e fornecedor). Vão para "Configurações Comerciais" quando o módulo existir.
        (ModulosConfiguracao.Pessoas, "Comercial (cliente e fornecedor)", Permissoes.Cadastros.Comercial,
            new ItemConfiguracao("Condições de pagamento", "Parcelas, acréscimo e desconto", "condicoes-pagamento")),
        (ModulosConfiguracao.Pessoas, "Comercial (cliente e fornecedor)", Permissoes.Cadastros.Comercial,
            new ItemConfiguracao("Perfis comerciais", "Limite, desconto e aprovação padrão", "perfis-comerciais")),
        (ModulosConfiguracao.Pessoas, "Comercial (cliente e fornecedor)", Permissoes.Cadastros.Comercial,
            new ItemConfiguracao("Tipos de carteira", "Vendedor, representante, televendas...", "tipos-carteira")),
        // ---- Organização ----
        (ModulosConfiguracao.Organizacao, "Estrutura organizacional", Permissoes.Cadastros.EstruturaOrganizacional, new ItemConfiguracao("Cargos", "Cargos dos colaboradores", "cargos")),
        (ModulosConfiguracao.Organizacao, "Estrutura organizacional", Permissoes.Cadastros.EstruturaOrganizacional, new ItemConfiguracao("Centros de custo", "Árvore de centros de custo", "centros-custo")),
        (ModulosConfiguracao.Organizacao, "Estrutura organizacional", Permissoes.Cadastros.EstruturaOrganizacional, new ItemConfiguracao("Departamentos", "Departamentos da empresa", "departamentos")),
        (ModulosConfiguracao.Organizacao, "Estrutura organizacional", Permissoes.Cadastros.EstruturaOrganizacional, new ItemConfiguracao("Setores", "Setores de cada departamento", "setores")),
        // ---- Metas ----
        (ModulosConfiguracao.Metas, "Cadastros de metas", Permissoes.Metas.Gerenciar, new ItemConfiguracao("Equipes", "Equipes e seus membros", "equipes")),
        (ModulosConfiguracao.Metas, "Cadastros de metas", Permissoes.Metas.Gerenciar, new ItemConfiguracao("Indicadores", "O que as metas medem", "indicadores")),
        // ---- Sistema (transversal) ----
        (ModulosConfiguracao.Sistema, "Usuários e permissões", Permissoes.Seguranca.GerenciarPerfis, new ItemConfiguracao("Perfis de acesso", "Permissões por perfil e empresa", "perfis")),
        (ModulosConfiguracao.Sistema, "Usuários e permissões", Permissoes.Seguranca.GerenciarUsuarios, new ItemConfiguracao("Usuários", "Quem acessa o sistema", "usuarios")),
    ];

    /// <summary>Grupos do módulo, na ordem do catálogo; itens em ordem alfabética. Grupo sem item permitido não aparece.</summary>
    public static IReadOnlyList<GrupoConfiguracao> Montar(Func<string, bool> possui, string modulo)
    {
        var doModulo = Catalogo.Where(t => t.Modulo == modulo).ToList();
        var ordem = doModulo.Select(t => t.Grupo).Distinct().ToList();
        return doModulo
            .Where(t => possui(t.Permissao))
            .GroupBy(t => t.Grupo)
            .OrderBy(g => ordem.IndexOf(g.Key))
            .Select(g => new GrupoConfiguracao(g.Key,
                g.Select(t => t.Item).OrderBy(i => i.Titulo, StringComparer.Create(TextoTela.Brasil, ignoreCase: true)).ToList()))
            .ToList();
    }

    /// <summary>O módulo tem alguma configuração que o usuário pode abrir (o item "⚙ Configurações" do menu aparece).</summary>
    public static bool AlgumaPermitida(Func<string, bool> possui, string modulo) => Montar(possui, modulo).Count > 0;

    /// <summary>Rotas dos cadastros do módulo (o menu destaca "Configurações de X" enquanto um deles está aberto).</summary>
    public static IReadOnlyList<string> RotasDoModulo(string modulo) =>
        Catalogo.Where(t => t.Modulo == modulo).Select(t => t.Item.Rota).ToList();

    /// <summary>Módulo dono de um cadastro (ex.: "papeis" → pessoas); nulo se a rota não for de configuração.</summary>
    public static string? ModuloDoCadastro(string rota) => Catalogo.FirstOrDefault(t => t.Item.Rota == rota).Modulo;
}
