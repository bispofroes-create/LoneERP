namespace Lone.Cliente.Navegacao;

/// <summary>
/// "De onde eu vim?" — a pilha de lugares visitados (regra pura, testável). Não é a hierarquia (breadcrumb): quem chegou a
/// uma pessoa por um link continua sabendo que veio do link.
/// <para>Regras:</para>
/// <list type="bullet">
/// <item>Mesmo lugar de novo (duplo clique, releitura): não empilha; atualiza o título.</item>
/// <item>Registro novo que acabou de ser gravado: a entrada "Novo" vira o registro (não é outro passo).</item>
/// <item>Outro registro escolhido na lista da mesma tela: substitui (navegar entre registros não é histórico).</item>
/// <item>Link de outra tela que chega à tela e depois abre o registro: um passo só (o Voltar leva de volta à origem).</item>
/// <item>Chegar ao lugar anterior por qualquer caminho (Fechar, voltar do celular, Voltar): é voltar — desempilha.</item>
/// <item>Qualquer outro caso: empilha. No máximo <see cref="Maximo"/> entradas (as mais antigas saem).</item>
/// </list>
/// </summary>
public sealed class HistoricoNavegacao
{
    public const int Maximo = 50;

    private readonly List<EntradaNavegacao> _entradas = [];

    public IReadOnlyList<EntradaNavegacao> Entradas => _entradas;
    public EntradaNavegacao? Atual => _entradas.Count > 0 ? _entradas[^1] : null;
    public EntradaNavegacao? Anterior => _entradas.Count > 1 ? _entradas[^2] : null;
    public bool PodeVoltar => _entradas.Count > 1;

    /// <summary>Registra que o usuário está em <paramref name="entrada"/>. Devolve verdadeiro se o histórico mudou.</summary>
    public bool Registrar(EntradaNavegacao entrada)
    {
        var local = entrada.Local;
        if (Atual is not { } atual)
        {
            _entradas.Add(entrada);
            return true;
        }

        if (local.MesmoQue(atual.Local))
        {
            if (Equals(atual.Local, local)) return false;
            _entradas[^1] = atual with { Local = local }; // mesmo lugar, título novo
            return true;
        }

        var mesmaTela = string.Equals(atual.Local.Rota, local.Rota, StringComparison.Ordinal);
        if (mesmaTela && atual.Local.Registro is { Novo: true } && local.Registro is { Novo: false })
        {
            _entradas[^1] = atual with { Local = local }; // gravou o novo: continua sendo o mesmo passo
            return true;
        }

        if (Anterior is { } anterior && local.MesmoQue(anterior.Local))
        {
            _entradas.RemoveAt(_entradas.Count - 1);
            _entradas[^1] = anterior with { Local = local };
            return true;
        }

        if (mesmaTela && entrada.Origem == OrigemNavegacao.Lista && atual.Local.Registro is { Novo: false } && local.Registro is not null)
        {
            _entradas[^1] = entrada; // trocou de registro pela lista: navegação entre registros, não histórico
            return true;
        }

        if (mesmaTela && entrada.Origem == OrigemNavegacao.Link && atual.Origem == OrigemNavegacao.Link && atual.SoTela)
        {
            _entradas[^1] = entrada; // link de outra tela: chegar à tela e abrir o registro é um passo só
            return true;
        }

        _entradas.Add(entrada);
        if (_entradas.Count > Maximo) _entradas.RemoveAt(0);
        return true;
    }

    /// <summary>Volta até a entrada <paramref name="indice"/> (descarta as de cima), trocando o lugar pelo que de fato abriu.</summary>
    public void VoltarPara(int indice, LocalNavegacao local)
    {
        if (indice < 0 || indice >= _entradas.Count) return;
        _entradas.RemoveRange(indice + 1, _entradas.Count - indice - 1);
        _entradas[indice] = _entradas[indice] with { Local = local };
        // O registro de volta não abriu mais (ex.: excluído) e o lugar real é igual ao anterior: é o mesmo passo.
        if (indice > 0 && local.MesmoQue(_entradas[indice - 1].Local)) _entradas.RemoveAt(indice);
    }

    /// <summary>
    /// Guarda o retrato do estado da tela na entrada atual (o usuário está saindo dela). Só a entrada atual: o retrato de
    /// uma entrada anterior nunca é sobrescrito por uma navegação posterior.
    /// </summary>
    public void DefinirEstadoDaAtual(object estado)
    {
        if (_entradas.Count > 0) _entradas[^1] = _entradas[^1] with { Estado = estado };
    }

    /// <summary>Cópia independente (para prever o efeito de uma chegada sem mexer no histórico real).</summary>
    public HistoricoNavegacao Copia()
    {
        var copia = new HistoricoNavegacao();
        copia._entradas.AddRange(_entradas);
        return copia;
    }

    /// <summary>Tira uma entrada que não pode mais ser visitada (ex.: perdeu a permissão da tela).</summary>
    public void Remover(int indice)
    {
        if (indice >= 0 && indice < _entradas.Count - 1) _entradas.RemoveAt(indice);
    }

    public void Limpar() => _entradas.Clear();
}
