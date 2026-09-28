using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comercial;
using Lone.Contracts.Pessoas;

namespace Lone.Tests.Cliente;

public class ComercialFormularioTests
{
    private static readonly TipoCarteiraDto Vendedor = new()
    {
        Id = Guid.NewGuid(), Nome = "Vendedor", ResponsavelDaConta = true, LimitePorVez = 1,
        TipoCredito = Lone.Domain.Enums.TipoCreditoComercial.Receita, Ordem = 1
    };
    private static readonly PerfilComercialDto Atacado = new() { Id = Guid.NewGuid(), Nome = "Atacado", DescontoMaximo = 12 };
    private static readonly CondicaoPagamentoDto Trinta = new() { Id = Guid.NewGuid(), Nome = "30 dias", Parcelas = "30" };

    private static ComercialOpcoesDto Opcoes() => new()
    {
        Perfis = [Atacado],
        Condicoes = [Trinta],
        TiposCarteira = [Vendedor],
        Vendedores = [new PessoaOpcaoDto(Guid.NewGuid(), "João Vendedor")]
    };

    [Fact]
    public void Carteira_nova_ja_vem_com_o_papel_responsavel_e_gravada_so_desativa()
    {
        var gravada = new CarteiraDto { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Guid.NewGuid(), Vendedor = "Ana", InicioEm = new DateOnly(2025, 1, 1) };
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Cliente", Carteira = [gravada] });
        f.DefinirOpcoesComercial(Opcoes());

        f.NovaCarteira();
        Assert.Equal(Vendedor.Id, f.Carteira[0].Tipo.Valor);

        var antiga = f.Carteira[1];
        antiga.RemoverCommand.Execute(null);
        Assert.Equal(2, f.Carteira.Count);
        Assert.False(antiga.ParaDto().Ativo);
        Assert.Contains(antiga.Vendedores, v => v.Texto.Contains("sem o papel"));
    }

    [Fact]
    public void Perfil_e_condicao_vao_no_envio_e_o_resumo_mostra_o_desconto_do_perfil()
    {
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Cliente",
            ContasCliente = [new ContaClienteDto { Id = Guid.NewGuid(), PerfilComercialId = Atacado.Id, DescontoMaximo = 5 }]
        });

        // Antes de ler as opções, o perfil gravado volta intacto.
        Assert.Equal(Atacado.Id, f.ParaDto().ContasCliente.Single(c => c.EmpresaId is null).PerfilComercialId);

        f.DefinirOpcoesComercial(Opcoes());
        f.ContaCliente.Condicao = f.ContaCliente.Condicoes.First(c => c.Valor == Trinta.Id);

        Assert.Equal(Trinta.Id, f.ParaDto().ContasCliente.Single(c => c.EmpresaId is null).CondicaoPagamentoId);
        Assert.Contains("desconto máximo 12%", f.ResumoComercial);
    }

    [Fact]
    public void Excecao_nova_sai_da_lista_e_gravada_fica()
    {
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Cliente",
            ExcecoesComerciais = [new ExcecaoComercialDto { Id = Guid.NewGuid(), InicioEm = new DateOnly(2026, 3, 1), DescontoMaximo = 15 }]
        });
        f.NovaExcecao();
        Assert.Equal(2, f.Excecoes.Count);

        f.Excecoes[0].RemoverCommand.Execute(null);
        f.Excecoes[0].RemoverCommand.Execute(null);

        Assert.Single(f.Excecoes);
        Assert.Equal(15, f.ParaDto().ExcecoesComerciais[0].DescontoMaximo);
    }
    // ---- Substituição de vendedor (Etapa 4, D3) ----

    private static readonly Guid Joao = Guid.NewGuid();
    private static readonly Guid Maria = Guid.NewGuid();

    private static (PessoaFormulario Ficha, CarteiraFormulario Atual, CarteiraFormulario Nova) FichaComVendedor(string inicioDaNova)
    {
        var atual = new CarteiraDto { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Joao, Vendedor = "João da Silva", InicioEm = new DateOnly(2026, 1, 1) };
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Cliente", Carteira = [atual] });
        var opcoes = Opcoes();
        opcoes.Vendedores = [new PessoaOpcaoDto(Joao, "João da Silva"), new PessoaOpcaoDto(Maria, "Maria Oliveira")];
        f.DefinirOpcoesComercial(opcoes);
        f.NovaCarteira();
        var nova = f.Carteira[0];
        nova.Vendedor = nova.Vendedores.First(v => v.Valor == Maria);
        nova.InicioEm = inicioDaNova;
        return (f, f.Carteira[1], nova);
    }

    [Fact]
    public void Vendedor_novo_no_lugar_do_vigente_pede_confirmacao_e_encerra_o_anterior_na_vespera()
    {
        var (f, atual, nova) = FichaComVendedor("15/03/2026");

        var substituicao = Assert.Single(f.SubstituicoesDeVendedor());
        Assert.Same(nova, substituicao.Novo);
        Assert.Same(atual, Assert.Single(substituicao.Encerrar));
        Assert.False(substituicao.Impedida);
        Assert.Equal("Vendedor já atribuído", substituicao.Titulo);
        Assert.Contains("Vendedor atual: João da Silva (desde 01/01/2026)", substituicao.Mensagem);
        Assert.Contains("Novo vendedor: Maria Oliveira (a partir de 15/03/2026)", substituicao.Mensagem);
        Assert.Contains("será encerrado em 14/03/2026", substituicao.Mensagem);
        Assert.Contains("Nenhum cadastro é excluído", substituicao.Mensagem);
        Assert.Equal("Encerrar anterior e atribuir novo vendedor", SubstituicaoVendedor.TextoConfirmar);

        var desfazer = substituicao.Aplicar();

        Assert.Equal("14/03/2026", atual.FimEm);
        Assert.True(atual.Ativo); // continua no histórico
        Assert.Empty(f.SubstituicoesDeVendedor());

        desfazer(); // a gravação falhou: a ficha volta como estava e a pergunta aparece de novo
        Assert.Equal(string.Empty, atual.FimEm);
        Assert.Single(f.SubstituicoesDeVendedor());
    }

    [Fact]
    public void Vendedor_novo_no_mesmo_dia_ou_antes_do_vigente_nao_substitui()
    {
        var (f, atual, _) = FichaComVendedor("01/01/2026");

        var substituicao = Assert.Single(f.SubstituicoesDeVendedor());

        Assert.True(substituicao.Impedida);
        Assert.Contains("o histórico não é alterado", substituicao.Mensagem);
        Assert.Contains("use um papel que aceite mais de um", substituicao.Mensagem);
        Assert.Throws<InvalidOperationException>(() => substituicao.Aplicar());
        Assert.Equal(string.Empty, atual.FimEm);
    }

    [Fact]
    public void Vendedor_de_outro_tipo_ou_ja_encerrado_nao_e_substituicao()
    {
        var (f, atual, nova) = FichaComVendedor("15/03/2026");
        atual.FimEm = "31/01/2026";
        Assert.Empty(f.SubstituicoesDeVendedor());

        atual.FimEm = string.Empty;
        var televendas = new TipoCarteiraDto { Id = Guid.NewGuid(), Nome = "Televendas", Ordem = 2 };
        var opcoes = Opcoes();
        opcoes.TiposCarteira = [Vendedor, televendas];
        opcoes.Vendedores = [new PessoaOpcaoDto(Joao, "João da Silva"), new PessoaOpcaoDto(Maria, "Maria Oliveira")];
        f.DefinirOpcoesComercial(opcoes);
        nova.Tipo = nova.Tipos.First(t => t.Valor == televendas.Id);
        Assert.Empty(f.SubstituicoesDeVendedor());
    }

    // ---- Motor Comercial, Fase 1a: crédito (%) no vínculo ----

    [Fact]
    public void Novo_vendedor_herda_o_credito_do_anterior_e_desfazer_devolve()
    {
        var (f, atual, nova) = FichaComVendedor("15/03/2026");
        atual.PercentualCredito = "70";

        var substituicao = Assert.Single(f.SubstituicoesDeVendedor());
        var desfazer = substituicao.Aplicar();
        Assert.Equal("70", nova.PercentualCredito);
        Assert.Equal(70m, nova.ParaDto().PercentualCredito);

        desfazer();
        Assert.Equal(string.Empty, nova.PercentualCredito);
    }

    [Fact]
    public void Credito_aparece_so_para_papel_que_recebe_e_valida_a_faixa()
    {
        var apoio = new TipoCarteiraDto { Id = Guid.NewGuid(), Nome = "Apoio", Ordem = 2 };
        var opcoes = Opcoes();
        opcoes.TiposCarteira = [Vendedor, apoio];
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Cliente" });
        f.DefinirOpcoesComercial(opcoes);
        f.NovaCarteira();
        var v = f.Carteira[0];

        Assert.True(v.RecebeCredito);
        Assert.Contains("100%", v.DicaCredito);
        v.PercentualCredito = "150";
        Assert.Contains(v.Validar("Carteira 1"), e => e.Contains("crédito inválido"));

        v.PercentualCredito = string.Empty;
        v.Tipo = v.Tipos.First(t => t.Valor == apoio.Id);
        Assert.False(v.RecebeCredito);
        Assert.Null(v.ParaDto().PercentualCredito);
    }
}
