using Lone.Domain.Enums;

namespace Lone.Domain.Territorios;

/// <summary>Uma fixação vigente em D para o cliente no mapa (exceção Fixar).</summary>
public sealed record FixacaoVigente(Guid ExcecaoId, Guid TerritorioId);

/// <summary>
/// O que o motor precisa saber de um cliente em D, já calculado em lote fora do domínio: se está no universo do mapa, os
/// territórios cuja regra ele atende (candidatos), os territórios de onde foi retirado, as fixações vigentes e a
/// atribuição atual (a da véspera; no mapa não exclusivo, o conjunto).
/// </summary>
public sealed record ClienteParaResolver(
    Guid PessoaId,
    bool NoUniverso,
    IReadOnlySet<Guid> Candidatos,
    IReadOnlySet<Guid> Retirados,
    IReadOnlyList<FixacaoVigente> Fixacoes,
    IReadOnlySet<Guid> AtribuicoesAtuais);

/// <summary>
/// O mapa em D, igual para todos os clientes: exclusivo ou não, territórios ativos em D com o pai em D (a árvore em D,
/// para a especificidade) e, para cada território com regra em D, a regra e a prioridade dela.
/// </summary>
public sealed class MapaParaResolver
{
    public MapaParaResolver(bool exclusivo, IReadOnlyDictionary<Guid, Guid?> paiDosAtivos,
                            IReadOnlyDictionary<Guid, RegraParaResolver> regras)
    {
        Exclusivo = exclusivo;
        PaiDosAtivos = paiDosAtivos;
        Regras = regras;
    }

    public bool Exclusivo { get; }

    /// <summary>Território ativo em D → pai em D (nulo = raiz).</summary>
    public IReadOnlyDictionary<Guid, Guid?> PaiDosAtivos { get; }

    /// <summary>Território → a regra vigente (ou planejada) em D.</summary>
    public IReadOnlyDictionary<Guid, RegraParaResolver> Regras { get; }

    public bool Ativo(Guid territorio) => PaiDosAtivos.ContainsKey(territorio);

    /// <summary><paramref name="acima"/> é ancestral de <paramref name="abaixo"/> na árvore de D (resiste a ciclo gravado).</summary>
    public bool EstaAcima(Guid acima, Guid abaixo)
    {
        var atual = abaixo;
        for (var passos = 0; passos < RegrasArvoreTerritorial.ProfundidadeMaxima * 2; passos++)
        {
            if (!PaiDosAtivos.TryGetValue(atual, out var pai) || pai is not { } p) return false;
            if (p == acima) return true;
            atual = p;
        }
        return false;
    }
}

/// <summary>A regra de um território em D: o id da versão (a planejada tem id próprio) e a prioridade (nula = sem).</summary>
public sealed record RegraParaResolver(Guid RegraId, int? Prioridade);

/// <summary>Um território no resultado, com a origem (regra e versão, ou a exceção).</summary>
public sealed record TerritorioDecidido(Guid TerritorioId, OrigemAtribuicaoTerritorio Origem, Guid? RegraId, Guid? ExcecaoId);

/// <summary>
/// A decisão para um cliente, com a explicação: o resultado, o passo que decidiu, os territórios (um no exclusivo; zero ou
/// mais no não exclusivo), o estado de cada território envolvido e as exceções envolvidas.
/// </summary>
public sealed record DecisaoTerritorial(
    Guid PessoaId,
    ResultadoAtribuicao Resultado,
    PassoDecisaoTerritorial Passo,
    IReadOnlyList<TerritorioDecidido> Territorios,
    IReadOnlyDictionary<Guid, EstadoCandidatoTerritorial> Estados,
    IReadOnlyList<Guid> ExcecoesEnvolvidas)
{
    /// <summary>Conflito de fixação ou fixação inválida: nada é gravado para ele e a aplicação fica bloqueada (T3).</summary>
    public bool Inconsistente => Resultado is ResultadoAtribuicao.ConflitoDeFixacao or ResultadoAtribuicao.FixacaoInvalida;

    /// <summary>
    /// Os territórios que ficam gravados depois da decisão. Inconsistência: os atuais, sem mexer (nada é escolhido).
    /// PermaneceEmConflito: o atual (manter não é escolher).
    /// </summary>
    public IReadOnlySet<Guid> TerritoriosFinais(IReadOnlySet<Guid> atuais) =>
        Inconsistente ? atuais : Territorios.Select(t => t.TerritorioId).ToHashSet();
}

/// <summary>
/// O motor de atribuição territorial (T3), exatamente como aprovado. Não acessa banco e decide só com conjuntos: nenhum
/// critério implícito (ordem de cadastro, Id, nome, ordem de consulta, data de criação) entra na decisão. A ordem das
/// listas na saída é por Id apenas para a saída ser estável; nunca decide nada.
///
/// 0 universo → 1 candidatos pela regra → 2 Retirar → 3 Fixar (duas fixações no exclusivo = conflito de fixação; fixação
/// em território inativo em D ou com Retirar do mesmo território = fixação inválida) → 4 não exclusivo = soma → 5a
/// prioridade (1 = mais alta; sem = perde de todas) → 5b especificidade (o de baixo vence o de cima; irmãos não se
/// resolvem) → 6 conflito (mantém a atual se ela está entre os empatados; senão, sem território).
/// </summary>
public static class MotorAtribuicao
{
    public static DecisaoTerritorial Resolver(MapaParaResolver mapa, ClienteParaResolver c)
    {
        var estados = new Dictionary<Guid, EstadoCandidatoTerritorial>();
        var excecoes = c.Fixacoes.Select(f => f.ExcecaoId).ToList();

        // 0. Universo: nem exceção atravessa. Fixação de cliente fora do universo é fixação inválida (T3): nada é escolhido.
        if (!c.NoUniverso)
        {
            if (c.Fixacoes.Count == 0)
                return Decisao(c, ResultadoAtribuicao.ForaDoUniverso, PassoDecisaoTerritorial.Universo, [], estados, excecoes);
            foreach (var f in c.Fixacoes) estados[f.TerritorioId] = EstadoCandidatoTerritorial.FixacaoInvalida;
            return Decisao(c, ResultadoAtribuicao.FixacaoInvalida, PassoDecisaoTerritorial.Inconsistencia, [], estados, excecoes);
        }

        // 1. Candidatos: território ativo em D, com regra em D, cuja regra o cliente atende.
        var candidatos = c.Candidatos.Where(t => mapa.Ativo(t) && mapa.Regras.ContainsKey(t)).ToHashSet();

        // 2. Retirar: território retirado não concorre a nada.
        foreach (var t in candidatos.Where(c.Retirados.Contains).ToList())
        {
            candidatos.Remove(t);
            estados[t] = EstadoCandidatoTerritorial.RetiradoPorExcecao;
        }

        // 3. Fixar.
        var fixacoes = c.Fixacoes;
        var invalidas = fixacoes.Where(f => !mapa.Ativo(f.TerritorioId) || c.Retirados.Contains(f.TerritorioId)).ToList();
        if (mapa.Exclusivo && fixacoes.Count >= 2)
        {
            foreach (var f in fixacoes) estados[f.TerritorioId] = EstadoCandidatoTerritorial.FixacaoEmConflito;
            return Decisao(c, ResultadoAtribuicao.ConflitoDeFixacao, PassoDecisaoTerritorial.Inconsistencia, [], estados, excecoes);
        }
        if (invalidas.Count > 0)
        {
            foreach (var f in invalidas) estados[f.TerritorioId] = EstadoCandidatoTerritorial.FixacaoInvalida;
            return Decisao(c, ResultadoAtribuicao.FixacaoInvalida, PassoDecisaoTerritorial.Inconsistencia, [], estados, excecoes);
        }
        if (mapa.Exclusivo && fixacoes.Count == 1)
        {
            var f = fixacoes[0];
            foreach (var t in candidatos.Where(t => t != f.TerritorioId)) estados[t] = EstadoCandidatoTerritorial.PerdeuParaExcecao;
            estados[f.TerritorioId] = EstadoCandidatoTerritorial.Vencedor;
            return Decisao(c, ResultadoAtribuicao.Atribuido, PassoDecisaoTerritorial.Fixacao,
                [new TerritorioDecidido(f.TerritorioId, OrigemAtribuicaoTerritorio.Excecao, null, f.ExcecaoId)], estados, excecoes);
        }

        // 4. Mapa não exclusivo: a soma (a fixação vale mais que a regra no mesmo território).
        if (!mapa.Exclusivo)
        {
            var porTerritorio = new Dictionary<Guid, TerritorioDecidido>();
            foreach (var t in candidatos) porTerritorio[t] = PelaRegra(mapa, t);
            foreach (var f in fixacoes) porTerritorio[f.TerritorioId] = new TerritorioDecidido(f.TerritorioId, OrigemAtribuicaoTerritorio.Excecao, null, f.ExcecaoId);
            foreach (var t in porTerritorio.Keys) estados[t] = EstadoCandidatoTerritorial.Vencedor;
            var lista = porTerritorio.Values.OrderBy(t => t.TerritorioId).ToList();
            return Decisao(c, lista.Count == 0 ? ResultadoAtribuicao.SemTerritorio : ResultadoAtribuicao.Atribuido,
                lista.Count == 0 ? PassoDecisaoTerritorial.Nenhum : PassoDecisaoTerritorial.NaoExclusivo, lista, estados, excecoes);
        }

        // Exclusivo, sem fixação.
        if (candidatos.Count == 0)
            return Decisao(c, ResultadoAtribuicao.SemTerritorio, PassoDecisaoTerritorial.Nenhum, [], estados, excecoes);
        if (candidatos.Count == 1)
            return Vencedor(c, mapa, candidatos.Single(), PassoDecisaoTerritorial.UnicoCandidato, estados, excecoes);

        // 5a. Prioridade: fica o conjunto dos de menor número (sem prioridade = perde de qualquer número).
        var melhor = candidatos.Min(t => Prioridade(mapa, t));
        foreach (var t in candidatos.Where(t => Prioridade(mapa, t) != melhor).ToList())
        {
            candidatos.Remove(t);
            estados[t] = EstadoCandidatoTerritorial.PerdeuPorPrioridade;
        }
        if (candidatos.Count == 1)
            return Vencedor(c, mapa, candidatos.Single(), PassoDecisaoTerritorial.Prioridade, estados, excecoes);

        // 5b. Especificidade: sai todo território que tem outro do conjunto abaixo dele na árvore de D.
        var maisEspecificos = candidatos.Where(t => !candidatos.Any(u => u != t && mapa.EstaAcima(t, u))).ToHashSet();
        foreach (var t in candidatos.Where(t => !maisEspecificos.Contains(t)))
            estados[t] = EstadoCandidatoTerritorial.PerdeuPorEspecificidade;
        if (maisEspecificos.Count == 1)
            return Vencedor(c, mapa, maisEspecificos.Single(), PassoDecisaoTerritorial.Especificidade, estados, excecoes);

        // 6. Conflito: nunca escolhe entre os empatados.
        foreach (var t in maisEspecificos) estados[t] = EstadoCandidatoTerritorial.Empatado;
        var atualEmpatada = c.AtribuicoesAtuais.Where(maisEspecificos.Contains).ToList();
        if (atualEmpatada.Count == 1)
            return Decisao(c, ResultadoAtribuicao.PermaneceEmConflito, PassoDecisaoTerritorial.Conflito,
                [PelaRegra(mapa, atualEmpatada[0])], estados, excecoes);
        return Decisao(c, ResultadoAtribuicao.Conflito, PassoDecisaoTerritorial.Conflito, [], estados, excecoes);
    }

    private static int Prioridade(MapaParaResolver mapa, Guid territorio) => mapa.Regras[territorio].Prioridade ?? int.MaxValue;

    private static TerritorioDecidido PelaRegra(MapaParaResolver mapa, Guid territorio) =>
        new(territorio, OrigemAtribuicaoTerritorio.Regra, mapa.Regras.TryGetValue(territorio, out var r) ? r.RegraId : null, null);

    private static DecisaoTerritorial Vencedor(ClienteParaResolver c, MapaParaResolver mapa, Guid territorio, PassoDecisaoTerritorial passo,
                                               Dictionary<Guid, EstadoCandidatoTerritorial> estados, List<Guid> excecoes)
    {
        estados[territorio] = EstadoCandidatoTerritorial.Vencedor;
        return Decisao(c, ResultadoAtribuicao.Atribuido, passo, [PelaRegra(mapa, territorio)], estados, excecoes);
    }

    private static DecisaoTerritorial Decisao(ClienteParaResolver c, ResultadoAtribuicao resultado, PassoDecisaoTerritorial passo,
                                              IReadOnlyList<TerritorioDecidido> territorios, Dictionary<Guid, EstadoCandidatoTerritorial> estados,
                                              List<Guid> excecoes) =>
        new(c.PessoaId, resultado, passo, territorios, estados, excecoes.Order().ToList());

    /// <summary>
    /// O efeito da decisão sobre o cliente, comparando com as atribuições atuais (territórios e a origem de cada um).
    /// Mesmo território com origem diferente (outra versão da regra, regra ↔ exceção) = origem atualizada: reabre citando a nova.
    /// </summary>
    public static EfeitoNoCliente Efeito(DecisaoTerritorial d, IReadOnlyDictionary<Guid, TerritorioDecidido> atuais)
    {
        if (d.Inconsistente) return EfeitoNoCliente.Bloqueado;
        var novos = d.Territorios.ToDictionary(t => t.TerritorioId);
        if (atuais.Count == 0) return novos.Count == 0 ? EfeitoNoCliente.Permanece : EfeitoNoCliente.Entra;
        if (novos.Count == 0) return EfeitoNoCliente.Sai;
        if (!novos.Keys.ToHashSet().SetEquals(atuais.Keys)) return EfeitoNoCliente.Muda;
        return novos.Values.All(n => atuais[n.TerritorioId] == n) ? EfeitoNoCliente.Permanece : EfeitoNoCliente.OrigemAtualizada;
    }
}
