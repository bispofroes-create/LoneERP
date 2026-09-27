using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Domain.Comum;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Papéis na Identificação da ficha. Padrão do Lone: o cadastro auxiliar oferece as opções; a ficha mostra só o que a
/// pessoa tem ou já teve. Os outros aparecem em "Adicionar papel" (painel com pesquisa). Ligar/desligar muda só a
/// ficha em memória; ao salvar, a regra de sempre vale (desligar encerra o período, religar começa outro, nada é
/// apagado) e a auditoria registra quem fez e o motivo.
/// </summary>
public sealed partial class PapeisDaFicha : ObservableObject
{
    private readonly IReadOnlyList<PapelOpcao> _todos;
    private bool _podeEditar = true;

    public PapeisDaFicha(IReadOnlyList<PapelOpcao> todos)
    {
        _todos = todos;
        foreach (var papel in todos)
            papel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PapelOpcao.Ativo)) Atualizar();
            };
        Atualizar();
    }

    /// <summary>Cartões da ficha, na ordem do cadastro de papéis.</summary>
    public ObservableCollection<PapelOpcao> NaFicha { get; } = new();
    public bool Vazio => NaFicha.Count == 0;

    /// <summary>Opções do painel "Adicionar papel": ainda não ativas, que o usuário pode ligar, filtradas pela pesquisa.</summary>
    public ObservableCollection<PapelOpcao> Disponiveis { get; } = new();
    public bool SemDisponiveis => Disponiveis.Count == 0;
    public string TextoSemDisponiveis => Busca.Trim().Length > 0
        ? "Nenhum papel encontrado."
        : "Todos os papéis disponíveis já estão atribuídos a esta pessoa.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoSemDisponiveis))]
    private string _busca = string.Empty;

    /// <summary>Painel "Adicionar papel" aberto.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBotaoAdicionar))]
    private bool _escolhendo;

    public string TextoBotaoAdicionar => Escolhendo ? "Fechar" : "+ Adicionar papel";

    /// <summary>Quem só consulta vê os papéis e o histórico, mas não liga, desliga nem adiciona.</summary>
    public bool PodeEditar => _podeEditar;

    /// <summary>
    /// Mesmas permissões da gravação, também na tela: editar a pessoa e, para "Empresa do grupo",
    /// PESSOAS.EMPRESAS_DO_GRUPO. Papel desativado no cadastro não começa período novo.
    /// </summary>
    public void DefinirPermissoes(bool podeEditar, bool podeEmpresasDoGrupo)
    {
        _podeEditar = podeEditar;
        foreach (var papel in _todos)
            papel.Editavel = podeEditar
                             && (papel.Papel != TipoPapel.EmpresaDoGrupo || podeEmpresasDoGrupo)
                             && (papel.Ativo || papel.PodeReativar);
        if (!podeEditar) Escolhendo = false;
        OnPropertyChanged(nameof(PodeEditar));
        AtualizarDisponiveis();
    }

    [RelayCommand]
    private void AlternarEscolha()
    {
        if (!PodeEditar) return;
        Busca = string.Empty;
        Escolhendo = !Escolhendo;
    }

    /// <summary>Liga o papel escolhido (o período começa ao salvar) e fecha o painel.</summary>
    [RelayCommand]
    private void Adicionar(PapelOpcao? papel)
    {
        if (papel is null || !papel.Editavel || papel.Ativo) return;
        papel.Ativo = true;
        Escolhendo = false;
        Busca = string.Empty;
    }

    partial void OnBuscaChanged(string value) => AtualizarDisponiveis();

    private void Atualizar()
    {
        ColecaoSincronizada.Sincronizar(NaFicha, _todos.Where(p => p.NaFicha).ToList());
        OnPropertyChanged(nameof(Vazio));
        AtualizarDisponiveis();
    }

    private void AtualizarDisponiveis()
    {
        var termo = TextoBusca.Normalizar(Busca);
        var opcoes = _todos
            .Where(p => !p.Ativo && p.Editavel && p.PodeReativar)
            .Where(p => termo.Length == 0 || TextoBusca.Normalizar($"{p.Nome} {p.Descricao}").Contains(termo, StringComparison.Ordinal))
            .ToList();
        ColecaoSincronizada.Sincronizar(Disponiveis, opcoes);
        OnPropertyChanged(nameof(SemDisponiveis));
    }
}
