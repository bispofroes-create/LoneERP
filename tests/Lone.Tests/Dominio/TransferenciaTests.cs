using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

/// <summary>
/// Transferência de carteira (Motor Comercial, Fase 1d): o plano de cada cliente (o que passa e o que fica, com o motivo),
/// a aplicação sem apagar nada e sem quebrar as regras da ficha, a divisão pela menor carteira e as contagens.
/// </summary>
public class TransferenciaTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);
    private static readonly DateOnly Efeito = new(2026, 10, 1);

    private static readonly TipoCarteira Vendedor = new()
    {
        Id = Guid.NewGuid(), Nome = "Vendedor", ResponsavelDaConta = true, LimitePorVez = 1, TipoCredito = TipoCreditoComercial.Receita
    };
    private static readonly TipoCarteira Representante = new()
    {
        Id = Guid.NewGuid(), Nome = "Representante", LimitePorVez = 3, TipoCredito = TipoCreditoComercial.Receita
    };
    private static readonly TipoCarteira Supervisor = new() { Id = Guid.NewGuid(), Nome = "Supervisor", TipoCredito = TipoCreditoComercial.Nenhum };
    private static Dictionary<Guid, TipoCarteira> Tipos => new[] { Vendedor, Representante, Supervisor }.ToDictionary(t => t.Id);

    private static readonly Guid Joao = Guid.NewGuid();
    private static readonly Guid Maria = Guid.NewGuid();
    private static readonly Guid Pedro = Guid.NewGuid();
    private static readonly Guid Transferencia = Guid.NewGuid();

    private static FiltroTransferencia Filtro(Guid? papel = null, Guid? empresa = null, DateOnly? efeito = null) =>
        new(Joao, papel, empresa, efeito ?? Efeito);

    private static CarteiraCliente Vinculo(Guid pessoaId, TipoCarteira papel, Guid quem, DateOnly inicio, DateOnly? fim = null,
                                           decimal? credito = null, Guid? empresa = null) => new()
    {
        Id = Guid.NewGuid(), PessoaId = pessoaId, TipoCarteiraId = papel.Id, VendedorId = quem, InicioEm = inicio, FimEm = fim,
        PercentualCredito = credito, EmpresaId = empresa
    };

    private static Pessoa Cliente(params Func<Guid, CarteiraCliente>[] vinculos)
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Cliente" };
        foreach (var v in vinculos) p.Carteira.Add(v(p.Id));
        return p;
    }

    private static PlanoTransferenciaCliente Planejar(Pessoa cliente, FiltroTransferencia filtro, Guid? destino = null) =>
        RegrasTransferencia.Planejar(cliente, filtro, destino ?? Maria, Transferencia, Tipos, Guid.NewGuid);

    // ---- Pedido ----

    [Fact]
    public void Pedido_exige_origem_destino_diferente_efeito_e_motivo()
    {
        var erros = RegrasTransferencia.ValidarPedido(new FiltroTransferencia(Guid.Empty, null, null, default), [], null, null, Hoje, 30);
        Assert.Contains(erros, e => e.Contains("origem"));
        Assert.Contains(erros, e => e.Contains("destino"));
        Assert.Contains(erros, e => e.Contains("data de efeito"));
        Assert.Contains(erros, e => e.Contains("motivo"));

        Assert.Contains(RegrasTransferencia.ValidarPedido(Filtro(), [Joao], "Desligamento", null, Hoje, 30), e => e.Contains("própria origem"));
        Assert.Contains(RegrasTransferencia.ValidarPedido(Filtro(), [Maria, Maria], "Desligamento", null, Hoje, 30), e => e.Contains("duas vezes"));
        Assert.Empty(RegrasTransferencia.ValidarPedido(Filtro(), [Maria, Pedro], "Desligamento", null, Hoje, 30));
    }

    [Fact]
    public void Efeito_no_passado_so_ate_o_limite_dos_parametros_e_zero_so_de_hoje_em_diante()
    {
        Assert.Empty(RegrasTransferencia.ValidarPedido(Filtro(efeito: Hoje.AddDays(-30)), [Maria], "Saiu", null, Hoje, 30));
        Assert.Contains(RegrasTransferencia.ValidarPedido(Filtro(efeito: Hoje.AddDays(-31)), [Maria], "Saiu", null, Hoje, 30),
            e => e.Contains("no máximo 30 dia(s)"));
        Assert.Contains(RegrasTransferencia.ValidarPedido(Filtro(efeito: Hoje.AddDays(-1)), [Maria], "Saiu", null, Hoje, 0),
            e => e.Contains("não pode ser anterior a hoje"));
        Assert.Empty(RegrasTransferencia.ValidarPedido(Filtro(efeito: Hoje), [Maria], "Saiu", null, Hoje, 0));

        Assert.NotNull(RegrasTransferencia.AvisoRetroativo(Hoje.AddDays(-8), Hoje));
        Assert.Null(RegrasTransferencia.AvisoRetroativo(Hoje, Hoje));
    }

    [Fact]
    public void Motivo_e_observacao_longos_sao_recusados()
    {
        var longo = new string('x', TransferenciaCarteira.TamanhoMaximoTexto + 1);
        var erros = RegrasTransferencia.ValidarPedido(Filtro(), [Maria], longo, longo, Hoje, 30);
        Assert.Contains(erros, e => e.StartsWith("Motivo"));
        Assert.Contains(erros, e => e.StartsWith("Observação"));
    }

    // ---- Plano e aplicação ----

    [Fact]
    public void Vendedor_passa_para_o_destino_na_data_de_efeito_e_o_anterior_termina_na_vespera_sem_apagar()
    {
        var cliente = Cliente(id => Vinculo(id, Vendedor, Joao, new DateOnly(2025, 1, 1), credito: 100));
        var joao = cliente.Carteira[0];
        var antes = RegrasTransferencia.ComoEstava(cliente);

        var plano = Planejar(cliente, Filtro(Vendedor.Id));
        RegrasTransferencia.Aplicar(cliente, plano);

        var (origem, novo) = Assert.Single(plano.Transferir);
        Assert.Same(joao, origem);
        Assert.Equal(new DateOnly(2026, 9, 30), joao.FimEm);         // véspera do efeito
        Assert.True(joao.Ativo);                                      // encerrado, não desativado
        Assert.Equal(2, cliente.Carteira.Count);
        Assert.Equal(Maria, novo.VendedorId);
        Assert.Equal(Efeito, novo.InicioEm);
        Assert.Null(novo.FimEm);
        Assert.Equal(100, novo.PercentualCredito);                    // herda o crédito
        Assert.Equal(OrigemVinculoCarteira.Transferencia, novo.Origem);
        Assert.Equal(Transferencia, novo.TransferenciaId);

        // As regras da ficha continuam valendo depois da troca (um por vez, histórico, crédito).
        Assert.Empty(RegrasComercial.Validar(cliente, Tipos, antes));
        Assert.Empty(RegrasComercial.ValidarHistorico(cliente, antes, Tipos, Hoje));
        Assert.Empty(RegrasComercial.ValidarCarteira(cliente, antes, Tipos));
        Assert.Null(antes.Carteira[0].FimEm);                         // a cópia "como estava" não mudou
    }

    [Fact]
    public void Divisao_de_credito_70_30_continua_somando_100_depois_da_transferencia()
    {
        var cliente = Cliente(
            id => Vinculo(id, Vendedor, Joao, new DateOnly(2025, 1, 1), credito: 70),
            id => Vinculo(id, Representante, Pedro, new DateOnly(2025, 1, 1), credito: 30));
        var antes = RegrasTransferencia.ComoEstava(cliente);

        RegrasTransferencia.Aplicar(cliente, Planejar(cliente, Filtro()));

        Assert.Empty(RegrasComercial.ValidarCarteira(cliente, antes, Tipos));
        var creditos = RegrasComercial.CreditosEmData(cliente.Carteira, Tipos, null, Efeito);
        Assert.Equal(70, creditos.Single(c => c.Vinculo.VendedorId == Maria).Percentual);
        Assert.Equal(30, creditos.Single(c => c.Vinculo.VendedorId == Pedro).Percentual);
        Assert.Equal(Joao, RegrasComercial.CreditosEmData(cliente.Carteira, Tipos, null, Efeito.AddDays(-1))
            .Single(c => c.Vinculo.TipoCarteiraId == Vendedor.Id).Vinculo.VendedorId);
    }

    [Fact]
    public void Fim_planejado_da_origem_passa_para_o_destino()
    {
        var cliente = Cliente(id => Vinculo(id, Vendedor, Joao, new DateOnly(2026, 1, 1), fim: new DateOnly(2026, 12, 30)));
        var novo = Assert.Single(Planejar(cliente, Filtro()).Transferir).Novo;
        Assert.Equal(new DateOnly(2026, 12, 30), novo.FimEm);
    }

    [Fact]
    public void Vinculo_que_comeca_no_efeito_ou_depois_fica_como_esta_com_o_motivo()
    {
        var cliente = Cliente(
            id => Vinculo(id, Vendedor, Joao, Efeito),
            id => Vinculo(id, Supervisor, Joao, Efeito.AddDays(10)));

        var plano = Planejar(cliente, Filtro());

        Assert.Empty(plano.Transferir);
        Assert.Equal(2, plano.Manter.Count);
        Assert.All(plano.Manter, m => Assert.Contains("fica como está", m.Motivo));
    }

    [Fact]
    public void Filtro_de_papel_e_empresa_e_vinculos_ja_encerrados_ficam_de_fora()
    {
        var empresaA = Guid.NewGuid();
        var cliente = Cliente(
            id => Vinculo(id, Vendedor, Joao, new DateOnly(2025, 1, 1), empresa: empresaA),
            id => Vinculo(id, Supervisor, Joao, new DateOnly(2025, 1, 1)),
            id => Vinculo(id, Vendedor, Joao, new DateOnly(2024, 1, 1), fim: new DateOnly(2024, 12, 31)),
            id => Vinculo(id, Vendedor, Pedro, new DateOnly(2025, 1, 1), empresa: Guid.NewGuid()));

        Assert.Single(RegrasTransferencia.Candidatos(cliente.Carteira, Filtro(Vendedor.Id, empresaA)));
        Assert.Equal(2, RegrasTransferencia.Candidatos(cliente.Carteira, Filtro()).Count()); // o encerrado e o do Pedro não
        var desativado = cliente.Carteira[1];
        desativado.Ativo = false;                                                          // lançado por engano
        Assert.Single(RegrasTransferencia.Candidatos(cliente.Carteira, Filtro()));
    }

    [Fact]
    public void Destino_que_ja_atende_o_cliente_no_papel_fica_de_fora()
    {
        var cliente = Cliente(
            id => Vinculo(id, Representante, Joao, new DateOnly(2025, 1, 1)),
            id => Vinculo(id, Representante, Maria, new DateOnly(2025, 6, 1)));

        var plano = Planejar(cliente, Filtro());

        Assert.Empty(plano.Transferir);
        Assert.Contains("já atende", Assert.Single(plano.Manter).Motivo);
    }

    [Fact]
    public void Destino_que_atendeu_antes_e_ja_saiu_nao_impede()
    {
        var cliente = Cliente(
            id => Vinculo(id, Representante, Maria, new DateOnly(2024, 1, 1), fim: new DateOnly(2024, 12, 31)),
            id => Vinculo(id, Representante, Joao, new DateOnly(2025, 1, 1)));
        Assert.Single(Planejar(cliente, Filtro()).Transferir);
    }

    [Fact]
    public void Vendedor_ja_planejado_de_outra_pessoa_nao_e_sobreposto_porque_o_destino_herda_o_fim()
    {
        // João até 30/11; Pedro já planejado a partir de 01/12. Maria herda o fim de João (30/11) e não sobrepõe o Pedro.
        var cliente = Cliente(
            id => Vinculo(id, Vendedor, Joao, new DateOnly(2025, 1, 1), fim: new DateOnly(2026, 11, 30)),
            id => Vinculo(id, Vendedor, Pedro, new DateOnly(2026, 12, 1)));
        var antes = RegrasTransferencia.ComoEstava(cliente);

        var plano = Planejar(cliente, Filtro());
        Assert.Equal(new DateOnly(2026, 11, 30), Assert.Single(plano.Transferir).Novo.FimEm); // herda o fim: não sobrepõe
        RegrasTransferencia.Aplicar(cliente, plano);
        Assert.Empty(RegrasComercial.Validar(cliente, Tipos, antes));
    }

    // ---- Divisão, frases e contagens ----

    [Fact]
    public void Varios_destinos_dividem_pela_menor_carteira_e_empate_fica_com_o_primeiro()
    {
        var clientes = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var divisao = RegrasTransferencia.DividirPelaMenorCarteira(clientes, [Maria, Pedro], new Dictionary<Guid, int> { [Maria] = 2 });

        // Pedro (0) recebe até empatar com Maria (2); daí alternam começando pela Maria (primeira da lista).
        Assert.Equal(new[] { Pedro, Pedro, Maria, Pedro, Maria }, clientes.Select(c => divisao[c]).ToArray());

        var umSo = RegrasTransferencia.DividirPelaMenorCarteira(clientes, [Maria], new Dictionary<Guid, int>());
        Assert.All(clientes, c => Assert.Equal(Maria, umSo[c]));
    }

    [Fact]
    public void Frase_do_historico_diz_quem_para_quem_quando_numero_e_motivo()
    {
        var cliente = Cliente(id => Vinculo(id, Vendedor, Joao, new DateOnly(2025, 1, 1)));
        var nomes = new Dictionary<Guid, string> { [Joao] = "João", [Maria] = "Maria" };

        var frase = Assert.Single(RegrasTransferencia.Frases(Planejar(cliente, Filtro()), Tipos, id => nomes[id], "TR-2026-0003", "desligamento"));

        Assert.Equal("Carteira: João (Vendedor) transferido para Maria a partir de 01/10/2026 (TR-2026-0003: desligamento).", frase);
        Assert.Equal("TR-2026-0003", RegrasTransferencia.Numero(2026, 3));
    }

    [Fact]
    public void Contagem_e_por_cliente_transferido_se_algum_vinculo_passou()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        TransferenciaCarteiraItem Item(Guid cliente, ResultadoItemTransferencia r) => new() { ClienteId = cliente, Resultado = r };

        var (transferidos, naoProcessados, erros) = RegrasTransferencia.Contar(
        [
            Item(a, ResultadoItemTransferencia.Transferido), Item(a, ResultadoItemTransferencia.NaoProcessado),
            Item(b, ResultadoItemTransferencia.NaoProcessado),
            Item(c, ResultadoItemTransferencia.Erro)
        ]);

        Assert.Equal((1, 1, 1), (transferidos, naoProcessados, erros));
    }

    // ---- Parâmetro e cobertura no passado (T6) ----

    [Fact]
    public void Cobertura_nova_so_comeca_ate_o_limite_de_dias_no_passado()
    {
        CoberturaComercial Nova(DateOnly inicio) => new()
        {
            Id = Guid.NewGuid(), TitularId = Joao, SubstitutoId = Maria, TipoAusenciaId = Guid.NewGuid(), InicioEm = inicio, FimEm = Hoje.AddDays(10)
        };

        Assert.Empty(RegrasCobertura.Validar(Nova(Hoje.AddDays(-30)), null, [], Hoje, 30));
        Assert.Contains(RegrasCobertura.Validar(Nova(Hoje.AddDays(-31)), null, [], Hoje, 30), e => e.Contains("no máximo 30 dia(s)"));
        Assert.Empty(RegrasCobertura.Validar(Nova(Hoje.AddDays(-31)), null, [], Hoje));   // sem limite informado: como antes

        // A gravada que começou há muito tempo continua podendo mudar o fim (o início não mudou).
        var gravada = Nova(Hoje.AddDays(-60));
        var prorrogada = Nova(Hoje.AddDays(-60));
        prorrogada.Id = gravada.Id;
        prorrogada.TipoAusenciaId = gravada.TipoAusenciaId;
        prorrogada.FimEm = Hoje.AddDays(20);
        Assert.Empty(RegrasCobertura.Validar(prorrogada, gravada, [gravada], Hoje, 30));
    }

    [Fact]
    public void Parametro_de_dias_no_passado_vai_de_0_a_365()
    {
        Assert.Empty(RegrasParametrosComerciais.Validar(new ParametrosComerciais { DiasRetroativosMaximo = 0 }));
        Assert.Contains(RegrasParametrosComerciais.Validar(new ParametrosComerciais { DiasRetroativosMaximo = 366 }), e => e.Contains("Datas no passado"));
        Assert.Equal(30, new ParametrosComerciais().DiasRetroativosMaximo);
    }

    [Fact]
    public void Ficha_mantem_a_transferencia_do_vinculo_gravado_e_o_novo_nasce_sem()
    {
        var gravado = Vinculo(Guid.NewGuid(), Vendedor, Maria, Efeito);
        gravado.Origem = OrigemVinculoCarteira.Transferencia;
        gravado.TransferenciaId = Transferencia;
        var anterior = new Pessoa { Id = gravado.PessoaId, Carteira = [gravado] };

        var daFicha = RegrasTransferencia.Copiar(gravado);
        daFicha.TransferenciaId = null;             // a ficha não envia
        daFicha.Origem = OrigemVinculoCarteira.Manual;
        var novo = Vinculo(gravado.PessoaId, Supervisor, Pedro, Efeito);
        novo.TransferenciaId = Guid.NewGuid();      // nem aceita
        var atual = new Pessoa { Id = gravado.PessoaId, Carteira = [daFicha, novo] };

        RegrasComercial.DefinirOrigens(atual, anterior, Tipos);

        Assert.Equal(Transferencia, daFicha.TransferenciaId);
        Assert.Equal(OrigemVinculoCarteira.Transferencia, daFicha.Origem);
        Assert.Null(novo.TransferenciaId);
        Assert.Equal(OrigemVinculoCarteira.Manual, novo.Origem);
    }
}
