using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Territorios;

/// <summary>
/// O estado oficial de um mapa lido para a data de efeito D (vigentes em D), mais a última atribuição de cada cliente. O
/// serviço lê em lote; o domínio monta o cenário com ou sem as mudanças da operação e resolve.
/// </summary>
public sealed record EstadoTerritorialEmD(
    MapaTerritorial Mapa,
    DateOnly Efeito,
    IReadOnlyDictionary<Guid, Territorio> Territorios,
    IReadOnlyList<RegraTerritorio> RegrasVigentes,
    IReadOnlyList<ExcecaoTerritorio> ExcecoesVigentes,
    IReadOnlyList<AtribuicaoTerritorio> AtribuicoesVigentes);

/// <summary>A regra de um território no cenário: a vigente, ou a versão planejada (id = id da mudança, estável entre simulação e aplicação).</summary>
public sealed record RegraNoCenario(Guid TerritorioId, Guid RegraId, string Grupos, int? Prioridade, bool Planejada);

/// <summary>
/// O cenário em D (seção I): a árvore em D já com as mudanças de estrutura, as regras finais (vigentes com as planejadas por
/// cima) e as exceções finais (vigentes com as planejadas). Com a lista de mudanças vazia, é o estado sem a operação — a
/// diferença entre os dois separa o efeito da operação das divergências que já existiam (DN-14).
/// </summary>
public sealed class CenarioTerritorial
{
    private CenarioTerritorial(MapaParaResolver mapa, IReadOnlyDictionary<Guid, RegraNoCenario> regras,
                               IReadOnlyList<(Guid PessoaId, Guid TerritorioId, TipoExcecaoTerritorio Tipo, Guid ExcecaoId)> excecoes)
    {
        Mapa = mapa;
        Regras = regras;
        Excecoes = excecoes;
    }

    public MapaParaResolver Mapa { get; }

    /// <summary>Território ativo em D → a regra dele no cenário.</summary>
    public IReadOnlyDictionary<Guid, RegraNoCenario> Regras { get; }

    public IReadOnlyList<(Guid PessoaId, Guid TerritorioId, TipoExcecaoTerritorio Tipo, Guid ExcecaoId)> Excecoes { get; }

    public static CenarioTerritorial Montar(EstadoTerritorialEmD e, IReadOnlyList<OperacaoTerritorialMudanca> mudancas)
    {
        // Árvore em D (o estado de hoje; a estrutura não tem efeito futuro, DN-01) + mudanças de estrutura em ordem.
        var pai = new Dictionary<Guid, Guid?>();
        foreach (var t in e.Territorios.Values.Where(t => t.Ativo)) pai[t.Id] = t.PaiId;
        foreach (var m in mudancas.OrderBy(m => m.Ordem).Where(m => RegrasOperacaoTerritorial.Estrutural(m.Tipo)))
        {
            if (m.TerritorioId is not { } id || !e.Territorios.TryGetValue(id, out var t)) continue;
            var dados = DadosMudancaTerritorial.DeJson(m.Depois);
            switch (m.Tipo)
            {
                case TipoMudancaTerritorial.MoverTerritorio: pai[id] = dados.NovoPaiId; break;
                case TipoMudancaTerritorial.EncerrarTerritorio: pai.Remove(id); break;
                case TipoMudancaTerritorial.ReativarTerritorio: pai[id] = t.PaiId; break;
            }
        }

        // Regras: vigentes em D, com as planejadas por cima; só dos territórios ativos no cenário.
        var regras = e.RegrasVigentes.Where(r => r.VigenteEm(e.Efeito))
            .ToDictionary(r => r.TerritorioId, r => new RegraNoCenario(r.TerritorioId, r.Id, r.Grupos, r.Prioridade, false));
        foreach (var m in mudancas.OrderBy(m => m.Ordem))
        {
            if (m.TerritorioId is not { } id) continue;
            if (m.Tipo == TipoMudancaTerritorial.NovaVersaoRegra)
            {
                var dados = DadosMudancaTerritorial.DeJson(m.Depois);
                regras[id] = new RegraNoCenario(id, m.Id, dados.Grupos ?? string.Empty, dados.Prioridade, true);
            }
            else if (m.Tipo is TipoMudancaTerritorial.EncerrarRegra or TipoMudancaTerritorial.EncerrarTerritorio)
                regras.Remove(id);
        }
        foreach (var id in regras.Keys.Where(id => !pai.ContainsKey(id)).ToList()) regras.Remove(id);

        // Exceções: vigentes em D, menos as encerradas pela operação ou do território encerrado, mais as planejadas.
        var encerradas = mudancas.Where(m => m.Tipo == TipoMudancaTerritorial.EncerrarExcecao && m.ExcecaoBaseId is not null)
            .Select(m => m.ExcecaoBaseId!.Value).ToHashSet();
        var territoriosEncerrados = mudancas.Where(m => m.Tipo == TipoMudancaTerritorial.EncerrarTerritorio && m.TerritorioId is not null)
            .Select(m => m.TerritorioId!.Value).ToHashSet();
        var excecoes = e.ExcecoesVigentes
            .Where(x => x.VigenteEm(e.Efeito) && !encerradas.Contains(x.Id) && !territoriosEncerrados.Contains(x.TerritorioId))
            .Select(x => (x.PessoaId, x.TerritorioId, x.Tipo, x.Id)).ToList();
        foreach (var m in mudancas.Where(m => m.Tipo is TipoMudancaTerritorial.Fixar or TipoMudancaTerritorial.Retirar))
            if (m.PessoaId is { } p && m.TerritorioId is { } t)
                excecoes.Add((p, t, m.Tipo == TipoMudancaTerritorial.Fixar ? TipoExcecaoTerritorio.Fixar : TipoExcecaoTerritorio.Retirar, m.Id));

        var mapa = new MapaParaResolver(e.Mapa.Exclusivo, pai,
            regras.ToDictionary(kv => kv.Key, kv => new RegraParaResolver(kv.Value.RegraId, kv.Value.Prioridade)));
        return new CenarioTerritorial(mapa, regras, excecoes);
    }

    /// <summary>
    /// Resolve os clientes: o universo em D mais todo cliente que tem atribuição ou exceção (quem saiu do universo precisa
    /// sair do território). <paramref name="candidatosPorTerritorio"/>: quem atende à regra de cada território do cenário.
    /// </summary>
    public IReadOnlyList<DecisaoTerritorial> Resolver(IReadOnlySet<Guid> universo,
                                                      IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> candidatosPorTerritorio,
                                                      IReadOnlyList<AtribuicaoTerritorio> atribuicoesVigentes)
    {
        var candidatosPorCliente = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var (territorio, pessoas) in candidatosPorTerritorio)
        {
            if (!Regras.ContainsKey(territorio)) continue;
            foreach (var p in pessoas)
            {
                if (!candidatosPorCliente.TryGetValue(p, out var set)) candidatosPorCliente[p] = set = [];
                set.Add(territorio);
            }
        }
        var retirados = Excecoes.Where(x => x.Tipo == TipoExcecaoTerritorio.Retirar).ToLookup(x => x.PessoaId, x => x.TerritorioId);
        var fixacoes = Excecoes.Where(x => x.Tipo == TipoExcecaoTerritorio.Fixar).ToLookup(x => x.PessoaId, x => new FixacaoVigente(x.ExcecaoId, x.TerritorioId));
        var atuais = atribuicoesVigentes.ToLookup(a => a.PessoaId, a => a.TerritorioId);

        var clientes = new HashSet<Guid>(universo);
        clientes.UnionWith(atribuicoesVigentes.Select(a => a.PessoaId));
        clientes.UnionWith(Excecoes.Select(x => x.PessoaId));

        var vazio = new HashSet<Guid>();
        return clientes.Order().Select(p => MotorAtribuicao.Resolver(Mapa, new ClienteParaResolver(
            p,
            universo.Contains(p),
            candidatosPorCliente.TryGetValue(p, out var c) ? c : vazio,
            retirados[p].ToHashSet(),
            fixacoes[p].OrderBy(f => f.ExcecaoId).ToList(),
            atuais[p].ToHashSet()))).ToList();
    }

    /// <summary>As atribuições vigentes de um cliente como o motor as descreve (para comparar origem: regra/versão ou exceção).</summary>
    public static IReadOnlyDictionary<Guid, TerritorioDecidido> Atuais(IEnumerable<AtribuicaoTerritorio> doCliente) =>
        doCliente.ToDictionary(a => a.TerritorioId, a => new TerritorioDecidido(a.TerritorioId, a.Origem,
            a.Origem == OrigemAtribuicaoTerritorio.Regra ? a.RegraId : null, a.Origem == OrigemAtribuicaoTerritorio.Excecao ? a.ExcecaoId : null));
}

/// <summary>O efeito de um cliente numa simulação/aplicação, com a origem (esta operação ou divergência que já existia).</summary>
public sealed record EfeitoCliente(DecisaoTerritorial Decisao, EfeitoNoCliente Efeito, OrigemEfeitoSimulado Origem,
                                   IReadOnlyDictionary<Guid, TerritorioDecidido> Atuais);

/// <summary>O resultado da simulação (ou da re-simulação dentro da aplicação), já comparado com o estado oficial.</summary>
public sealed class ResultadoSimulacaoTerritorial
{
    public required IReadOnlyList<EfeitoCliente> Afetados { get; init; }
    public required byte[] Assinatura { get; init; }

    public int Entram => Afetados.Count(a => a.Efeito == EfeitoNoCliente.Entra);
    public int Saem => Afetados.Count(a => a.Efeito == EfeitoNoCliente.Sai);
    public int Mudam => Afetados.Count(a => a.Efeito == EfeitoNoCliente.Muda);
    public int OrigemAtualizada => Afetados.Count(a => a.Efeito == EfeitoNoCliente.OrigemAtualizada);
    public int Inconsistencias => Afetados.Count(a => a.Efeito == EfeitoNoCliente.Bloqueado);
    public int EmConflito => Afetados.Count(a => a.Decisao.Resultado is ResultadoAtribuicao.Conflito or ResultadoAtribuicao.PermaneceEmConflito);
    public int DaOperacao => Afetados.Count(a => a.Origem == OrigemEfeitoSimulado.EstaOperacao);
    public int Divergencias => Afetados.Count(a => a.Origem == OrigemEfeitoSimulado.DivergenciaExistente);
    public bool Bloqueada => Inconsistencias > 0;
}

/// <summary>
/// A simulação (seção I) em forma pura: compara o resultado com e sem a operação com as atribuições vigentes, separa o
/// efeito da operação das divergências (DN-14) e calcula a assinatura que a aplicação vai conferir.
/// </summary>
public static class SimulacaoTerritorial
{
    /// <param name="comOperacao">Decisões do cenário com as mudanças.</param>
    /// <param name="semOperacao">Decisões do cenário sem as mudanças (estado oficial reavaliado hoje).</param>
    public static ResultadoSimulacaoTerritorial Comparar(IReadOnlyList<DecisaoTerritorial> comOperacao, IReadOnlyList<DecisaoTerritorial> semOperacao,
                                                         IReadOnlyList<AtribuicaoTerritorio> atribuicoesVigentes, DateOnly efeito, byte[] versaoMotor)
    {
        var atuaisPorCliente = atribuicoesVigentes.ToLookup(a => a.PessoaId);
        var sem = semOperacao.ToDictionary(d => d.PessoaId);
        var afetados = new List<EfeitoCliente>();
        foreach (var d in comOperacao)
        {
            var atuais = CenarioTerritorial.Atuais(atuaisPorCliente[d.PessoaId]);
            var efeitoCliente = MotorAtribuicao.Efeito(d, atuais);
            var conflito = d.Resultado is ResultadoAtribuicao.Conflito or ResultadoAtribuicao.PermaneceEmConflito;
            if (efeitoCliente == EfeitoNoCliente.Permanece && !conflito) continue;
            // Divergência que já existia: sem a operação, o motor já daria o mesmo resultado (a operação não é a causa).
            var jaExistia = sem.TryGetValue(d.PessoaId, out var s) && MesmoResultado(s, d);
            afetados.Add(new EfeitoCliente(d, efeitoCliente, jaExistia ? OrigemEfeitoSimulado.DivergenciaExistente : OrigemEfeitoSimulado.EstaOperacao, atuais));
        }
        return new ResultadoSimulacaoTerritorial { Afetados = afetados, Assinatura = Assinar(afetados, efeito, versaoMotor) };
    }

    private static bool MesmoResultado(DecisaoTerritorial a, DecisaoTerritorial b) =>
        a.Resultado == b.Resultado && a.Territorios.Count == b.Territorios.Count &&
        a.Territorios.Select(t => (t.TerritorioId, t.Origem)).ToHashSet().SetEquals(b.Territorios.Select(t => (t.TerritorioId, t.Origem)));

    /// <summary>SHA-256 de (efeito; versão do motor; por cliente afetado em ordem de id: efeito, resultado, território, origem, regra, exceção).</summary>
    public static byte[] Assinar(IEnumerable<EfeitoCliente> afetados, DateOnly efeito, byte[] versaoMotor)
    {
        var texto = new StringBuilder();
        texto.Append(efeito.ToString("yyyy-MM-dd")).Append('|').Append(Convert.ToHexString(versaoMotor)).Append('\n');
        foreach (var a in afetados.OrderBy(a => a.Decisao.PessoaId))
        {
            texto.Append(a.Decisao.PessoaId).Append('|').Append((int)a.Efeito).Append('|').Append((int)a.Decisao.Resultado);
            foreach (var t in a.Decisao.Territorios.OrderBy(t => t.TerritorioId))
                texto.Append('|').Append(t.TerritorioId).Append(':').Append((int)t.Origem).Append(':').Append(t.RegraId).Append(':').Append(t.ExcecaoId);
            texto.Append('\n');
        }
        return SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString()));
    }

    /// <summary>A explicação congelada de um cliente (JSON): resultado, passo, estado de cada território envolvido, exceções e atributos lidos.</summary>
    public static string Explicacao(EfeitoCliente a, IReadOnlyDictionary<string, string?>? atributos = null)
    {
        var d = a.Decisao;
        var objeto = new
        {
            resultado = d.Resultado.ToString(),
            passo = d.Passo.ToString(),
            efeito = a.Efeito.ToString(),
            origem = a.Origem.ToString(),
            atuais = a.Atuais.Values.Select(t => new { territorio = t.TerritorioId, origem = t.Origem.ToString(), regra = t.RegraId, excecao = t.ExcecaoId }),
            territorios = d.Territorios.Select(t => new { territorio = t.TerritorioId, origem = t.Origem.ToString(), regra = t.RegraId, excecao = t.ExcecaoId }),
            estados = d.Estados.OrderBy(e => e.Key).Select(e => new { territorio = e.Key, estado = e.Value.ToString() }),
            excecoes = d.ExcecoesEnvolvidas,
            atributos
        };
        return JsonSerializer.Serialize(objeto);
    }
}
