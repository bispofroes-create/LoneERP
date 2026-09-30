using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>
/// Avisa quando qualquer coisa muda na ficha aberta — num campo dela ou em qualquer nível abaixo (endereços, contatos,
/// papéis, listas que ganham ou perdem itens). Base do Salvar habilitado só com alterações e do estado "Sem alterações" /
/// "Alterações não salvas" em todos os cadastros.
/// <para>
/// Só avisa; quem decide se há alteração continua sendo a comparação da ficha com a versão gravada
/// (<see cref="CadastroViewModelBase{TItem}.TemAlteracoes"/>) — mudar e voltar ao valor original não conta como alteração.
/// </para>
/// <para>
/// Percorre campos (não propriedades: não executa nenhum cálculo da ficha) de objetos observáveis do próprio Lone e das
/// coleções observáveis. Não entra em ViewModels de tela (ações da ficha apontam para eles) nem segue delegados. Quando
/// a estrutura muda (item incluído, objeto trocado), passa de novo por tudo. Ao fechar a ficha, solta todas as
/// inscrições (nenhuma referência fica presa).
/// </para>
/// </summary>
internal sealed class ObservadorFicha
{
    private const int ProfundidadeMaxima = 10;
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> CamposPorTipo = new();

    private readonly Action _mudou;
    private readonly List<INotifyPropertyChanged> _objetos = [];
    private readonly List<INotifyCollectionChanged> _colecoes = [];
    private object? _raiz;

    public ObservadorFicha(Action mudou) => _mudou = mudou;

    /// <summary>Quantos objetos e coleções estão sendo acompanhados (para os testes).</summary>
    internal int Acompanhados => _objetos.Count + _colecoes.Count;

    /// <summary>Passa a acompanhar esta ficha (nulo = nenhuma).</summary>
    public void Observar(object? raiz)
    {
        _raiz = raiz;
        Religar();
    }

    private void Religar()
    {
        foreach (var o in _objetos) o.PropertyChanged -= PropriedadeMudou;
        foreach (var c in _colecoes) c.CollectionChanged -= ColecaoMudou;
        _objetos.Clear();
        _colecoes.Clear();
        if (_raiz is not null) Percorrer(_raiz, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private void Percorrer(object objeto, int nivel, HashSet<object> vistos)
    {
        if (nivel > ProfundidadeMaxima || !vistos.Add(objeto)) return;

        if (objeto is INotifyCollectionChanged colecao)
        {
            colecao.CollectionChanged += ColecaoMudou;
            _colecoes.Add(colecao);
            if (objeto is IEnumerable itens)
                foreach (var item in itens)
                    if (item is not null && Acompanhavel(item)) Percorrer(item, nivel + 1, vistos);
            return;
        }

        if (objeto is INotifyPropertyChanged observavel)
        {
            observavel.PropertyChanged += PropriedadeMudou;
            _objetos.Add(observavel);
        }

        foreach (var campo in Campos(objeto.GetType()))
            if (campo.GetValue(objeto) is { } valor && Acompanhavel(valor))
                Percorrer(valor, nivel + 1, vistos);
    }

    /// <summary>Objetos observáveis do Lone e coleções observáveis; nunca ViewModels de tela.</summary>
    private static bool Acompanhavel(object valor) =>
        valor is not ViewModelBase &&
        (valor is INotifyCollectionChanged ||
         valor is INotifyPropertyChanged && valor.GetType().Namespace?.StartsWith("Lone.", StringComparison.Ordinal) == true);

    /// <summary>Campos de instância declarados pelos tipos do Lone (a hierarquia até sair do Lone), sem delegados.</summary>
    private static FieldInfo[] Campos(Type tipo) => CamposPorTipo.GetOrAdd(tipo, static t =>
    {
        var campos = new List<FieldInfo>();
        for (var atual = t; atual is not null && atual.Namespace?.StartsWith("Lone.", StringComparison.Ordinal) == true; atual = atual.BaseType)
            campos.AddRange(atual.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(c => !typeof(Delegate).IsAssignableFrom(c.FieldType)));
        return campos.ToArray();
    });

    private static readonly ConcurrentDictionary<(Type, string), bool> PodeTrocarEstrutura = new();

    private void PropriedadeMudou(object? sender, PropertyChangedEventArgs e)
    {
        // Texto, número, data: só avisa. Propriedade que guarda objeto ou coleção observável (ex.: trocou o endereço
        // inteiro): acompanha a estrutura nova. Decide pelo tipo declarado — não lê o valor (nenhum cálculo é executado).
        if (sender is not null && MudaEstrutura(sender.GetType(), e.PropertyName)) Religar();
        _mudou();
    }

    private static bool MudaEstrutura(Type tipo, string? propriedade)
    {
        if (string.IsNullOrEmpty(propriedade)) return true; // "tudo mudou"
        return PodeTrocarEstrutura.GetOrAdd((tipo, propriedade), static chave =>
        {
            PropertyInfo? info;
            try { info = chave.Item1.GetProperty(chave.Item2, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
            catch (AmbiguousMatchException) { return true; } // propriedade redeclarada ("new"): por segurança, religa
            if (info is null) return true;
            var t = info.PropertyType;
            return typeof(INotifyPropertyChanged).IsAssignableFrom(t) || typeof(INotifyCollectionChanged).IsAssignableFrom(t)
                   || t == typeof(object) || t.IsInterface;
        });
    }

    private void ColecaoMudou(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Religar();
        _mudou();
    }
}
