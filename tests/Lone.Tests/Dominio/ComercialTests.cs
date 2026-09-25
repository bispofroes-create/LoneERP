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
}
