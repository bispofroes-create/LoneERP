using Lone.Contracts.Territorios;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Consultas;
using Microsoft.EntityFrameworkCore;
using static Lone.Tests.Infraestrutura.MotorTerritorialTeste;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// O motor territorial (Fase 2b-1b) de ponta a ponta no SQL Server, com os serviços e repositórios de verdade: numeração
/// TE- sem MAX+1 (DN-13), simulação que não toca em fato (seção I), aplicação tudo ou nada com assinatura (seção J),
/// desfazer (DN-05), RT-1, T8/DN-08, DN-01, DN-02, L3, gatilhos 50073–50076 e FK de Exclusivo gravando direto no banco,
/// histórico e texto congelado (seção L). Rodam duas vezes: padrão do SQL Server e com RCSI/SNAPSHOT.
/// </summary>
public abstract partial class BancoTerritoriosTestesBase
{
    private protected BancoDeTeste Banco => _fixture.Banco!;

    // ------------------------------------------------------------------ Numeração (DN-13)

    [FatoSqlServer]
    public async Task Rascunhos_criados_ao_mesmo_tempo_recebem_numeros_TE_diferentes()
    {
        var c = await CenarioAsync(Banco, porEtiqueta: 0);
        var tarefas = Enumerable.Range(0, 8).Select(_ => Operacoes(Banco).CriarAsync(
            new CriarOperacaoTerritorialRequisicao { MapaId = c.Mapa.Id, EfeitoEm = Hoje, Motivo = "Numeração" })).ToList();
        var criadas = await Task.WhenAll(tarefas);
        Assert.Equal(8, criadas.Select(o => o.Numero).Distinct().Count());
        Assert.All(criadas, o => Assert.StartsWith("TE-2026-", o.Numero));
    }

    // ------------------------------------------------------------------ Simulação (seção I, DN-03)

    [FatoSqlServer]
    public async Task Simular_nao_grava_fato_nem_muda_o_motor_e_guarda_a_evidencia()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var op = await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Mg.Id, c.EtiquetaMg), Regra(c.Sp.Id, c.EtiquetaSp));
        var fatos = await FatosAsync(Banco, c.Mapa.Id);
        var motor = await VersaoMotorAsync(Banco, c.Mapa.Id);

        var simulada = await SimularAsync(s, op);
        var denovo = await SimularAsync(s, simulada);

        Assert.Equal(fatos, await FatosAsync(Banco, c.Mapa.Id));
        Assert.Equal(motor, await VersaoMotorAsync(Banco, c.Mapa.Id));
        Assert.Equal(SituacaoOperacaoTerritorial.Simulada, denovo.Situacao);
        Assert.Equal(6, denovo.SimulacaoAtual!.Entram);
        var simulacoes = await s.SimulacoesAsync(op.Id);
        Assert.Equal(2, simulacoes.Count); // a anterior continua guardada (DN-03)
        Assert.Single(simulacoes, x => x.Atual);
        var itens = await s.ItensSimulacaoAsync(simulada.SimulacaoAtual!.Id, new FiltroItensOperacaoTerritorialDto());
        Assert.Equal(6, itens.Total); // a primeira simulação, intacta
    }

    // ------------------------------------------------------------------ Aplicar e desfazer (seção J, DN-05)

    [FatoSqlServer]
    public async Task Aplicar_grava_regras_e_atribuicoes_de_uma_vez_e_desfazer_antes_do_efeito_volta_tudo()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var efeito = Hoje.AddDays(10);
        var aplicada = await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, efeito, Regra(c.Mg.Id, c.EtiquetaMg), Regra(c.Sp.Id, c.EtiquetaSp)));

        Assert.Equal(SituacaoOperacaoTerritorial.Aplicada, aplicada.Situacao);
        Assert.True(aplicada.Agendada);
        Assert.Equal(6, aplicada.Entraram);
        var atribuidos = await AtribuidosAsync(Banco, c.Mapa.Id, efeito);
        Assert.All(c.ClientesMg, p => Assert.Equal(c.Mg.Id, atribuidos[p]));
        Assert.All(c.ClientesSp, p => Assert.Equal(c.Sp.Id, atribuidos[p]));
        Assert.Empty(await AtribuidosAsync(Banco, c.Mapa.Id, Hoje)); // antes do efeito, nada muda
        var uso = await new UsoTerritorialSql(new Fabrica(Banco), new UsuarioTeste()).TerritoriosComUsoAsync(c.Mapa.Id, default);
        Assert.Equal(new[] { c.Mg.Id, c.Sp.Id }.Order(), uso.Order());
        Assert.True(aplicada.PodeDesfazer);

        var desfeita = await s.DesfazerAsync(aplicada.Id, new MotivoOperacaoTerritorialRequisicao { Versao = aplicada.Versao, Motivo = "Planejamento revisto" });

        Assert.Equal(SituacaoOperacaoTerritorial.Desfeita, desfeita.Situacao);
        Assert.Empty(await AtribuidosAsync(Banco, c.Mapa.Id, efeito));
        await using var db = Banco.Contexto();
        Assert.Equal(6, await db.AtribuicoesTerritorio.CountAsync(a => a.MapaId == c.Mapa.Id && !a.Ativo && a.OperacaoAnulacaoId == aplicada.Id));
        Assert.Equal(2, await db.RegrasTerritorio.CountAsync(r => r.MapaId == c.Mapa.Id && !r.Ativo)); // anuladas, visíveis no histórico
        var motor = await db.MapaTerritorialMotores.AsNoTracking().SingleAsync(m => m.MapaId == c.Mapa.Id);
        Assert.Null(motor.UltimaOperacaoId);
        Assert.Null(motor.UltimoEfeitoEm);
        Assert.Equal(6, await db.OperacaoTerritorialItens.CountAsync(i => i.OperacaoId == aplicada.Id)); // a evidência fica
    }

    [FatoSqlServer]
    public async Task Cadastro_que_mudou_depois_da_simulacao_recusa_a_aplicacao_sem_gravar_nada_e_registra_a_tentativa()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var simulada = await SimularAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Mg.Id, c.EtiquetaMg)));
        await ClientesAsync(Banco, 1, c.EtiquetaMg); // um cliente novo com a etiqueta: o resultado muda

        var erro = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => AplicarAsync(s, simulada));

        Assert.Equal(RegrasOperacaoTerritorial.MensagemAssinaturaDiferente, erro.Message);
        Assert.Equal((0, 0, 0), await FatosAsync(Banco, c.Mapa.Id));
        Assert.Equal(SituacaoOperacaoTerritorial.Simulada, (await s.ObterAsync(simulada.Id))!.Situacao);
        await using var db = Banco.Contexto();
        Assert.Contains(await db.Auditoria.Where(a => a.RaizId == simulada.Id && a.Acao == AcaoAuditoria.Evento).Select(a => a.Descricao).ToListAsync(),
                        d => d!.Contains("recusada", StringComparison.Ordinal));
    }

    [FatoSqlServer]
    public async Task Volume_entre_o_aviso_e_o_limite_avisa_e_acima_do_limite_nao_aplica_nem_trava_o_mapa()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var simulada = await SimularAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Mg.Id, c.EtiquetaMg)));

        // O volume é fingido nas contagens da simulação, sem cadastrar 50.001 clientes: o limite em si (50.000 aplicados numa
        // transação) é provado em VolumeOperacoesTerritoriaisTests; aqui, o caminho da tela e da recusa (DN-12).
        async Task ContagemAsync(int entram)
        {
            await using var db = Banco.Contexto();
            await db.OperacaoTerritorialSimulacoes.Where(x => x.Id == simulada.SimulacaoAtual!.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Entram, entram));
        }

        await ContagemAsync(RegrasOperacaoTerritorial.AvisoClientesPorOperacao);
        var comAviso = (await s.ObterAsync(simulada.Id))!;
        Assert.True(comAviso.PodeAplicar);
        Assert.Contains(comAviso.Avisos, a => a.Contains("afetará 10.000 clientes", StringComparison.Ordinal));

        await ContagemAsync(RegrasOperacaoTerritorial.LimiteClientesPorOperacao + 1);
        var acima = (await s.ObterAsync(simulada.Id))!;
        Assert.False(acima.PodeAplicar);
        Assert.Contains(acima.Avisos, a => a.Contains("ultrapassando o limite", StringComparison.Ordinal));

        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => AplicarAsync(s, simulada));
        Assert.Contains(erro.Erros, e => e.Contains("50.001 clientes", StringComparison.Ordinal));
        Assert.Equal((0, 0, 0), await FatosAsync(Banco, c.Mapa.Id));
        await using var depois = Banco.Contexto();
        Assert.Null((await depois.MapaTerritorialMotores.AsNoTracking().SingleAsync(m => m.MapaId == c.Mapa.Id)).UltimaOperacaoId);
        Assert.Contains(await depois.Auditoria.Where(a => a.RaizId == simulada.Id && a.Acao == AcaoAuditoria.Evento).Select(a => a.Descricao).ToListAsync(),
                        d => d!.Contains("ultrapassando o limite", StringComparison.Ordinal));
    }

    [FatoSqlServer]
    public async Task Operacao_sem_mudancas_corrige_divergencias_do_cadastro_e_sem_nada_a_gravar_nao_aplica()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Mg.Id, c.EtiquetaMg)));
        var novo = Assert.Single(await ClientesAsync(Banco, 1, c.EtiquetaMg)); // cadastro mudou sem operação: divergência

        var divergencias = await s.DivergenciasAsync(c.Mapa.Id, new FiltroItensOperacaoTerritorialDto());
        Assert.Equal(1, divergencias.Entram);

        // "Criar operação com estas" (DN-14): rascunho sem mudanças; a simulação mostra só divergência e a aplicação corrige.
        var simulada = await SimularAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje));
        Assert.Equal(1, simulada.SimulacaoAtual!.Divergencias);
        Assert.Equal(0, simulada.SimulacaoAtual.DaOperacao);
        var aplicada = await AplicarAsync(s, simulada);
        Assert.Equal(1, aplicada.Entraram);
        Assert.Equal(c.Mg.Id, (await AtribuidosAsync(Banco, c.Mapa.Id, Hoje))[novo]);
        Assert.Equal(0, (await s.DivergenciasAsync(c.Mapa.Id, new FiltroItensOperacaoTerritorialDto())).Itens.Total);

        // Sem mudanças e sem divergências: nada a gravar, a aplicação recusa e nada muda.
        var vazia = await SimularAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje));
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => AplicarAsync(s, vazia));
        Assert.Contains(erro.Erros, e => e.StartsWith("Nada a aplicar", StringComparison.Ordinal));
        Assert.Equal(SituacaoOperacaoTerritorial.Simulada, (await s.ObterAsync(vazia.Id))!.Situacao);
    }

    [FatoSqlServer]
    public async Task Rascunho_editado_por_outra_pessoa_fica_com_a_autoria_e_o_antes_e_depois_na_historia()
    {
        var c = await CenarioAsync(Banco);
        var maria = await UsuarioAsync(Banco, "Maria " + Guid.NewGuid().ToString("N")[..6]);
        var joao = await UsuarioAsync(Banco, "João " + Guid.NewGuid().ToString("N")[..6]);
        var daMaria = Operacoes(Banco, usuario: maria);
        var doJoao = Operacoes(Banco, usuario: joao);

        // Maria cria e inclui uma mudança: eventos sem "alterou" (é dela), sem destaque de autoria.
        var op = await RascunhoAsync(daMaria, c.Mapa.Id, Hoje, Regra(c.Mg.Id, c.EtiquetaMg));
        Assert.Empty(op.EditadaTambemPor);
        Assert.Contains(op.Historico, h => h.Descricao is { } d && d.StartsWith($"Operação {op.Numero}: mudança 1 incluída — Nova versão da regra de", StringComparison.Ordinal));

        // João (outro PLANEJAR) troca o motivo e retira a mudança: pode (DN-06), e fica dito quem fez o quê na operação de quem.
        var alterada = await doJoao.AlterarAsync(op.Id, new AlterarOperacaoTerritorialRequisicao
            { Versao = op.Versao, EfeitoEm = op.EfeitoEm, Motivo = "Revisado pelo João" });
        var retirada = await doJoao.RetirarMudancaAsync(op.Id, alterada.Mudancas.Single().Id, new VersaoOperacaoTerritorialRequisicao { Versao = alterada.Versao });

        var tela = (await daMaria.ObterAsync(op.Id))!;
        Assert.Equal(new[] { joao.Nome }, tela.EditadaTambemPor);
        Assert.Equal(joao.Nome, tela.UltimaEdicaoPor);
        Assert.Contains(tela.Historico, h => h.Descricao == $"{joao.Nome} alterou a operação {op.Numero}, criada por {maria.Nome}: motivo alterado.");
        Assert.Contains(tela.Historico, h => h.Descricao is { } d &&
            d.StartsWith($"{joao.Nome} alterou a operação {op.Numero}, criada por {maria.Nome}: mudança 1 retirada — Nova versão da regra de", StringComparison.Ordinal));
        var motivo = Assert.Single(tela.Historico, h => h.Acao == AcaoAuditoria.Alteracao);
        Assert.Equal(nameof(OperacaoTerritorial.Motivo), motivo.Campo);
        Assert.Equal("Teste", motivo.ValorAnterior);
        Assert.Equal("Revisado pelo João", motivo.ValorNovo);
        Assert.Equal(joao.Nome, motivo.Usuario);
        Assert.Empty(retirada.Mudancas);
    }

    [FatoSqlServer]
    public async Task Duas_aplicacoes_simultaneas_no_mesmo_mapa_so_uma_grava_e_a_outra_nao_deixa_nada()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var a = await SimularAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Mg.Id, c.EtiquetaMg)));
        var b = await SimularAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Sp.Id, c.EtiquetaSp)));

        var resultados = await Task.WhenAll(Tentar(() => AplicarAsync(Operacoes(Banco), a)), Tentar(() => AplicarAsync(Operacoes(Banco), b)));

        Assert.Single(resultados, r => r is null);
        var recusa = Assert.IsType<ConflitoDeEdicaoException>(resultados.Single(r => r is not null));
        Assert.Equal(RegrasOperacaoTerritorial.MensagemMapaMudou, recusa.Message);
        var (regras, _, atribuicoes) = await FatosAsync(Banco, c.Mapa.Id);
        Assert.Equal(1, regras);
        Assert.Equal(3, atribuicoes);
    }

    [FatoSqlServer]
    public async Task Simulacao_fica_desatualizada_quando_outra_operacao_e_aplicada_e_nao_aplica()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var segunda = await SimularAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Sp.Id, c.EtiquetaSp)));
        await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Mg.Id, c.EtiquetaMg)));

        var lida = (await s.ObterAsync(segunda.Id))!;
        Assert.True(lida.Desatualizada);
        Assert.False(lida.PodeAplicar);
        var erro = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => AplicarAsync(s, lida));
        Assert.Equal(RegrasOperacaoTerritorial.MensagemMapaMudou, erro.Message);
    }

    // ------------------------------------------------------------------ Datas: RT-1, T8/DN-08, DN-01

    [FatoSqlServer]
    public async Task Operacao_futura_aplicada_bloqueia_a_anterior_e_diz_qual_desfazer()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var futura = await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje.AddDays(30), Regra(c.Mg.Id, c.EtiquetaMg)));
        var anterior = await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Sp.Id, c.EtiquetaSp));

        Assert.Equal(futura.Numero, anterior.BloqueadaPor);
        Assert.True(anterior.PodeDesfazerBloqueio);
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => SimularAsync(s, anterior));
        Assert.Contains(erro.Erros, e => e.Contains($"primeiro desfaça a {futura.Numero}", StringComparison.Ordinal));
    }

    [FatoSqlServer]
    public async Task Trinta_e_um_dias_atras_e_recusado_com_o_padrao_e_aceito_depois_de_mudar_o_parametro()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var op = await RascunhoAsync(s, c.Mapa.Id, Hoje.AddDays(-31), Regra(c.Mg.Id, c.EtiquetaMg));
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => SimularAsync(s, op));
        Assert.Contains(erro.Erros, e => e.Contains("30 dia", StringComparison.Ordinal));

        var parametros = Parametros(Banco);
        var atual = await parametros.ObterAsync();
        Assert.Equal(30, atual.DiasRetroativosMaximo);
        var novo = await parametros.SalvarAsync(new ParametrosTerritoriaisDto { Versao = atual.Versao, DiasRetroativosMaximo = 40 });
        try
        {
            Assert.Equal(SituacaoOperacaoTerritorial.Simulada, (await SimularAsync(s, op)).Situacao);
        }
        finally
        {
            await parametros.SalvarAsync(new ParametrosTerritoriaisDto { Versao = novo.Versao, DiasRetroativosMaximo = 30 });
        }
    }

    [FatoSqlServer]
    public async Task Mudanca_de_estrutura_com_efeito_futuro_e_recusada()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var op = await RascunhoAsync(s, c.Mapa.Id, Hoje.AddDays(1));
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => s.IncluirMudancaAsync(op.Id, new IncluirMudancaTerritorialRequisicao
        {
            Versao = op.Versao, Tipo = TipoMudancaTerritorial.MoverTerritorio, TerritorioId = c.Sp.Id, NovoPaiId = c.Mg.Id
        }));
        Assert.Contains(erro.Erros, e => e.Contains("só com efeito até hoje", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ Estrutura por operação (DN-01, DN-09)

    [FatoSqlServer]
    public async Task Mover_territorio_com_uso_por_operacao_fecha_a_posicao_na_vespera_e_abre_outra_no_efeito()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje.AddDays(-5), Regra(c.Mg.Id, c.EtiquetaMg), Regra(c.Sp.Id, c.EtiquetaSp)));
        var arvoreAntes = await VersaoDaArvoreAsync(c.Mapa.Id);

        var mover = await RascunhoAsync(s, c.Mapa.Id, Hoje, new IncluirMudancaTerritorialRequisicao
        {
            Tipo = TipoMudancaTerritorial.MoverTerritorio, TerritorioId = c.Sp.Id, NovoPaiId = c.Mg.Id
        });
        var aplicada = await SimularEAplicarAsync(s, mover);

        await using var db = Banco.Contexto();
        var sp = await db.Territorios.AsNoTracking().Include(t => t.Posicoes).SingleAsync(t => t.Id == c.Sp.Id);
        Assert.Equal(c.Mg.Id, sp.PaiId);
        Assert.Contains(sp.Posicoes, p => p.Ativo && p.PaiId == c.Brasil.Id && p.FimEm == Hoje.AddDays(-1) && p.OperacaoEncerramentoId == aplicada.Id);
        Assert.Contains(sp.Posicoes, p => p.Ativo && p.PaiId == c.Mg.Id && p.InicioEm == Hoje && p.FimEm == null && p.OperacaoId == aplicada.Id);
        Assert.NotEqual(arvoreAntes, await VersaoDaArvoreAsync(c.Mapa.Id));
        Assert.Equal(SituacaoOperacaoTerritorial.Aplicada, aplicada.Situacao);
        Assert.False(aplicada.PodeDesfazer); // estrutura só com efeito até hoje: nunca é desfeita (DN-01, DN-05)
    }

    [FatoSqlServer]
    public async Task Encerrar_territorio_por_operacao_encerra_regra_atribuicoes_e_responsaveis_como_consequencia()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje.AddDays(-5), Regra(c.Mg.Id, c.EtiquetaMg), Regra(c.Sp.Id, c.EtiquetaSp)));
        var cadastros = await CadastrosAsync();
        await ExecutarNoBancoAsync(InsertResponsavel(c.Sp.Id, cadastros.P1, cadastros.F1, "2026-01-01", null));

        var aplicada = await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje,
            new IncluirMudancaTerritorialRequisicao { Tipo = TipoMudancaTerritorial.EncerrarTerritorio, TerritorioId = c.Sp.Id }));

        Assert.Equal(3, aplicada.Sairam);
        await using var db = Banco.Contexto();
        var sp = await db.Territorios.AsNoTracking().SingleAsync(t => t.Id == c.Sp.Id);
        Assert.Equal(SituacaoTerritorio.Encerrado, sp.Situacao);
        Assert.Equal(Hoje.AddDays(-1), sp.FimEm);
        Assert.Equal(Hoje.AddDays(-1), (await db.RegrasTerritorio.AsNoTracking().SingleAsync(r => r.TerritorioId == c.Sp.Id)).FimEm);
        var responsavel = await db.TerritorioResponsaveis.AsNoTracking().SingleAsync(r => r.TerritorioId == c.Sp.Id);
        Assert.Equal(Hoje.AddDays(-1), responsavel.FimEm);
        Assert.Equal(aplicada.Id, responsavel.OperacaoEncerramentoId);
        var atribuidos = await AtribuidosAsync(Banco, c.Mapa.Id, Hoje);
        Assert.DoesNotContain(c.ClientesSp, atribuidos.ContainsKey);
        Assert.All(c.ClientesMg, p => Assert.Equal(c.Mg.Id, atribuidos[p]));
    }

    // ------------------------------------------------------------------ DN-02 e L3 (ajustes da 2b-1a)

    [FatoSqlServer]
    public async Task Versao_do_motor_nao_muda_com_o_nome_do_mapa_e_muda_com_a_arvore_e_a_ativacao()
    {
        var c = await CenarioAsync(Banco, porEtiqueta: 0);
        var v0 = await VersaoMotorAsync(Banco, c.Mapa.Id);

        var ficha = (await Mapas().ObterAsync(c.Mapa.Id, default))!;
        ficha.Nome += " (renomeado)";
        await Mapas().SalvarAsync(ficha, novo: false, default);
        Assert.Equal(v0, await VersaoMotorAsync(Banco, c.Mapa.Id));

        var norte = Novo(c.Mapa.Id, "Norte", c.Brasil.Id);
        await Repositorio().SalvarAsync(norte, novo: true, await VersaoDaArvoreAsync(c.Mapa.Id), new HashSet<Guid>(), default);
        var v1 = await VersaoMotorAsync(Banco, c.Mapa.Id);
        Assert.NotEqual(v0, v1);

        ficha = (await Mapas().ObterAsync(c.Mapa.Id, default))!;
        ficha.Desativar();
        await Mapas().SalvarAsync(ficha, novo: false, default);
        Assert.NotEqual(v1, await VersaoMotorAsync(Banco, c.Mapa.Id));
    }

    [FatoSqlServer]
    public async Task Mudanca_de_estrutura_conferida_sem_uso_e_recusada_se_uma_aplicacao_deu_uso_no_meio()
    {
        var c = await CenarioAsync(Banco);
        var vista = await VersaoDaArvoreAsync(c.Mapa.Id);
        var semUso = new HashSet<Guid>(); // o que a tela conferiu
        var s = Operacoes(Banco);
        await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Sp.Id, c.EtiquetaSp))); // SP ganha uso

        var sp = (await Repositorio().ObterAsync(c.Sp.Id, default))!;
        var mover = new Territorio
        {
            Id = sp.Id, Versao = sp.Versao, MapaId = sp.MapaId, Codigo = sp.Codigo, Nome = sp.Nome, TipoId = sp.TipoId, PaiId = c.Mg.Id,
            Situacao = sp.Situacao, Posicoes = sp.Posicoes, Responsaveis = sp.Responsaveis
        };
        RegrasArvoreTerritorial.AjustarPosicoes(mover, sp, new DateOnly(2026, 1, 1), usoNaSubarvore: false);
        var erro = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => Repositorio().SalvarAsync(mover, novo: false, vista, semUso, default));
        // A árvore não mudou (a aplicação não é estrutural): quem recusa é o uso relido depois das travas.
        Assert.Equal(RegrasArvoreTerritorial.MensagemUsoMudou, erro.Message);
        Assert.Equal(c.Brasil.Id, (await Repositorio().ObterAsync(c.Sp.Id, default))!.PaiId);
    }

    [FatoSqlServer]
    public async Task Mapa_que_ganhou_uso_nao_muda_a_exclusividade_nem_pelo_repositorio_nem_direto_no_banco()
    {
        var c = await CenarioAsync(Banco);
        var ficha = (await Mapas().ObterAsync(c.Mapa.Id, default))!; // aberta sem uso
        var s = Operacoes(Banco);
        await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Mg.Id, c.EtiquetaMg)));

        ficha.Exclusivo = false;
        var erro = await Assert.ThrowsAsync<ConflitoDeEdicaoException>(() => Mapas().SalvarAsync(ficha, novo: false, default));
        Assert.Equal(RegrasMapaTerritorial.MensagemPassouATerUso, erro.Message);

        var direto = await Recusado(() => ExecutarNoBancoAsync($"UPDATE MapasTerritoriais SET Exclusivo = 0 WHERE Id = '{c.Mapa.Id}'"));
        Assert.Equal(547, direto.Number); // FK (MapaId, Exclusivo) das atribuições
    }

    // ------------------------------------------------------------------ Gatilhos 50073–50076 direto no banco

    [FatoSqlServer]
    public async Task Gatilhos_do_motor_barram_gravacoes_feitas_direto_no_banco()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var aplicada = await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Mg.Id, c.EtiquetaMg), Regra(c.Sp.Id, c.EtiquetaSp)));
        Guid regraMg;
        await using (var db = Banco.Contexto())
            regraMg = await db.RegrasTerritorio.Where(r => r.TerritorioId == c.Mg.Id).Select(r => r.Id).SingleAsync();
        var op = aplicada.Id;
        var cliente = c.ClientesMg[0];

        // 50073: regra publicada é imutável; versões ativas do mesmo território não se cruzam.
        Assert.Equal(50073, (await Recusado(() => ExecutarNoBancoAsync($"UPDATE RegrasTerritorio SET Prioridade = 5 WHERE Id = '{regraMg}'"))).Number);
        Assert.Equal(50073, (await Recusado(() => ExecutarNoBancoAsync($"""
            INSERT INTO RegrasTerritorio (Id, MapaId, TerritorioId, Numero, Grupos, Criterios, Prioridade, InicioEm, FimEm, Ativo, OperacaoId, CriadoEm)
            VALUES ('{Guid.NewGuid()}', '{c.Mapa.Id}', '{c.Mg.Id}', 9, '{"{}"}', 'x', NULL, '2026-12-01', '2026-12-31', 1, '{op}', SYSUTCDATETIME())
            """))).Number);
        // Fechar e anular continuam permitidos (é o que as operações fazem).
        await ExecutarNoBancoAsync($"UPDATE RegrasTerritorio SET FimEm = '2027-12-31' WHERE Id = '{regraMg}'");

        // 50074: o mesmo cliente fixado em dois territórios do mapa exclusivo no mesmo período (duas linhas, um comando).
        Assert.Equal(50074, (await Recusado(() => ExecutarNoBancoAsync($"""
            INSERT INTO ExcecoesTerritorio (Id, MapaId, Exclusivo, TerritorioId, PessoaId, Tipo, InicioEm, FimEm, Motivo, Origem, Ativo, OperacaoId, CriadoEm)
            VALUES ('{Guid.NewGuid()}', '{c.Mapa.Id}', 1, '{c.Mg.Id}', '{cliente}', 1, '2026-10-01', '2026-10-31', 'a', 1, 1, '{op}', SYSUTCDATETIME()),
                   ('{Guid.NewGuid()}', '{c.Mapa.Id}', 1, '{c.Sp.Id}', '{cliente}', 1, '2026-10-15', '2026-11-15', 'b', 1, 1, '{op}', SYSUTCDATETIME())
            """))).Number);
        // Fixar e Retirar do mesmo território no mesmo período.
        Assert.Equal(50074, (await Recusado(() => ExecutarNoBancoAsync($"""
            INSERT INTO ExcecoesTerritorio (Id, MapaId, Exclusivo, TerritorioId, PessoaId, Tipo, InicioEm, FimEm, Motivo, Origem, Ativo, OperacaoId, CriadoEm)
            VALUES ('{Guid.NewGuid()}', '{c.Mapa.Id}', 1, '{c.Mg.Id}', '{cliente}', 1, '2026-10-01', '2026-10-31', 'a', 1, 1, '{op}', SYSUTCDATETIME()),
                   ('{Guid.NewGuid()}', '{c.Mapa.Id}', 1, '{c.Mg.Id}', '{cliente}', 2, '2026-10-31', NULL, 'b', 1, 1, '{op}', SYSUTCDATETIME())
            """))).Number);

        // 50075: o cliente já está em MG desde hoje; outra atribuição que se cruza (fechada, para não bater no índice da aberta).
        Assert.Equal(50075, (await Recusado(() => ExecutarNoBancoAsync($"""
            INSERT INTO AtribuicoesTerritorio (Id, MapaId, Exclusivo, TerritorioId, PessoaId, InicioEm, FimEm, Origem, RegraId, ExcecaoId, Ativo, OperacaoId, CriadoEm)
            VALUES ('{Guid.NewGuid()}', '{c.Mapa.Id}', 1, '{c.Sp.Id}', '{cliente}', '2026-11-01', '2026-11-30', 1, '{regraMg}', NULL, 1, '{op}', SYSUTCDATETIME())
            """))).Number);
        // Anulada não conta.
        await ExecutarNoBancoAsync($"""
            INSERT INTO AtribuicoesTerritorio (Id, MapaId, Exclusivo, TerritorioId, PessoaId, InicioEm, FimEm, Origem, RegraId, ExcecaoId, Ativo, OperacaoId, CriadoEm)
            VALUES ('{Guid.NewGuid()}', '{c.Mapa.Id}', 1, '{c.Sp.Id}', '{cliente}', '2026-11-01', '2026-11-30', 1, '{regraMg}', NULL, 0, '{op}', SYSUTCDATETIME())
            """);

        // 50076: o resultado aplicado não muda nem some.
        Assert.Equal(50076, (await Recusado(() => ExecutarNoBancoAsync($"UPDATE OperacaoTerritorialItens SET Explicacao = '{"{}"}' WHERE OperacaoId = '{op}'"))).Number);
        Assert.Equal(50076, (await Recusado(() => ExecutarNoBancoAsync($"DELETE FROM OperacaoTerritorialItens WHERE OperacaoId = '{op}'"))).Number);

        // FK composta: território de outro mapa nunca.
        var outro = await CenarioAsync(Banco, porEtiqueta: 0);
        Assert.Equal(547, (await Recusado(() => ExecutarNoBancoAsync($"""
            INSERT INTO AtribuicoesTerritorio (Id, MapaId, Exclusivo, TerritorioId, PessoaId, InicioEm, FimEm, Origem, RegraId, ExcecaoId, Ativo, OperacaoId, CriadoEm)
            VALUES ('{Guid.NewGuid()}', '{c.Mapa.Id}', 1, '{outro.Mg.Id}', '{cliente}', '2027-01-01', NULL, 1, '{regraMg}', NULL, 0, '{op}', SYSUTCDATETIME())
            """))).Number);
    }

    // ------------------------------------------------------------------ Histórico e texto congelado (seção L)

    [FatoSqlServer]
    public async Task Onde_estava_o_cliente_na_data_e_por_que_depois_de_duas_operacoes()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        var primeira = await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje.AddDays(-5), Regra(c.Mg.Id, c.EtiquetaMg)));
        var cliente = c.ClientesMg[0];
        var segunda = await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, new IncluirMudancaTerritorialRequisicao
        {
            Tipo = TipoMudancaTerritorial.Fixar, TerritorioId = c.Sp.Id, PessoaId = cliente, Motivo = "Pedido do cliente"
        }));

        var consultas = Consultas(Banco);
        var antes = (await consultas.DoClienteAsync(cliente, Hoje.AddDays(-1))).Territorios.Single();
        var depois = (await consultas.DoClienteAsync(cliente, Hoje)).Territorios.Single();
        Assert.Equal(c.Mg.Id, antes.TerritorioId);
        Assert.Equal(primeira.Numero, antes.Operacao);
        Assert.Equal("Brasil › MG", antes.Caminho);
        Assert.Equal(c.Sp.Id, depois.TerritorioId);
        Assert.Equal(OrigemAtribuicaoTerritorio.Excecao, depois.Origem);
        Assert.Equal(segunda.Numero, depois.Operacao);

        var itens = await s.ItensAplicadosAsync(segunda.Id, new FiltroItensOperacaoTerritorialDto());
        var item = Assert.Single(itens.Itens);
        Assert.Equal(EfeitoNoCliente.Muda, item.Efeito);
        Assert.Contains("Fixacao", item.Explicacao, StringComparison.Ordinal);
        Assert.Contains("Etiquetas", item.Explicacao, StringComparison.Ordinal); // o atributo lido fica guardado
    }

    [FatoSqlServer]
    public async Task Renomear_a_etiqueta_depois_nao_muda_os_criterios_congelados_da_regra()
    {
        var c = await CenarioAsync(Banco);
        var s = Operacoes(Banco);
        await SimularEAplicarAsync(s, await RascunhoAsync(s, c.Mapa.Id, Hoje, Regra(c.Mg.Id, c.EtiquetaMg)));
        string nomeAntigo;
        await using (var db = Banco.Contexto())
        {
            var etiqueta = await db.Etiquetas.SingleAsync(e => e.Id == c.EtiquetaMg);
            nomeAntigo = etiqueta.Nome;
            etiqueta.Nome = "Renomeada " + Guid.NewGuid().ToString("N")[..6];
            await db.SaveChangesAsync();
        }
        var regra = (await Consultas(Banco).DoTerritorioAsync(c.Mg.Id)).Regras.Single();
        Assert.Contains(nomeAntigo, regra.Criterios, StringComparison.Ordinal);
        Assert.Equal(1, regra.Numero);
        Assert.True(regra.Vigente);
    }
}
