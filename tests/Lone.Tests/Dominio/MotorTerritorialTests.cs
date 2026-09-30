using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;

namespace Lone.Tests.Dominio;

/// <summary>
/// Motor de atribuição (T3, Fase 2b-1b), sem banco: universo, candidatos, Retirar, Fixar (uma, duas, inválida), mapa não
/// exclusivo, prioridade (inclusive 10 × 10), especificidade, conflito mantendo e sem a atual, e determinismo (entrada
/// embaralhada 50 vezes = mesmo resultado).
/// </summary>
public class MotorTerritorialTests
{
    // Árvore: MG > Norte > Curvelo; MG > Sul; SP (raiz).
    private static readonly Guid Mg = Guid.NewGuid(), Norte = Guid.NewGuid(), Curvelo = Guid.NewGuid(), Sul = Guid.NewGuid(), Sp = Guid.NewGuid();
    private static readonly Guid Cliente = Guid.NewGuid();

    private static MapaParaResolver Mapa(bool exclusivo = true, Dictionary<Guid, int?>? prioridades = null, IEnumerable<Guid>? semRegra = null)
    {
        var pais = new Dictionary<Guid, Guid?> { [Mg] = null, [Norte] = Mg, [Curvelo] = Norte, [Sul] = Mg, [Sp] = null };
        var regras = pais.Keys.Except(semRegra ?? []).ToDictionary(t => t, t => new RegraParaResolver(t, prioridades?.GetValueOrDefault(t)));
        return new MapaParaResolver(exclusivo, pais, regras);
    }

    private static ClienteParaResolver C(IEnumerable<Guid>? candidatos = null, IEnumerable<Guid>? retirados = null,
                                         IEnumerable<FixacaoVigente>? fixacoes = null, IEnumerable<Guid>? atuais = null, bool noUniverso = true) =>
        new(Cliente, noUniverso, (candidatos ?? []).ToHashSet(), (retirados ?? []).ToHashSet(), (fixacoes ?? []).ToList(), (atuais ?? []).ToHashSet());

    private static FixacaoVigente Fixar(Guid territorio) => new(Guid.NewGuid(), territorio);

    [Fact]
    public void Zero_um_e_dois_candidatos_sem_desempate_possivel()
    {
        Assert.Equal(ResultadoAtribuicao.SemTerritorio, MotorAtribuicao.Resolver(Mapa(), C()).Resultado);

        var um = MotorAtribuicao.Resolver(Mapa(), C([Sp]));
        Assert.Equal(ResultadoAtribuicao.Atribuido, um.Resultado);
        Assert.Equal(PassoDecisaoTerritorial.UnicoCandidato, um.Passo);
        Assert.Equal(Sp, Assert.Single(um.Territorios).TerritorioId);

        var irmaos = MotorAtribuicao.Resolver(Mapa(), C([Norte, Sul]));
        Assert.Equal(ResultadoAtribuicao.Conflito, irmaos.Resultado);
        Assert.Empty(irmaos.Territorios);
        Assert.All(new[] { Norte, Sul }, t => Assert.Equal(EstadoCandidatoTerritorial.Empatado, irmaos.Estados[t]));
    }

    [Fact]
    public void Especificidade_o_de_baixo_vence_o_de_cima()
    {
        var d = MotorAtribuicao.Resolver(Mapa(), C([Mg, Norte, Curvelo]));
        Assert.Equal(Curvelo, Assert.Single(d.Territorios).TerritorioId);
        Assert.Equal(PassoDecisaoTerritorial.Especificidade, d.Passo);
        Assert.Equal(EstadoCandidatoTerritorial.PerdeuPorEspecificidade, d.Estados[Mg]);
        Assert.Equal(EstadoCandidatoTerritorial.PerdeuPorEspecificidade, d.Estados[Norte]);
    }

    [Fact]
    public void Prioridade_vem_antes_da_especificidade_e_sem_prioridade_perde_de_qualquer_numero()
    {
        var d = MotorAtribuicao.Resolver(Mapa(prioridades: new() { [Mg] = 2 }), C([Mg, Curvelo]));
        Assert.Equal(Mg, Assert.Single(d.Territorios).TerritorioId);
        Assert.Equal(PassoDecisaoTerritorial.Prioridade, d.Passo);
        Assert.Equal(EstadoCandidatoTerritorial.PerdeuPorPrioridade, d.Estados[Curvelo]);
    }

    [Fact]
    public void Prioridade_10_contra_10_ancestral_perde_e_irmaos_empatam()
    {
        var dez = new Dictionary<Guid, int?> { [Mg] = 10, [Curvelo] = 10, [Sul] = 10 };
        var ancestral = MotorAtribuicao.Resolver(Mapa(prioridades: dez), C([Mg, Curvelo]));
        Assert.Equal(Curvelo, Assert.Single(ancestral.Territorios).TerritorioId); // o mais específico vence

        var irmaos = MotorAtribuicao.Resolver(Mapa(prioridades: dez), C([Curvelo, Sul]));
        Assert.Equal(ResultadoAtribuicao.Conflito, irmaos.Resultado); // nunca escolhe
        Assert.Empty(irmaos.Territorios);
    }

    [Fact]
    public void Conflito_mantem_a_atual_se_ela_esta_entre_os_empatados_e_senao_fica_sem()
    {
        var mantem = MotorAtribuicao.Resolver(Mapa(), C([Norte, Sul], atuais: [Sul]));
        Assert.Equal(ResultadoAtribuicao.PermaneceEmConflito, mantem.Resultado);
        Assert.Equal(Sul, Assert.Single(mantem.Territorios).TerritorioId);

        var sem = MotorAtribuicao.Resolver(Mapa(), C([Norte, Sul], atuais: [Sp]));
        Assert.Equal(ResultadoAtribuicao.Conflito, sem.Resultado);
        Assert.Empty(sem.Territorios);
    }

    [Fact]
    public void Retirar_faz_cair_no_proximo_e_Fixar_vence_a_regra()
    {
        var retirado = MotorAtribuicao.Resolver(Mapa(), C([Norte, Curvelo], retirados: [Curvelo]));
        Assert.Equal(Norte, Assert.Single(retirado.Territorios).TerritorioId);
        Assert.Equal(EstadoCandidatoTerritorial.RetiradoPorExcecao, retirado.Estados[Curvelo]);

        var fixacao = Fixar(Sp);
        var fixado = MotorAtribuicao.Resolver(Mapa(), C([Curvelo], fixacoes: [fixacao]));
        var vencedor = Assert.Single(fixado.Territorios);
        Assert.Equal(Sp, vencedor.TerritorioId);
        Assert.Equal(OrigemAtribuicaoTerritorio.Excecao, vencedor.Origem);
        Assert.Equal(fixacao.ExcecaoId, vencedor.ExcecaoId);
        Assert.Equal(EstadoCandidatoTerritorial.PerdeuParaExcecao, fixado.Estados[Curvelo]);
    }

    [Fact]
    public void Duas_fixacoes_no_exclusivo_sao_conflito_e_a_atual_nao_muda()
    {
        var d = MotorAtribuicao.Resolver(Mapa(), C([Curvelo], fixacoes: [Fixar(Sp), Fixar(Sul)], atuais: [Curvelo]));
        Assert.Equal(ResultadoAtribuicao.ConflitoDeFixacao, d.Resultado);
        Assert.True(d.Inconsistente);
        Assert.Equal(new HashSet<Guid> { Curvelo }, d.TerritoriosFinais(new HashSet<Guid> { Curvelo }));
        Assert.Equal(EfeitoNoCliente.Bloqueado, MotorAtribuicao.Efeito(d, new Dictionary<Guid, TerritorioDecidido>()));
    }

    [Fact]
    public void Fixacao_invalida_territorio_inativo_fixar_e_retirar_juntos_ou_cliente_fora_do_universo()
    {
        var inativo = Guid.NewGuid();
        Assert.Equal(ResultadoAtribuicao.FixacaoInvalida, MotorAtribuicao.Resolver(Mapa(), C(fixacoes: [Fixar(inativo)])).Resultado);
        Assert.Equal(ResultadoAtribuicao.FixacaoInvalida, MotorAtribuicao.Resolver(Mapa(), C(fixacoes: [Fixar(Sp)], retirados: [Sp])).Resultado);
        Assert.Equal(ResultadoAtribuicao.FixacaoInvalida, MotorAtribuicao.Resolver(Mapa(), C(fixacoes: [Fixar(Sp)], noUniverso: false)).Resultado);
        Assert.Equal(ResultadoAtribuicao.ForaDoUniverso, MotorAtribuicao.Resolver(Mapa(), C([Sp], noUniverso: false)).Resultado);
    }

    [Fact]
    public void Mapa_nao_exclusivo_soma_regras_e_fixacoes_e_aceita_duas_fixacoes()
    {
        var d = MotorAtribuicao.Resolver(Mapa(exclusivo: false), C([Norte, Sul], fixacoes: [Fixar(Sp), Fixar(Curvelo)]));
        Assert.Equal(ResultadoAtribuicao.Atribuido, d.Resultado);
        Assert.Equal(new[] { Norte, Sul, Sp, Curvelo }.ToHashSet(), d.Territorios.Select(t => t.TerritorioId).ToHashSet());
    }

    [Fact]
    public void Territorio_sem_regra_nao_e_candidato_so_recebe_por_fixacao()
    {
        var mapa = Mapa(semRegra: [Mg]);
        Assert.Equal(ResultadoAtribuicao.SemTerritorio, MotorAtribuicao.Resolver(mapa, C([Mg])).Resultado);
        Assert.Equal(Mg, Assert.Single(MotorAtribuicao.Resolver(mapa, C(fixacoes: [Fixar(Mg)])).Territorios).TerritorioId);
    }

    [Fact]
    public void Deterministico_com_entrada_embaralhada_50_vezes()
    {
        var aleatorio = new Random(7);
        var candidatos = new[] { Mg, Norte, Curvelo, Sul, Sp };
        var prioridades = new Dictionary<Guid, int?> { [Curvelo] = 5, [Sul] = 5 };
        var esperado = MotorAtribuicao.Resolver(Mapa(prioridades: prioridades), C(candidatos));
        for (var i = 0; i < 50; i++)
        {
            var embaralhados = candidatos.OrderBy(_ => aleatorio.Next()).ToArray();
            var d = MotorAtribuicao.Resolver(Mapa(prioridades: prioridades), C(embaralhados));
            Assert.Equal(esperado.Resultado, d.Resultado);
            Assert.Equal(esperado.Territorios, d.Territorios);
            Assert.Equal(esperado.Estados.OrderBy(e => e.Key), d.Estados.OrderBy(e => e.Key));
        }
    }

    [Fact]
    public void Efeito_distingue_entra_sai_muda_permanece_e_origem_atualizada()
    {
        var regra1 = new TerritorioDecidido(Sp, OrigemAtribuicaoTerritorio.Regra, Guid.NewGuid(), null);
        var d = new DecisaoTerritorial(Cliente, ResultadoAtribuicao.Atribuido, PassoDecisaoTerritorial.UnicoCandidato, [regra1],
            new Dictionary<Guid, EstadoCandidatoTerritorial>(), []);
        var vazio = new Dictionary<Guid, TerritorioDecidido>();
        Assert.Equal(EfeitoNoCliente.Entra, MotorAtribuicao.Efeito(d, vazio));
        Assert.Equal(EfeitoNoCliente.Permanece, MotorAtribuicao.Efeito(d, new Dictionary<Guid, TerritorioDecidido> { [Sp] = regra1 }));
        Assert.Equal(EfeitoNoCliente.OrigemAtualizada, MotorAtribuicao.Efeito(d,
            new Dictionary<Guid, TerritorioDecidido> { [Sp] = regra1 with { RegraId = Guid.NewGuid() } }));
        Assert.Equal(EfeitoNoCliente.Muda, MotorAtribuicao.Efeito(d,
            new Dictionary<Guid, TerritorioDecidido> { [Sul] = regra1 with { TerritorioId = Sul } }));
        var semNada = d with { Resultado = ResultadoAtribuicao.SemTerritorio, Territorios = [] };
        Assert.Equal(EfeitoNoCliente.Sai, MotorAtribuicao.Efeito(semNada, new Dictionary<Guid, TerritorioDecidido> { [Sp] = regra1 }));
    }
}
