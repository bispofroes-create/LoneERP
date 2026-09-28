using Lone.Domain.Comercial;
using Lone.Domain.Entidades;

namespace Lone.Tests.Dominio;

public class ComercialTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 25);
    private static readonly TipoCarteira Vendedor = new() { Id = Guid.NewGuid(), Nome = "Vendedor", Principal = true };
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
    public void Dois_vendedores_principais_ao_mesmo_tempo_sao_recusados_mas_televendas_pode()
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Cliente" };
        p.Carteira.Add(new CarteiraCliente { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Guid.NewGuid(), InicioEm = Hoje.AddDays(-10) });
        p.Carteira.Add(new CarteiraCliente { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Guid.NewGuid(), InicioEm = Hoje });
        p.Carteira.Add(new CarteiraCliente { Id = Guid.NewGuid(), TipoCarteiraId = Televendas.Id, VendedorId = Guid.NewGuid(), InicioEm = Hoje });
        p.Carteira.Add(new CarteiraCliente { Id = Guid.NewGuid(), TipoCarteiraId = Televendas.Id, VendedorId = Guid.NewGuid(), InicioEm = Hoje });

        var erros = RegrasComercial.Validar(p, Tipos);

        Assert.Single(erros);
        Assert.Contains("principal", erros[0]);
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
    public void Vendedor_principal_vigente_vira_o_vendedor_padrao_da_conta()
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
    public void Conflito_so_no_mesmo_tipo_e_empresa_com_periodo_sobreposto_e_tipo_principal_ou_exclusivo()
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
}
