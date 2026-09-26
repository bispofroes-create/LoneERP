using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Sessao;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Seguranca;

namespace Lone.Cliente.ViewModels;

/// <summary>Um cadastro de apoio na página Configurações. <see cref="Rota"/> é a rota da tela no menu (AppShell).</summary>
public sealed record ItemConfiguracao(string Titulo, string Descricao, string Rota);

/// <summary>Grupo da página Configurações (Pessoas, Comercial, Organização, Metas, Sistema).</summary>
public sealed record GrupoConfiguracao(string Titulo, IReadOnlyList<ItemConfiguracao> Itens);

/// <summary>
/// Página "Configurações": reúne os cadastros de apoio que antes ficavam soltos no menu lateral. O menu fica só com a
/// operação (Início, Pessoas, Consulta avançada, Grupos empresariais, Metas). Cada item aparece com a mesma permissão
/// que o item do menu exigia; as telas e as rotas não mudaram.
/// </summary>
public partial class ConfiguracoesViewModel : ViewModelBase
{
    private readonly SessaoCliente _sessao;

    public ConfiguracoesViewModel(SessaoCliente sessao) => _sessao = sessao;

    public ObservableCollection<GrupoConfiguracao> Grupos { get; } = new();
    public bool Vazio => Grupos.Count == 0;

    /// <summary>Definido pela página: abre a tela da rota (o Shell pergunta antes se houver alterações não salvas).</summary>
    public Func<string, Task>? Navegar { get; set; }

    /// <summary>Refaz a página com as permissões atuais (chamado quando a página aparece).</summary>
    [RelayCommand]
    private void Atualizar()
    {
        Grupos.Clear();
        foreach (var grupo in Montar(_sessao.Possui)) Grupos.Add(grupo);
        OnPropertyChanged(nameof(Vazio));
    }

    [RelayCommand]
    private Task AbrirAsync(ItemConfiguracao? item) =>
        item is null || Navegar is null ? Task.CompletedTask : Navegar(item.Rota);

    /// <summary>
    /// Grupos na ordem de uso; itens em ordem alfabética dentro do grupo. Grupo sem nenhum item permitido não aparece.
    /// </summary>
    public static IReadOnlyList<GrupoConfiguracao> Montar(Func<string, bool> possui)
    {
        (string Grupo, string Permissao, ItemConfiguracao Item)[] todos =
        [
            ("Pessoas", Permissoes.Cadastros.CamposPersonalizados,
                new ItemConfiguracao("Campos personalizados", "Informações adicionais das pessoas e dos documentos", "campos-personalizados")),
            ("Pessoas", Permissoes.Cadastros.Etiquetas, new ItemConfiguracao("Etiquetas", "Marcadores livres para agrupar e filtrar pessoas", "etiquetas")),
            ("Pessoas", Permissoes.Cadastros.Papeis, new ItemConfiguracao("Papéis", "Cliente, fornecedor, colaborador...", "papeis")),
            ("Pessoas", Permissoes.Cadastros.Profissoes, new ItemConfiguracao("Profissões", "Profissões com a ocupação da CBO", "profissoes")),
            ("Pessoas", Permissoes.Cadastros.Tipos,
                new ItemConfiguracao("Finalidades de tratamento", "Para quê a pessoa pode ser contatada (LGPD)", "finalidades-tratamento")),
            ("Pessoas", Permissoes.Cadastros.Tipos, new ItemConfiguracao("Tipos de documento", "RG, CNH, alvará... com validade e aviso", "tipos-documento")),
            ("Pessoas", Permissoes.Cadastros.Tipos, new ItemConfiguracao("Tipos de endereço", "Sede, filial, depósito, residência...", "tipos-endereco")),
            ("Pessoas", Permissoes.Cadastros.Tipos, new ItemConfiguracao("Tipos de telefone e e-mail", "Comercial, residencial, pessoal...", "tipos-meio-contato")),
            ("Comercial", Permissoes.Cadastros.Comercial, new ItemConfiguracao("Condições de pagamento", "Parcelas, acréscimo e desconto", "condicoes-pagamento")),
            ("Comercial", Permissoes.Cadastros.Comercial, new ItemConfiguracao("Perfis comerciais", "Limite, desconto e aprovação padrão", "perfis-comerciais")),
            ("Comercial", Permissoes.Cadastros.Comercial, new ItemConfiguracao("Tipos de carteira", "Vendedor, representante, televendas...", "tipos-carteira")),
            ("Organização", Permissoes.Cadastros.EstruturaOrganizacional, new ItemConfiguracao("Cargos", "Cargos dos colaboradores", "cargos")),
            ("Organização", Permissoes.Cadastros.EstruturaOrganizacional, new ItemConfiguracao("Centros de custo", "Árvore de centros de custo", "centros-custo")),
            ("Organização", Permissoes.Cadastros.EstruturaOrganizacional, new ItemConfiguracao("Departamentos", "Departamentos da empresa", "departamentos")),
            ("Organização", Permissoes.Cadastros.EstruturaOrganizacional, new ItemConfiguracao("Setores", "Setores de cada departamento", "setores")),
            ("Metas", Permissoes.Metas.Gerenciar, new ItemConfiguracao("Equipes", "Equipes e seus membros", "equipes")),
            ("Metas", Permissoes.Metas.Gerenciar, new ItemConfiguracao("Indicadores", "O que as metas medem", "indicadores")),
            ("Sistema", Permissoes.Seguranca.GerenciarPerfis, new ItemConfiguracao("Perfis de acesso", "Permissões por perfil e empresa", "perfis")),
            ("Sistema", Permissoes.Seguranca.GerenciarUsuarios, new ItemConfiguracao("Usuários", "Quem acessa o sistema", "usuarios")),
        ];
        string[] ordem = ["Pessoas", "Comercial", "Organização", "Metas", "Sistema"];

        return todos
            .Where(t => possui(t.Permissao))
            .GroupBy(t => t.Grupo)
            .OrderBy(g => Array.IndexOf(ordem, g.Key))
            .Select(g => new GrupoConfiguracao(g.Key,
                g.Select(t => t.Item).OrderBy(i => i.Titulo, StringComparer.Create(TextoTela.Brasil, ignoreCase: true)).ToList()))
            .ToList();
    }

    /// <summary>Alguma configuração visível: o item "Configurações" do menu aparece.</summary>
    public static bool AlgumaPermitida(Func<string, bool> possui) => Montar(possui).Count > 0;
}
