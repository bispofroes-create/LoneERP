using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;

namespace Lone.Tests.Dominio;

/// <summary>
/// Regras da operação territorial TE- (Fase 2b-1b), sem banco: data de efeito (T8 com o parâmetro próprio, DN-01, RT-1),
/// cancelamento (DN-06/RT-2), desfazer (T15/DN-05), mudanças isoladas e em sequência (A↔B, RT-6), cenário com e sem a
/// operação, separação efeito × divergência (DN-14), assinatura e o plano de gravação (fecha → anula → abre).
/// </summary>
public class OperacaoTerritorialTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 29);
    private static readonly Guid MapaId = Guid.NewGuid();
    private static readonly Guid Joao = Guid.NewGuid(), Maria = Guid.NewGuid();

    private static MapaTerritorial Mapa(bool exclusivo = true) => new()
    {
        Id = MapaId, Codigo = "GEO", Nome = "Geografia", Exclusivo = exclusivo, FinalidadeEnderecoReferenciaId = Guid.NewGuid()
    };

    private static Territorio T(string nome, Territorio? pai = null, DateOnly? inicio = null)
    {
        var t = new Territorio { Id = Guid.NewGuid(), MapaId = MapaId, Codigo = nome.ToUpperInvariant(), Nome = nome, PaiId = pai?.Id };
        t.Posicoes.Add(new TerritorioPosicao { Id = Guid.NewGuid(), MapaId = MapaId, TerritorioId = t.Id, PaiId = t.PaiId, InicioEm = inicio ?? new(2026, 1, 1) });
        return t;
    }

    private static OperacaoTerritorial Op(DateOnly efeito, params OperacaoTerritorialMudanca[] mudancas)
    {
        var op = new OperacaoTerritorial { Id = Guid.NewGuid(), Ano = 2026, Sequencia = 1, MapaId = MapaId, EfeitoEm = efeito, Motivo = "Teste" };
        var ordem = 1;
        foreach (var m in mudancas) { m.OperacaoId = op.Id; m.MapaId = MapaId; m.Ordem = ordem++; op.Mudancas.Add(m); }
        return op;
    }

    private static OperacaoTerritorialMudanca M(TipoMudancaTerritorial tipo, Territorio? t = null, Guid? pessoa = null,
                                                DadosMudancaTerritorial? dados = null, Guid? posicaoBase = null) =>
        new()
        {
            Id = Guid.NewGuid(), Tipo = tipo, TerritorioId = t?.Id, PessoaId = pessoa, Depois = (dados ?? new()).ParaJson(),
            PosicaoBaseId = posicaoBase ?? (t is null ? null : RegrasArvoreTerritorial.PosicaoAberta(t)?.Id)
        };

    private static RegrasOperacaoTerritorial.ContextoMudanca Contexto(MapaTerritorial mapa, IEnumerable<Territorio> territorios, DateOnly efeito,
                                                                       IEnumerable<RegraTerritorio>? regras = null) =>
        new(mapa, territorios.ToDictionary(t => t.Id), new HashSet<Guid>(), (regras ?? []).ToDictionary(r => r.TerritorioId),
            new Dictionary<Guid, ExcecaoTerritorio>(), _ => true, efeito);

    // ------------------------------------------------------------------ Data de efeito

    [Theory]
    [InlineData(-30, false, true)]   // 30 dias atrás, com o padrão 30: aceita
    [InlineData(-31, false, false)]  // 31 dias atrás: recusada
    [InlineData(10, false, true)]    // futuro só com regras e exceções: aceita
    [InlineData(10, true, false)]    // futuro com mudança de estrutura: recusada (DN-01)
    [InlineData(0, true, true)]      // estrutura hoje: aceita
    public void Efeito_respeita_os_dias_retroativos_e_estrutura_so_ate_hoje(int dias, bool estrutural, bool aceita)
    {
        var erros = RegrasOperacaoTerritorial.ValidarEfeito(Hoje.AddDays(dias), Hoje, ParametrosTerritoriais.PadraoDiasRetroativos, null, null, estrutural);
        Assert.Equal(aceita, erros.Count == 0);
    }

    [Fact]
    public void Parametro_proprio_muda_o_limite_sem_depender_das_coberturas()
    {
        Assert.Equal(30, new ParametrosTerritoriais().DiasRetroativosMaximo);
        Assert.NotEmpty(RegrasOperacaoTerritorial.ValidarEfeito(Hoje.AddDays(-31), Hoje, 30, null, null, false));
        Assert.Empty(RegrasOperacaoTerritorial.ValidarEfeito(Hoje.AddDays(-31), Hoje, 60, null, null, false));
    }

    [Fact]
    public void Operacao_futura_aplicada_bloqueia_as_anteriores_e_diz_como_desfazer()
    {
        var erros = RegrasOperacaoTerritorial.ValidarEfeito(Hoje, Hoje, 30, new DateOnly(2027, 1, 1), "TE-2026-0001", temMudancaEstrutural: false);
        var erro = Assert.Single(erros);
        Assert.Contains("existe a TE-2026-0001 com efeito futuro", erro);
        Assert.Contains("primeiro desfaça a TE-2026-0001", erro);
        Assert.Empty(RegrasOperacaoTerritorial.ValidarEfeito(new DateOnly(2027, 1, 1), Hoje, 30, new DateOnly(2027, 1, 1), "TE-2026-0001", false));
    }

    // ------------------------------------------------------------------ Cancelar e desfazer

    [Fact]
    public void Planejar_cancela_so_a_propria_em_rascunho_ou_simulada_e_aplicar_cancela_qualquer_aberta()
    {
        var eu = Guid.NewGuid();
        var minha = Op(Hoje);
        minha.CriadaPorId = eu;
        Assert.Empty(RegrasOperacaoTerritorial.ValidarCancelamento(minha, eu, podeAplicar: false, podePlanejar: true, "Desisti"));
        minha.Situacao = SituacaoOperacaoTerritorial.Simulada;
        Assert.Empty(RegrasOperacaoTerritorial.ValidarCancelamento(minha, eu, false, true, "Desisti"));
        Assert.NotEmpty(RegrasOperacaoTerritorial.ValidarCancelamento(minha, Guid.NewGuid(), false, true, "Não é minha"));
        Assert.Empty(RegrasOperacaoTerritorial.ValidarCancelamento(minha, Guid.NewGuid(), true, false, "Quem aplica pode"));
        Assert.NotEmpty(RegrasOperacaoTerritorial.ValidarCancelamento(minha, eu, false, true, "  "));
        minha.Situacao = SituacaoOperacaoTerritorial.Aplicada;
        Assert.Contains(RegrasOperacaoTerritorial.ValidarCancelamento(minha, eu, true, true, "x"), e => e.Contains("desfeita"));
    }

    [Fact]
    public void Desfazer_so_a_ultima_aplicada_antes_do_efeito_com_motivo()
    {
        var op = Op(Hoje.AddDays(5));
        op.Situacao = SituacaoOperacaoTerritorial.Aplicada;
        Assert.Empty(RegrasOperacaoTerritorial.ValidarDesfazer(op, Hoje, op.Id, op.Numero, "Erro no plano"));
        Assert.Contains(RegrasOperacaoTerritorial.ValidarDesfazer(op, Hoje, Guid.NewGuid(), "TE-2026-0009", "x"), e => e.Contains("TE-2026-0009"));
        Assert.NotEmpty(RegrasOperacaoTerritorial.ValidarDesfazer(op, Hoje.AddDays(5), op.Id, op.Numero, "x")); // o efeito chegou
        Assert.NotEmpty(RegrasOperacaoTerritorial.ValidarDesfazer(op, Hoje, op.Id, op.Numero, null));
        op.Situacao = SituacaoOperacaoTerritorial.Simulada;
        Assert.NotEmpty(RegrasOperacaoTerritorial.ValidarDesfazer(op, Hoje, op.Id, op.Numero, "x"));
    }

    [Fact]
    public void Indicadores_agendada_e_em_vigor_sao_calculados()
    {
        var op = Op(Hoje.AddDays(1));
        Assert.False(op.Agendada(Hoje));
        op.Situacao = SituacaoOperacaoTerritorial.Aplicada;
        Assert.True(op.Agendada(Hoje));
        Assert.False(op.EmVigor(Hoje));
        Assert.True(op.EmVigor(Hoje.AddDays(1)));
    }

    // ------------------------------------------------------------------ Mudanças

    [Fact]
    public void A_abaixo_de_B_e_B_abaixo_de_A_na_mesma_operacao_e_barrado()
    {
        var a = T("A");
        var b = T("B");
        var op = Op(Hoje,
            M(TipoMudancaTerritorial.MoverTerritorio, a, dados: new() { NovoPaiId = b.Id }),
            M(TipoMudancaTerritorial.MoverTerritorio, b, dados: new() { NovoPaiId = a.Id }));
        var erros = RegrasOperacaoTerritorial.ValidarTodas(op.Mudancas, Contexto(Mapa(), [a, b], Hoje));
        Assert.Contains(erros, e => e.StartsWith("Mudança 2:") && e.Contains("ciclo"));
    }

    [Fact]
    public void Encerrar_os_filhos_antes_do_pai_na_mesma_operacao_e_aceito()
    {
        var pai = T("Pai");
        var filho = T("Filho", pai);
        var ordemCerta = Op(Hoje, M(TipoMudancaTerritorial.EncerrarTerritorio, filho), M(TipoMudancaTerritorial.EncerrarTerritorio, pai));
        Assert.Empty(RegrasOperacaoTerritorial.ValidarTodas(ordemCerta.Mudancas, Contexto(Mapa(), [pai, filho], Hoje)));
        var ordemErrada = Op(Hoje, M(TipoMudancaTerritorial.EncerrarTerritorio, pai), M(TipoMudancaTerritorial.EncerrarTerritorio, filho));
        Assert.NotEmpty(RegrasOperacaoTerritorial.ValidarTodas(ordemErrada.Mudancas, Contexto(Mapa(), [pai, filho], Hoje)));
    }

    [Fact]
    public void Estrutura_retroativa_nao_fecha_posicao_antes_de_ela_comecar()
    {
        var norte = T("Norte", inicio: Hoje.AddDays(-5));
        var sp = T("SP");
        var op = Op(Hoje.AddDays(-10), M(TipoMudancaTerritorial.MoverTerritorio, norte, dados: new() { NovoPaiId = sp.Id }));
        Assert.Contains(RegrasOperacaoTerritorial.ValidarTodas(op.Mudancas, Contexto(Mapa(), [norte, sp], op.EfeitoEm)),
            e => e.Contains("depois do início da posição atual"));
    }

    [Fact]
    public void Base_velha_da_posicao_e_detectada()
    {
        var norte = T("Norte");
        var sp = T("SP");
        var mudanca = M(TipoMudancaTerritorial.MoverTerritorio, norte, dados: new() { NovoPaiId = sp.Id }, posicaoBase: Guid.NewGuid());
        Assert.Contains(RegrasOperacaoTerritorial.ValidarTodas(Op(Hoje, mudanca).Mudancas, Contexto(Mapa(), [norte, sp], Hoje)),
            e => e.Contains("não é mais a vigente"));
    }

    [Fact]
    public void Duas_fixacoes_do_mesmo_cliente_no_exclusivo_e_fixar_e_retirar_juntos_sao_barrados()
    {
        var a = T("A");
        var b = T("B");
        var motivo = new DadosMudancaTerritorial { Motivo = "Conta especial" };
        var duas = new[] { M(TipoMudancaTerritorial.Fixar, a, Joao, motivo), M(TipoMudancaTerritorial.Fixar, b, Joao, motivo) };
        Assert.NotEmpty(RegrasOperacaoTerritorial.ValidarConjunto(duas, exclusivo: true));
        Assert.Empty(RegrasOperacaoTerritorial.ValidarConjunto(duas, exclusivo: false));
        var fixarERetirar = new[] { M(TipoMudancaTerritorial.Fixar, a, Joao, motivo), M(TipoMudancaTerritorial.Retirar, a, Joao, motivo) };
        Assert.NotEmpty(RegrasOperacaoTerritorial.ValidarConjunto(fixarERetirar, exclusivo: false));
    }

    [Fact]
    public void Fixar_exige_motivo_cliente_do_universo_e_territorio_ativo()
    {
        var a = T("A");
        var semMotivo = M(TipoMudancaTerritorial.Fixar, a, Joao);
        Assert.Contains(RegrasOperacaoTerritorial.ValidarMudanca(semMotivo, Contexto(Mapa(), [a], Hoje)), e => e.Contains("motivo"));
        var fora = Contexto(Mapa(), [a], Hoje) with { NoUniverso = _ => false };
        Assert.Contains(RegrasOperacaoTerritorial.ValidarMudanca(M(TipoMudancaTerritorial.Fixar, a, Joao, new() { Motivo = "x" }), fora),
            e => e.Contains("universo"));
    }

    // ------------------------------------------------------------------ Cenário, simulação e plano

    private static RegraTerritorio Regra(Territorio t, int? prioridade = null) => new()
    {
        Id = Guid.NewGuid(), MapaId = MapaId, TerritorioId = t.Id, Numero = 1, Grupos = "[]", Criterios = "", Prioridade = prioridade,
        InicioEm = new(2026, 1, 1), OperacaoId = Guid.NewGuid()
    };

    [Fact]
    public void Simulacao_separa_efeito_da_operacao_de_divergencia_e_o_plano_fecha_antes_de_abrir()
    {
        var mg = T("MG");
        var sp = T("SP");
        var regraMg = Regra(mg);
        // Joao está em MG pela regra; Maria atende a regra de SP mas nunca foi atribuída (divergência que já existia).
        var atribuicaoJoao = new AtribuicaoTerritorio
        {
            Id = Guid.NewGuid(), MapaId = MapaId, Exclusivo = true, TerritorioId = mg.Id, PessoaId = Joao, InicioEm = new(2026, 2, 1),
            Origem = OrigemAtribuicaoTerritorio.Regra, RegraId = regraMg.Id, OperacaoId = Guid.NewGuid()
        };
        var estado = new EstadoTerritorialEmD(Mapa(), Hoje, new[] { mg, sp }.ToDictionary(t => t.Id), [regraMg], [], [atribuicaoJoao]);
        var novaRegraSp = M(TipoMudancaTerritorial.NovaVersaoRegra, sp, dados: new() { Grupos = "[{}]", Prioridade = 1 });
        var fixarJoao = M(TipoMudancaTerritorial.Fixar, sp, Joao, new() { Motivo = "Pedido do cliente" });
        var op = Op(Hoje, novaRegraSp, fixarJoao);

        var universo = new HashSet<Guid> { Joao, Maria };
        IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> candidatos = new Dictionary<Guid, IReadOnlySet<Guid>>
        {
            [mg.Id] = new HashSet<Guid> { Joao }, [sp.Id] = new HashSet<Guid> { Maria }
        };
        var com = CenarioTerritorial.Montar(estado, op.Mudancas).Resolver(universo, candidatos, estado.AtribuicoesVigentes);
        var candidatosSem = new Dictionary<Guid, IReadOnlySet<Guid>> { [mg.Id] = new HashSet<Guid> { Joao } };
        var sem = CenarioTerritorial.Montar(estado, []).Resolver(universo, candidatosSem, estado.AtribuicoesVigentes);
        var versao = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var resultado = SimulacaoTerritorial.Comparar(com, sem, estado.AtribuicoesVigentes, Hoje, versao);

        var joao = resultado.Afetados.Single(a => a.Decisao.PessoaId == Joao);
        Assert.Equal(EfeitoNoCliente.Muda, joao.Efeito);
        Assert.Equal(OrigemEfeitoSimulado.EstaOperacao, joao.Origem);
        var maria = resultado.Afetados.Single(a => a.Decisao.PessoaId == Maria);
        Assert.Equal(EfeitoNoCliente.Entra, maria.Efeito);
        Assert.Equal(OrigemEfeitoSimulado.EstaOperacao, maria.Origem); // a regra de SP é nova: é efeito da operação
        Assert.Equal(resultado.Assinatura, SimulacaoTerritorial.Assinar(resultado.Afetados.Reverse(), Hoje, versao)); // ordem não importa
        Assert.NotEqual(resultado.Assinatura, SimulacaoTerritorial.Assinar(resultado.Afetados, Hoje.AddDays(1), versao));

        var plano = AplicacaoTerritorial.Planejar(op, estado, resultado, new Dictionary<Guid, int> { [mg.Id] = 1 }, []);
        Assert.Equal(novaRegraSp.Id, Assert.Single(plano.RegrasNovas).Id); // o id da mudança: igual ao da simulação
        Assert.Equal(1, plano.RegrasNovas[0].Numero);
        Assert.Equal(fixarJoao.Id, Assert.Single(plano.ExcecoesNovas).Id);
        var fechamento = Assert.Single(plano.Fechamentos);
        Assert.Equal(atribuicaoJoao.Id, fechamento.LinhaId);
        Assert.Equal(Hoje.AddDays(-1), fechamento.NovoFim);
        Assert.False(fechamento.Anular);
        Assert.Equal(2, plano.AtribuicoesNovas.Count);
        Assert.Equal(2, plano.Itens.Count);
    }

    [Fact]
    public void Divergencia_que_ja_existia_aparece_separada()
    {
        var sp = T("SP");
        var regraSp = Regra(sp);
        var estado = new EstadoTerritorialEmD(Mapa(), Hoje, new[] { sp }.ToDictionary(t => t.Id), [regraSp], [], []);
        var universo = new HashSet<Guid> { Maria };
        IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> candidatos = new Dictionary<Guid, IReadOnlySet<Guid>> { [sp.Id] = new HashSet<Guid> { Maria } };
        var op = Op(Hoje);
        var com = CenarioTerritorial.Montar(estado, op.Mudancas).Resolver(universo, candidatos, []);
        var sem = CenarioTerritorial.Montar(estado, []).Resolver(universo, candidatos, []);
        var resultado = SimulacaoTerritorial.Comparar(com, sem, [], Hoje, [1]);
        Assert.Equal(OrigemEfeitoSimulado.DivergenciaExistente, Assert.Single(resultado.Afetados).Origem);
        Assert.Equal(1, resultado.Divergencias);
    }

    [Fact]
    public void Linha_que_comeca_no_proprio_efeito_e_anulada_e_nao_encerrada()
    {
        var sp = T("SP");
        var regraDeHoje = Regra(sp);
        regraDeHoje.InicioEm = Hoje; // outra operação do mesmo dia abriu esta versão
        var estado = new EstadoTerritorialEmD(Mapa(), Hoje, new[] { sp }.ToDictionary(t => t.Id), [regraDeHoje], [], []);
        var op = Op(Hoje, M(TipoMudancaTerritorial.NovaVersaoRegra, sp, dados: new() { Grupos = "[{}]" }));
        var resultado = new ResultadoSimulacaoTerritorial { Afetados = [], Assinatura = [] };
        var plano = AplicacaoTerritorial.Planejar(op, estado, resultado, new Dictionary<Guid, int> { [sp.Id] = 1 }, []);
        var fechamento = Assert.Single(plano.Fechamentos);
        Assert.True(fechamento.Anular);
        Assert.Equal(2, plano.RegrasNovas.Single().Numero);
    }

    [Fact]
    public void Encerrar_territorio_fecha_posicao_regra_excecoes_e_responsaveis_como_consequencia()
    {
        var sp = T("SP");
        var regraSp = Regra(sp);
        var excecao = new ExcecaoTerritorio
        {
            Id = Guid.NewGuid(), MapaId = MapaId, Exclusivo = true, TerritorioId = sp.Id, PessoaId = Joao, Tipo = TipoExcecaoTerritorio.Fixar,
            InicioEm = new(2026, 3, 1), Motivo = "x", OperacaoId = Guid.NewGuid()
        };
        var comecou = new TerritorioResponsavel { Id = Guid.NewGuid(), TerritorioId = sp.Id, PessoaId = Maria, TipoCarteiraId = Guid.NewGuid(), InicioEm = new(2026, 1, 1) };
        var futuro = new TerritorioResponsavel { Id = Guid.NewGuid(), TerritorioId = sp.Id, PessoaId = Joao, TipoCarteiraId = Guid.NewGuid(), InicioEm = Hoje.AddDays(10) };
        var estado = new EstadoTerritorialEmD(Mapa(), Hoje, new[] { sp }.ToDictionary(t => t.Id), [regraSp], [excecao], []);
        var op = Op(Hoje, M(TipoMudancaTerritorial.EncerrarTerritorio, sp));
        var plano = AplicacaoTerritorial.Planejar(op, estado, new ResultadoSimulacaoTerritorial { Afetados = [], Assinatura = [] },
            new Dictionary<Guid, int>(), [comecou, futuro]);

        Assert.Contains(plano.Fechamentos, f => f.Tabela == TabelaFechamentoTerritorial.Posicao && f.NovoFim == Hoje.AddDays(-1));
        Assert.Contains(plano.Fechamentos, f => f.Tabela == TabelaFechamentoTerritorial.Regra && f.LinhaId == regraSp.Id);
        Assert.Contains(plano.Fechamentos, f => f.Tabela == TabelaFechamentoTerritorial.Excecao && f.LinhaId == excecao.Id);
        Assert.Contains(plano.Fechamentos, f => f.LinhaId == comecou.Id && !f.Anular);
        Assert.Contains(plano.Fechamentos, f => f.LinhaId == futuro.Id && f.Anular);
        var alterado = Assert.Single(plano.Territorios);
        Assert.Equal(SituacaoTerritorio.Encerrado, alterado.Situacao);
        Assert.Equal(Hoje.AddDays(-1), alterado.FimEm);
    }

    // ------------------------------------------------------------------ Volume (DN-12)

    [Fact]
    public void Volume_avisa_a_partir_de_dez_mil_e_recusa_acima_de_cinquenta_mil()
    {
        Assert.Equal(50_000, RegrasOperacaoTerritorial.LimiteClientesPorOperacao);
        Assert.Equal(10_000, RegrasOperacaoTerritorial.AvisoClientesPorOperacao);
        Assert.Equal(10, RegrasOperacaoTerritorial.ClientesGravados(1, 2, 3, 4));

        Assert.Null(RegrasOperacaoTerritorial.AvisoDeVolume(9_999));
        Assert.Contains("10.000 clientes", RegrasOperacaoTerritorial.AvisoDeVolume(10_000), StringComparison.Ordinal);
        Assert.NotNull(RegrasOperacaoTerritorial.AvisoDeVolume(50_000));
        Assert.Null(RegrasOperacaoTerritorial.AvisoDeVolume(50_001)); // acima do limite quem fala é a recusa

        Assert.Null(RegrasOperacaoTerritorial.AcimaDoLimite(50_000)); // o limite em si é aplicado (provado no teste de volume)
        var recusa = RegrasOperacaoTerritorial.AcimaDoLimite(50_001);
        Assert.NotNull(recusa);
        Assert.Contains("afetará 50.001 clientes", recusa, StringComparison.Ordinal);
        Assert.Contains("50.000", recusa, StringComparison.Ordinal);
    }
}
