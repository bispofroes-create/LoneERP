using System.Globalization;
using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

public class ComercialTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 25);
    private static readonly TipoCarteira Vendedor = new()
    {
        Id = Guid.NewGuid(), Nome = "Vendedor", ResponsavelDaConta = true, LimitePorVez = 1, TipoCredito = TipoCreditoComercial.Receita
    };
    private static readonly TipoCarteira Televendas = new() { Id = Guid.NewGuid(), Nome = "Televendas" };
    private static Dictionary<Guid, TipoCarteira> Tipos => new[] { Vendedor, Televendas }.ToDictionary(t => t.Id);

    [Theory]
    [InlineData("30, 60 ,90", "30/60/90")]
    [InlineData("0", "0")]
    [InlineData("30 60", "30/60")]
    [InlineData("30/sessenta", null)]
    public void Parcelas_sao_normalizadas(string digitado, string? esperado) =>
        Assert.Equal(esperado, RegrasComercial.NormalizarParcelas(digitado));

    [Fact]
    public void Parcelas_decrescentes_sao_recusadas_e_prazo_medio_e_calculado()
    {
        var c = new CondicaoPagamento { Nome = "Teste" };
        Assert.Contains(RegrasComercial.Validar(c, "60/30"), e => e.Contains("crescentes"));
        Assert.Empty(RegrasComercial.Validar(c, "30/60/90"));
        Assert.Equal(60m, RegrasComercial.PrazoMedio("30/60/90"));
    }

    [Fact]
    public void Excecao_vigente_vale_acima_do_perfil_que_vale_acima_da_conta()
    {
        var conta = new ContaCliente { LimiteCredito = 1000, DescontoMaximo = 5, DiasMaximoAtraso = 10 };
        var perfil = new PerfilComercial { DescontoMaximo = 8, LimiteCredito = 5000 };
        var excecao = new ExcecaoComercial { InicioEm = Hoje.AddDays(-5), FimEm = Hoje.AddDays(5), DescontoMaximo = 15 };
        var vencida = new ExcecaoComercial { InicioEm = Hoje.AddDays(-60), FimEm = Hoje.AddDays(-30), LimiteCredito = 99 };

        var v = RegrasComercial.Efetivos(conta, perfil, [excecao, vencida], Hoje);

        Assert.Equal(15, v.DescontoMaximo);   // exceção
        Assert.Equal(5000, v.LimiteCredito);  // perfil
        Assert.Equal(10, v.DiasMaximoAtraso); // conta
        Assert.True(v.ExigeAprovacaoAcimaLimite);
    }

    [Fact]
    public void Dois_vendedores_do_papel_de_um_por_vez_sao_recusados_mas_televendas_pode()
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Cliente" };
        p.Carteira.Add(new CarteiraCliente { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Guid.NewGuid(), InicioEm = Hoje.AddDays(-10) });
        p.Carteira.Add(new CarteiraCliente { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Guid.NewGuid(), InicioEm = Hoje });
        p.Carteira.Add(new CarteiraCliente { Id = Guid.NewGuid(), TipoCarteiraId = Televendas.Id, VendedorId = Guid.NewGuid(), InicioEm = Hoje });
        p.Carteira.Add(new CarteiraCliente { Id = Guid.NewGuid(), TipoCarteiraId = Televendas.Id, VendedorId = Guid.NewGuid(), InicioEm = Hoje });

        var erros = RegrasComercial.Validar(p, Tipos);

        Assert.Single(erros);
        Assert.Contains("só um \"Vendedor\" por vez", erros[0]);
    }

    [Fact]
    public void Exclusivo_nao_aceita_outro_do_mesmo_tipo_no_periodo()
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Cliente" };
        p.Carteira.Add(new CarteiraCliente { Id = Guid.NewGuid(), TipoCarteiraId = Televendas.Id, VendedorId = Guid.NewGuid(), InicioEm = Hoje, Exclusivo = true });
        p.Carteira.Add(new CarteiraCliente { Id = Guid.NewGuid(), TipoCarteiraId = Televendas.Id, VendedorId = Guid.NewGuid(), InicioEm = Hoje.AddDays(3) });

        Assert.Contains(RegrasComercial.Validar(p, Tipos), e => e.Contains("exclusivo"));
    }

    [Fact]
    public void Vendedor_vigente_do_responsavel_da_conta_vira_o_vendedor_padrao()
    {
        var antigo = Guid.NewGuid();
        var atual = Guid.NewGuid();
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Cliente" };
        var conta = new ContaCliente { VendedorPadraoId = antigo };
        p.ContasCliente.Add(conta);
        p.Carteira.Add(new CarteiraCliente { TipoCarteiraId = Vendedor.Id, VendedorId = antigo, InicioEm = Hoje.AddDays(-100), FimEm = Hoje.AddDays(-1) });
        p.Carteira.Add(new CarteiraCliente { TipoCarteiraId = Vendedor.Id, VendedorId = atual, InicioEm = Hoje });
        p.Carteira.Add(new CarteiraCliente { TipoCarteiraId = Televendas.Id, VendedorId = Guid.NewGuid(), InicioEm = Hoje });

        RegrasComercial.AtualizarVendedorPadrao(p, Tipos, Hoje);

        Assert.Equal(atual, conta.VendedorPadraoId);
    }

    [Fact]
    public void Excecao_precisa_de_algum_valor_e_nao_se_sobrepoe()
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Cliente" };
        p.ExcecoesComerciais.Add(new ExcecaoComercial { Id = Guid.NewGuid(), InicioEm = Hoje });
        p.ExcecoesComerciais.Add(new ExcecaoComercial { Id = Guid.NewGuid(), InicioEm = Hoje.AddDays(2), DescontoMaximo = 10 });

        var erros = RegrasComercial.Validar(p, Tipos);

        Assert.Contains(erros, e => e.Contains("ao menos um valor"));
        Assert.Contains(erros, e => e.Contains("sobrepostos"));
    }
    // ---- Substituição de vendedor (Etapa 4, decisão D3) ----

    private static CarteiraCliente Vinculo(TipoCarteira tipo, DateOnly inicio, DateOnly? fim = null, Guid? empresa = null, bool exclusivo = false) =>
        new() { Id = Guid.NewGuid(), TipoCarteiraId = tipo.Id, VendedorId = Guid.NewGuid(), InicioEm = inicio, FimEm = fim, EmpresaId = empresa, Exclusivo = exclusivo };

    [Fact]
    public void Conflito_so_no_mesmo_papel_e_empresa_com_periodo_sobreposto_e_um_por_vez_ou_exclusivo()
    {
        var joao = Vinculo(Vendedor, Hoje.AddDays(-100));
        Assert.True(RegrasComercial.Conflitam(joao, Vinculo(Vendedor, Hoje), Tipos));
        Assert.False(RegrasComercial.Conflitam(joao, Vinculo(Televendas, Hoje), Tipos));                 // outro tipo
        Assert.False(RegrasComercial.Conflitam(joao, Vinculo(Vendedor, Hoje, empresa: Guid.NewGuid()), Tipos)); // outra empresa
        Assert.False(RegrasComercial.Conflitam(Vinculo(Vendedor, Hoje.AddDays(-100), Hoje.AddDays(-1)), Vinculo(Vendedor, Hoje), Tipos));
        Assert.False(RegrasComercial.Conflitam(Vinculo(Televendas, Hoje), Vinculo(Televendas, Hoje), Tipos));   // vários televendas podem
        Assert.True(RegrasComercial.Conflitam(Vinculo(Televendas, Hoje, exclusivo: true), Vinculo(Televendas, Hoje), Tipos));
        var desativado = Vinculo(Vendedor, Hoje.AddDays(-100));
        desativado.Ativo = false;                                                                           // lançado por engano
        Assert.False(RegrasComercial.Conflitam(desativado, Vinculo(Vendedor, Hoje), Tipos));
    }

    [Fact]
    public void Substituir_encerra_o_anterior_na_vespera_sem_apagar_e_a_validacao_passa()
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Cliente" };
        var joao = Vinculo(Vendedor, new DateOnly(2026, 1, 1));
        p.Carteira.Add(joao);
        var maria = Vinculo(Vendedor, new DateOnly(2026, 3, 15));

        var plano = RegrasComercial.PlanejarSubstituicao(p.Carteira, maria, Tipos);
        Assert.Equal([joao], plano.Encerrar);
        Assert.False(plano.Impedida);

        RegrasComercial.Substituir(plano, maria);
        p.Carteira.Add(maria);

        Assert.Equal(new DateOnly(2026, 3, 14), joao.FimEm);
        Assert.True(joao.Ativo);                          // continua no histórico, com o período em que valeu
        Assert.Equal(new DateOnly(2026, 1, 1), joao.InicioEm);
        Assert.Empty(RegrasComercial.Validar(p, Tipos));
    }

    [Fact]
    public void Novo_que_comeca_no_mesmo_dia_ou_antes_do_vigente_nao_substitui()
    {
        var joao = Vinculo(Vendedor, Hoje);
        var mesmoDia = RegrasComercial.PlanejarSubstituicao([joao], Vinculo(Vendedor, Hoje), Tipos);
        var antes = RegrasComercial.PlanejarSubstituicao([joao], Vinculo(Vendedor, Hoje.AddDays(-10)), Tipos);

        Assert.True(mesmoDia.Impedida);
        Assert.True(antes.Impedida);
        Assert.Throws<InvalidOperationException>(() => RegrasComercial.Substituir(antes, Vinculo(Vendedor, Hoje.AddDays(-10))));
        Assert.Null(joao.FimEm); // nada foi mexido
    }

    [Fact]
    public void Sem_conflito_o_plano_fica_vazio()
    {
        var plano = RegrasComercial.PlanejarSubstituicao([Vinculo(Televendas, Hoje)], Vinculo(Vendedor, Hoje), Tipos);
        Assert.False(plano.TemConflito);
    }

    [Fact]
    public void Substituicao_vira_frase_no_historico_so_quando_o_anterior_foi_encerrado_na_vespera_do_novo()
    {
        var joaoGravado = Vinculo(Vendedor, new DateOnly(2026, 1, 1));
        var joaoAgora = new CarteiraCliente
        {
            Id = joaoGravado.Id, TipoCarteiraId = Vendedor.Id, VendedorId = joaoGravado.VendedorId,
            InicioEm = joaoGravado.InicioEm, FimEm = new DateOnly(2026, 3, 14)
        };
        var maria = Vinculo(Vendedor, new DateOnly(2026, 3, 15));
        var nomes = new Dictionary<Guid, string> { [joaoGravado.VendedorId] = "João da Silva", [maria.VendedorId] = "Maria Oliveira" };

        var frases = RegrasComercial.Substituicoes([joaoGravado], [joaoAgora, maria], Tipos, id => nomes[id]).ToList();

        Assert.Equal("Carteira: João da Silva (Vendedor) encerrado em 14/03/2026 e substituído por Maria Oliveira a partir de 15/03/2026.",
            Assert.Single(frases));
        // Encerrar sem incluir outro (ou incluir sem encerrar) não é substituição.
        Assert.Empty(RegrasComercial.Substituicoes([joaoGravado], [joaoAgora], Tipos, id => nomes[id]));
        Assert.Empty(RegrasComercial.Substituicoes([joaoGravado], [joaoGravado, maria], Tipos, id => nomes[id]));
    }

    // ---- Motor Comercial, Fase 1a: política dos papéis e crédito ----

    private static readonly TipoCarteira Representante = new()
    {
        Id = Guid.NewGuid(), Nome = "Representante", LimitePorVez = 2, TipoCredito = TipoCreditoComercial.Receita
    };
    private static readonly TipoCarteira Supervisor = new()
    {
        Id = Guid.NewGuid(), Nome = "Supervisor", TipoCredito = TipoCreditoComercial.Sobreposicao, PercentualPadrao = 5
    };
    private static Dictionary<Guid, TipoCarteira> Papeis => new[] { Vendedor, Televendas, Representante, Supervisor }.ToDictionary(t => t.Id);

    private static Pessoa Cliente(params CarteiraCliente[] vinculos)
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Cliente" };
        p.Carteira.AddRange(vinculos);
        return p;
    }

    private static CarteiraCliente Copia(CarteiraCliente c) => new()
    {
        Id = c.Id, PessoaId = c.PessoaId, TipoCarteiraId = c.TipoCarteiraId, VendedorId = c.VendedorId, EmpresaId = c.EmpresaId,
        InicioEm = c.InicioEm, FimEm = c.FimEm, Exclusivo = c.Exclusivo, PercentualCredito = c.PercentualCredito, Ativo = c.Ativo,
        Origem = c.Origem
    };

    private static CarteiraCliente ComCredito(CarteiraCliente c, decimal? pct)
    {
        c.PercentualCredito = pct;
        return c;
    }

    [Fact]
    public void Papel_com_limite_dois_recusa_o_terceiro_ao_mesmo_tempo_mas_aceita_depois_que_um_encerra()
    {
        var a = Vinculo(Representante, Hoje.AddDays(-30));
        var b = Vinculo(Representante, Hoje.AddDays(-20));
        var terceiro = Vinculo(Representante, Hoje);

        var erros = RegrasComercial.ValidarCarteira(Cliente(a, b, terceiro), null, Papeis);
        Assert.Contains(erros, e => e.Contains("no máximo 2 \"Representante\"") && e.Contains("seriam 3"));
        Assert.Empty(RegrasComercial.Validar(Cliente(a, b, terceiro), Papeis)); // limite acima de 1 não é conflito par a par

        a.FimEm = Hoje.AddDays(-1);
        Assert.DoesNotContain(RegrasComercial.ValidarCarteira(Cliente(a, b, terceiro), null, Papeis), e => e.Contains("no máximo"));
    }

    [Fact]
    public void Credito_de_receita_com_dois_vinculos_precisa_de_percentual_e_soma_100()
    {
        var vendedor = Vinculo(Vendedor, Hoje.AddDays(-30));
        var representante = Vinculo(Representante, Hoje);

        Assert.Contains(RegrasComercial.ValidarCarteira(Cliente(vendedor, representante), null, Papeis),
            e => e.Contains("informe o crédito (%) de cada um"));

        ComCredito(vendedor, 70);
        ComCredito(representante, 20);
        Assert.Contains(RegrasComercial.ValidarCarteira(Cliente(vendedor, representante), null, Papeis),
            e => e.Contains("soma 90%") && e.Contains(Hoje.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));

        ComCredito(representante, 30);
        Assert.Empty(RegrasComercial.ValidarCarteira(Cliente(vendedor, representante), null, Papeis));

        // Sozinho, fica com 100% (o % gravado não precisa ser 100).
        Assert.Empty(RegrasComercial.ValidarCarteira(Cliente(ComCredito(Vinculo(Vendedor, Hoje), 40)), null, Papeis));
    }

    [Fact]
    public void Encerrar_um_dos_tres_de_receita_obriga_rever_a_divisao_dos_que_ficam()
    {
        var joao = ComCredito(Vinculo(Vendedor, Hoje.AddDays(-60)), 50);
        var ana = ComCredito(Vinculo(Representante, Hoje.AddDays(-60)), 30);
        var rui = ComCredito(Vinculo(Representante, Hoje.AddDays(-60)), 20);
        var anterior = Cliente(Copia(joao), Copia(ana), Copia(rui));
        rui.FimEm = Hoje; // os outros continuam com 80% a partir de amanhã

        var erros = RegrasComercial.ValidarCarteira(Cliente(joao, ana, rui), anterior, Papeis);

        Assert.Contains(erros, e => e.Contains("soma 80%") && e.Contains(Hoje.AddDays(1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Politica_nova_do_papel_nao_trava_a_ficha_de_quem_nao_mexe_na_carteira()
    {
        // Três representantes de antes do limite 2 (e sem %): gravar a ficha sem mexer na carteira passa.
        var gravados = new[] { Vinculo(Representante, Hoje.AddDays(-90)), Vinculo(Representante, Hoje.AddDays(-80)), Vinculo(Representante, Hoje.AddDays(-70)) };
        var anterior = Cliente([.. gravados.Select(Copia)]);
        var atual = Cliente([.. gravados.Select(Copia)]);
        atual.Carteira[0].Observacao = "só a observação mudou";

        Assert.Empty(RegrasComercial.ValidarCarteira(atual, anterior, Papeis));

        // Incluir mais um no mesmo período é conferido.
        atual.Carteira.Add(Vinculo(Representante, Hoje));
        Assert.NotEmpty(RegrasComercial.ValidarCarteira(atual, anterior, Papeis));
    }

    [Fact]
    public void Papel_sem_credito_nao_aceita_percentual_e_percentual_fica_entre_0_e_100()
    {
        var erros = RegrasComercial.ValidarCarteira(
            Cliente(ComCredito(Vinculo(Televendas, Hoje), 10), ComCredito(Vinculo(Vendedor, Hoje), 101)), null, Papeis);

        Assert.Contains(erros, e => e.Contains("\"Televendas\" não recebe crédito"));
        Assert.Contains(erros, e => e.Contains("entre 0% e 100%"));
    }

    [Fact]
    public void Credito_numa_data_prefere_a_empresa_e_soma_a_sobreposicao()
    {
        var empresa = Guid.NewGuid();
        var geral = Vinculo(Vendedor, Hoje.AddDays(-100));
        var daEmpresa = ComCredito(Vinculo(Representante, Hoje.AddDays(-10), empresa: empresa), 60);
        var outroDaEmpresa = ComCredito(Vinculo(Representante, Hoje.AddDays(-10), empresa: empresa), 40);
        var supervisor = Vinculo(Supervisor, Hoje.AddDays(-100));
        var apoio = Vinculo(Televendas, Hoje.AddDays(-100));
        var carteira = new[] { geral, daEmpresa, outroDaEmpresa, supervisor, apoio };

        var naEmpresa = RegrasComercial.CreditosEmData(carteira, Papeis, empresa, Hoje);
        Assert.Equal([(daEmpresa, 60m), (outroDaEmpresa, 40m)],
            naEmpresa.Where(c => c.Tipo == TipoCreditoComercial.Receita).Select(c => (c.Vinculo, c.Percentual!.Value)));
        Assert.Equal(5m, Assert.Single(naEmpresa, c => c.Tipo == TipoCreditoComercial.Sobreposicao).Percentual);
        Assert.DoesNotContain(naEmpresa, c => c.Vinculo == apoio);

        var emOutra = RegrasComercial.CreditosEmData(carteira, Papeis, Guid.NewGuid(), Hoje);
        Assert.Equal(100m, Assert.Single(emOutra, c => c.Tipo == TipoCreditoComercial.Receita && c.Vinculo == geral).Percentual);

        var antes = RegrasComercial.CreditosEmData(carteira, Papeis, empresa, Hoje.AddDays(-50)); // representantes ainda não
        Assert.Equal(geral, Assert.Single(antes, c => c.Tipo == TipoCreditoComercial.Receita).Vinculo);
    }

    [Fact]
    public void Credito_sem_percentual_definido_fica_indefinido()
    {
        var carteira = new[] { Vinculo(Vendedor, Hoje), Vinculo(Representante, Hoje) };
        Assert.All(RegrasComercial.CreditosEmData(carteira, Papeis, null, Hoje), c => Assert.Null(c.Percentual));
    }

    [Fact]
    public void Cadastro_do_papel_confere_responsavel_limite_e_percentual()
    {
        var todos = new List<TipoCarteira> { Vendedor, Televendas };

        var outroResponsavel = new TipoCarteira { Id = Guid.NewGuid(), Nome = "Gerente", ResponsavelDaConta = true, LimitePorVez = 1 };
        Assert.Contains(RegrasComercial.ValidarPapel(outroResponsavel, todos), e => e.Contains("já é o responsável da conta"));

        var semLimite = new TipoCarteira { Id = Vendedor.Id, Nome = "Vendedor", ResponsavelDaConta = true };
        Assert.Contains(RegrasComercial.ValidarPapel(semLimite, todos), e => e.Contains("precisa ser um por vez"));

        var semCredito = new TipoCarteira { Id = Guid.NewGuid(), Nome = "Apoio", PercentualPadrao = 10 };
        Assert.Contains(RegrasComercial.ValidarPapel(semCredito, todos), e => e.Contains("não recebe crédito"));

        var limiteZero = new TipoCarteira { Id = Guid.NewGuid(), Nome = "Especialista", LimitePorVez = 0 };
        Assert.Contains(RegrasComercial.ValidarPapel(limiteZero, todos), e => e.Contains("de 1 a 99"));

        var repetido = new TipoCarteira { Id = Guid.NewGuid(), Nome = "televendas" };
        Assert.Contains(RegrasComercial.ValidarPapel(repetido, todos), e => e.Contains("Já existe"));

        Assert.Empty(RegrasComercial.ValidarPapel(
            new TipoCarteira { Id = Guid.NewGuid(), Nome = "Key account", LimitePorVez = 3, TipoCredito = TipoCreditoComercial.Receita, PercentualPadrao = 30 }, todos));
    }

    [Fact]
    public void Baixar_o_limite_conta_os_clientes_que_ficariam_acima_de_hoje_em_diante()
    {
        var cliente1 = Guid.NewGuid();
        var cliente2 = Guid.NewGuid();
        CarteiraCliente De(Guid pessoa, DateOnly inicio, DateOnly? fim = null)
        {
            var v = Vinculo(Representante, inicio, fim);
            v.PessoaId = pessoa;
            return v;
        }
        var vinculos = new[]
        {
            De(cliente1, Hoje.AddDays(-10)), De(cliente1, Hoje.AddDays(5)),           // dois a partir de daqui a 5 dias
            De(cliente2, Hoje.AddDays(-90), Hoje.AddDays(-1)), De(cliente2, Hoje)      // um de cada vez
        };

        Assert.Equal(1, RegrasComercial.ClientesAcimaDoLimite(vinculos, 1, Hoje));
        Assert.Equal(0, RegrasComercial.ClientesAcimaDoLimite(vinculos, 2, Hoje));
    }

    [Fact]
    public void Origem_e_do_servidor_gravado_mantem_novo_e_manual_e_substituto_e_substituicao()
    {
        var joaoGravado = Vinculo(Vendedor, new DateOnly(2026, 1, 1));
        joaoGravado.Origem = OrigemVinculoCarteira.Importacao;
        var anterior = Cliente(joaoGravado);

        var joaoAgora = Copia(joaoGravado);
        joaoAgora.Origem = OrigemVinculoCarteira.Manual;   // o que a ficha mandou não vale
        joaoAgora.FimEm = new DateOnly(2026, 3, 14);
        var maria = Vinculo(Vendedor, new DateOnly(2026, 3, 15));
        maria.Origem = OrigemVinculoCarteira.Transferencia;
        var televendas = Vinculo(Televendas, new DateOnly(2026, 3, 15));
        var atual = Cliente(joaoAgora, maria, televendas);

        RegrasComercial.DefinirOrigens(atual, anterior, Papeis);

        Assert.Equal(OrigemVinculoCarteira.Importacao, joaoAgora.Origem);
        Assert.Equal(OrigemVinculoCarteira.Substituicao, maria.Origem);
        Assert.Equal(OrigemVinculoCarteira.Manual, televendas.Origem);
    }

    [Fact]
    public void Sobreposicao_antiga_nao_trava_quem_encurta_mas_o_que_cresce_e_conferido()
    {
        // Dois "Vendedor" sobrepostos de antes do papel virar um por vez (gravados assim).
        var joao = Vinculo(Vendedor, Hoje.AddDays(-90));
        var maria = Vinculo(Vendedor, Hoje.AddDays(-60));
        var anterior = Cliente(Copia(joao), Copia(maria));

        joao.FimEm = Hoje.AddDays(-1); // encurtar não acrescenta nada
        Assert.Empty(RegrasComercial.Validar(Cliente(joao, maria), Papeis, anterior));
        Assert.NotEmpty(RegrasComercial.Validar(Cliente(joao, maria), Papeis)); // sem o anterior, tudo é novo

        var antecipada = Copia(maria);
        antecipada.InicioEm = Hoje.AddDays(-100); // cresceu para trás, sobre o período do João
        Assert.Contains(RegrasComercial.Validar(Cliente(Copia(joao), antecipada), Papeis, anterior), e => e.Contains("por vez"));
    }

    [Fact]
    public void Crescimentos_sao_so_os_trechos_acrescentados()
    {
        var gravado = Vinculo(Vendedor, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        var atual = Copia(gravado);
        atual.InicioEm = new DateOnly(2026, 2, 20);
        atual.FimEm = new DateOnly(2026, 4, 10);

        Assert.Equal([(new DateOnly(2026, 2, 20), (DateOnly?)new DateOnly(2026, 2, 28)), (new DateOnly(2026, 4, 1), (DateOnly?)new DateOnly(2026, 4, 10))],
            RegrasComercial.Crescimentos(atual, gravado));

        var encurtado = Copia(gravado);
        encurtado.FimEm = new DateOnly(2026, 3, 10);
        Assert.Empty(RegrasComercial.Crescimentos(encurtado, gravado));

        var outroPapel = Copia(gravado);
        outroPapel.TipoCarteiraId = Televendas.Id;
        Assert.Equal([(gravado.InicioEm, gravado.FimEm)], RegrasComercial.Crescimentos(outroPapel, gravado));
        Assert.Equal([(gravado.InicioEm, gravado.FimEm)], RegrasComercial.Crescimentos(atual: Copia(gravado), gravado: null));
    }
}
