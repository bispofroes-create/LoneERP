using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Comum;

/// <summary>Uma linha do resumo de erros: o erro e, se ele tiver campo, o "ir para".</summary>
public sealed class ItemValidacao
{
    public ItemValidacao(ErroValidacao erro) => Erro = erro;

    public ErroValidacao Erro { get; }
    public string Mensagem => Erro.Mensagem;
    public bool TemCampo => Erro.TemCampo;

    /// <summary>A seta logo depois do texto, só quando leva a um campo.</summary>
    public string Seta => TemCampo ? "   →" : string.Empty;

    /// <summary>Para o Narrador: o que o toque faz.</summary>
    public string Descricao => TemCampo ? $"Ir para o campo: {Mensagem}" : Mensagem;
}

/// <summary>
/// Resumo de erros de um formulário (Lone Contextual, Fase 1: o erro leva ao campo). Guarda os erros da última conferência
/// (do app ou da API), diz se um campo tem erro e pede para levar a um campo. <b>Só muda numa nova conferência</b>
/// (<see cref="Definir"/> ou <see cref="Limpar"/>): mexer no campo não tira o erro, porque ainda não se sabe se ficou
/// certo (decisão do usuário, 04/10/2026). Não é um sistema de notificações: só os erros que impedem gravar o formulário.
/// Quem decide a aba é o formulário (<see cref="AntesDeIr"/>); quem acha o controle, rola e põe o foco é a tela, que ouve
/// <see cref="FocoPedido"/>.
/// </summary>
public sealed partial class ResumoValidacao : ObservableObject
{
    public ObservableCollection<ItemValidacao> Itens { get; } = new();

    /// <summary>Título do resumo (ex.: "Não foi possível salvar a pessoa").</summary>
    [ObservableProperty] private string _titulo = "Não foi possível salvar";

    public bool Visivel => Itens.Count > 0;

    /// <summary>"Corrija 1 informação para continuar." / "Corrija 3 informações para continuar."</summary>
    public string Contagem => Itens.Count == 1 ? "Corrija 1 informação para continuar." : $"Corrija {Itens.Count} informações para continuar.";

    /// <summary>Mais de 4 erros: a lista rola dentro do resumo (a ficha não perde espaço).</summary>
    public bool ListaLonga => Itens.Count > 4;

    /// <summary>Os erros mudaram (a tela atualiza a marca dos campos).</summary>
    public event Action? Mudou;

    /// <summary>A tela deve levar ao campo deste erro (rolar e pôr o foco), depois de o formulário trocar a aba.</summary>
    public event Action<ErroValidacao>? FocoPedido;

    /// <summary>O formulário prepara a ida (ex.: troca para a aba do campo). Falso = não há como ir (aba que não existe).</summary>
    public Func<ErroValidacao, bool>? AntesDeIr { get; set; }

    /// <summary>Troca os erros pelos da conferência nova (substitui; nada fica de antes).</summary>
    public void Definir(IEnumerable<ErroValidacao> erros)
    {
        Itens.Clear();
        foreach (var erro in erros) Itens.Add(new ItemValidacao(erro));
        Avisar();
    }

    /// <summary>Sem erros (gravou, abriu outra ficha, descartou).</summary>
    public void Limpar()
    {
        if (Itens.Count == 0) return;
        Itens.Clear();
        Avisar();
    }

    /// <summary>A mensagem do erro deste campo (do registro <paramref name="item"/>, se for de uma lista); nula = sem erro.</summary>
    public string? ErroDe(string campo, Guid? item) =>
        Itens.FirstOrDefault(i => i.Erro.Campo == campo && i.Erro.Item == item)?.Mensagem;

    /// <summary>Quantos erros satisfazem a condição (ex.: os de uma aba).</summary>
    public int Contar(Func<ErroValidacao, bool> condicao) => Itens.Count(i => condicao(i.Erro));

    /// <summary>O primeiro erro que tem campo, na ordem da conferência; nulo se só houver erros gerais.</summary>
    public ErroValidacao? PrimeiroComCampo => Itens.FirstOrDefault(i => i.TemCampo)?.Erro;

    /// <summary>Toque numa linha do resumo: leva ao campo (erro geral não leva a lugar nenhum).</summary>
    [RelayCommand]
    private void IrPara(ItemValidacao? item)
    {
        if (item is { TemCampo: true }) Ir(item.Erro);
    }

    /// <summary>Leva ao primeiro erro com campo (ao salvar com erro). Falso se não houver para onde ir.</summary>
    public bool IrParaPrimeiro() => PrimeiroComCampo is { } erro && Ir(erro);

    private bool Ir(ErroValidacao erro)
    {
        if (AntesDeIr is { } preparar && !preparar(erro)) return false;
        FocoPedido?.Invoke(erro);
        return true;
    }

    private void Avisar()
    {
        OnPropertyChanged(nameof(Visivel));
        OnPropertyChanged(nameof(Contagem));
        OnPropertyChanged(nameof(ListaLonga));
        Mudou?.Invoke();
    }
}
