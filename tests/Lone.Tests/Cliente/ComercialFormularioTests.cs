using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comercial;
using Lone.Contracts.Pessoas;

namespace Lone.Tests.Cliente;

public class ComercialFormularioTests
{
    private static readonly Guid ClassVendedor = Guid.NewGuid();
    private static readonly Guid ClassFuncionario = Guid.NewGuid();

    private static readonly TipoCarteiraDto Vendedor = new()
    {
        Id = Guid.NewGuid(), Nome = "Vendedor", ResponsavelDaConta = true, LimitePorVez = 1,
        TipoCredito = Lone.Domain.Enums.TipoCreditoComercial.Receita, Ordem = 1, Classificacoes = [ClassVendedor]
    };
    private static readonly PerfilComercialDto Atacado = new() { Id = Guid.NewGuid(), Nome = "Atacado", DescontoMaximo = 12 };
    private static readonly CondicaoPagamentoDto Trinta = new() { Id = Guid.NewGuid(), Nome = "30 dias", Parcelas = "30" };

    private static ComercialOpcoesDto Opcoes() => new()
    {
        Perfis = [Atacado],
        Condicoes = [Trinta],
        TiposCarteira = [Vendedor],
        Atendentes = [new AtendenteOpcaoDto(Guid.NewGuid(), "João Vendedor", [ClassVendedor])],
        Classificacoes = [new ClassificacaoOpcaoDto(ClassVendedor, "Vendedor", true), new ClassificacaoOpcaoDto(ClassFuncionario, "Funcionário", true)]
    };

    private static List<AtendenteOpcaoDto> JoaoEMaria() =>
        [new AtendenteOpcaoDto(Joao, "João da Silva", [ClassVendedor]), new AtendenteOpcaoDto(Maria, "Maria Oliveira", [ClassVendedor])];

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
        Assert.Contains(antiga.Vendedores, v => v.Texto.Contains("não pode mais ser Vendedor"));
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
        opcoes.Atendentes = JoaoEMaria();
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
        opcoes.Atendentes = JoaoEMaria();
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

    // ---- Motor Comercial, Fase 1b: quem pode ser, histórico travado, Trocar e organização da carteira ----

    [Fact]
    public void Campo_da_pessoa_leva_o_nome_do_papel_e_lista_so_quem_pode_ocupa_lo()
    {
        var supervisor = new TipoCarteiraDto { Id = Guid.NewGuid(), Nome = "Supervisor", Ordem = 2, Classificacoes = [ClassFuncionario] };
        var opcoes = Opcoes();
        opcoes.TiposCarteira = [Vendedor, supervisor];
        var ana = Guid.NewGuid();
        opcoes.Atendentes = [.. JoaoEMaria(), new AtendenteOpcaoDto(ana, "Ana Souza", [ClassFuncionario])];
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Cliente" });
        f.DefinirOpcoesComercial(opcoes);
        f.NovaCarteira();
        var v = f.Carteira[0];

        Assert.Equal("Vendedor", v.RotuloPessoa);
        Assert.Equal(["—", "João da Silva · Vendedor", "Maria Oliveira · Vendedor"], v.Vendedores.Select(o => o.Texto));

        v.Vendedor = v.Vendedores.First(o => o.Valor == Joao);
        v.Tipo = v.Tipos.First(t => t.Valor == supervisor.Id);
        Assert.Equal("Supervisor", v.RotuloPessoa);
        Assert.Equal(["—", "Ana Souza · Funcionário"], v.Vendedores.Select(o => o.Texto));
        Assert.Null(v.Vendedor.Valor); // João não pode ser Supervisor
        Assert.Contains(v.Validar("Carteira 1"), e => e.Contains("escolha quem será Supervisor"));
    }

    [Fact]
    public void Adicionar_papel_escolhe_o_primeiro_papel_sem_ninguem()
    {
        var supervisor = new TipoCarteiraDto { Id = Guid.NewGuid(), Nome = "Supervisor", Ordem = 2, Classificacoes = [ClassFuncionario] };
        var opcoes = Opcoes();
        opcoes.TiposCarteira = [Vendedor, supervisor];
        var atual = new CarteiraDto { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Joao, InicioEm = new DateOnly(2026, 1, 1) };
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Cliente", Carteira = [atual] });
        f.DefinirOpcoesComercial(opcoes);

        f.NovaCarteira();

        Assert.Equal(supervisor.Id, f.Carteira[0].Tipo.Valor);
    }

    [Fact]
    public void Vinculo_que_ja_comecou_fica_travado_e_o_planejado_nao()
    {
        var hoje = new DateOnly(2026, 9, 28);
        var comecou = CarteiraFormulario.De(new CarteiraDto { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Joao, InicioEm = hoje }, hoje);
        var planejado = CarteiraFormulario.De(new CarteiraDto { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Joao, InicioEm = hoje.AddDays(1) }, hoje);
        var novo = CarteiraFormulario.Nova(null, Vendedor.Id, hoje);

        Assert.True(comecou.Travado);
        Assert.False(planejado.Travado);
        Assert.False(novo.Travado);
        Assert.Equal("Vigente desde 28/09/2026", comecou.Situacao);
        Assert.Equal("Começa em 29/09/2026", planejado.Situacao);
        Assert.Equal("Desativar (lançado por engano)", comecou.TextoRemover);
        Assert.Equal("Remover", novo.TextoRemover);
    }

    [Fact]
    public void Trocar_abre_o_novo_no_mesmo_papel_e_credito_e_encerra_o_atual_na_vespera()
    {
        var (f, atual, novaSolta) = FichaComVendedor("15/03/2026");
        novaSolta.RemoverCommand.Execute(null); // sem o cartão solto: a troca é pelo botão
        atual.PercentualCredito = "70";
        var fimAntes = atual.FimEm;

        atual.TrocarCommand.Execute(null);
        var nova = f.Carteira[0];

        Assert.Same(atual, nova.Substitui);
        Assert.Equal(Vendedor.Id, nova.Tipo.Valor);
        Assert.Equal("70", nova.PercentualCredito);
        Assert.Null(nova.Vendedor.Valor);
        Assert.False(atual.PodeTrocar); // uma troca por vez
        Assert.Equal(TextoTela.Data(DateOnly.FromDateTime(DateTime.Today).AddDays(-1)), atual.FimEm);

        nova.InicioEm = "01/10/2026";
        Assert.Equal("30/09/2026", atual.FimEm); // acompanha o início do novo
        Assert.Empty(f.SubstituicoesDeVendedor()); // já é troca: não pergunta de novo

        nova.InicioEm = "01/01/2026";
        Assert.Contains(nova.Validar("Carteira 1"), e => e.Contains("precisa começar depois de 01/01/2026"));

        nova.RemoverCommand.Execute(null); // desistiu
        Assert.Equal(fimAntes, atual.FimEm);
        Assert.True(atual.PodeTrocar);
        Assert.DoesNotContain(nova, f.Carteira);
    }

    [Fact]
    public void Encerrados_e_desativados_vao_para_o_historico_e_os_novos_ficam_no_topo()
    {
        var encerrado = new CarteiraDto { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Maria, InicioEm = new DateOnly(2025, 1, 1), FimEm = new DateOnly(2025, 12, 31) };
        var engano = new CarteiraDto { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Maria, InicioEm = new DateOnly(2025, 6, 1), Ativo = false };
        var vigente = new CarteiraDto { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Joao, InicioEm = new DateOnly(2026, 1, 1) };
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Cliente", Carteira = [encerrado, engano, vigente] });

        Assert.Equal([vigente.Id], f.CarteiraAtual.Select(c => c.Id));
        Assert.Equal([engano.Id, encerrado.Id], f.CarteiraHistorico.Select(c => c.Id));
        Assert.True(f.TemHistoricoCarteira);
        Assert.Equal("Mostrar histórico (2)", f.TextoHistoricoCarteira);

        f.NovaCarteira();
        Assert.Equal(f.Carteira[0], f.CarteiraAtual[0]);
        Assert.Equal(4, f.ParaDto().Carteira.Count); // o histórico continua indo para a API (nada é apagado)
    }

    // ---- Motor Comercial, Fase 1c: prazo do vínculo ----

    [Fact]
    public void Prazo_mostra_duracao_e_quanto_falta_e_destaca_perto_do_fim()
    {
        var hoje = new DateOnly(2026, 9, 28);
        CarteiraFormulario Vinculo(DateOnly inicio, DateOnly? fim) =>
            CarteiraFormulario.De(new CarteiraDto { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Joao, InicioEm = inicio, FimEm = fim }, hoje);

        var longe = Vinculo(new DateOnly(2026, 9, 28), new DateOnly(2026, 12, 30));
        Assert.Equal("94 dias · faltam 93 dias", longe.Prazo);
        Assert.False(longe.PertoDoFim);

        var perto = Vinculo(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 10));
        Assert.Equal("40 dias · faltam 12 dias", perto.Prazo);
        Assert.True(perto.PertoDoFim);
        Assert.StartsWith("Termina em 12 dias: renove", perto.AvisoFim);

        perto.FimEm = string.Empty; // renovou sem fim
        Assert.Equal("sem data de fim", perto.Prazo);
        Assert.False(perto.PertoDoFim);

        var hojeTermina = Vinculo(new DateOnly(2026, 9, 1), hoje);
        Assert.Equal("Termina hoje: renove (mude ou limpe o fim), troque ou deixe encerrar.", hojeTermina.AvisoFim);

        var encerrado = Vinculo(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        Assert.Equal("31 dias", encerrado.Prazo);
        Assert.False(encerrado.PertoDoFim);

        var futuro = Vinculo(new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 3));
        Assert.Equal("1 dia · começa em 5 dias", futuro.Prazo);
        Assert.True(futuro.PertoDoFim); // termina em 5 dias

        perto.Ativo = false; // lançado por engano: sem aviso
        perto.FimEm = "10/10/2026";
        Assert.False(perto.PertoDoFim);
    }

    [Fact]
    public void Antecedencia_do_aviso_vem_das_opcoes()
    {
        var hoje = new DateOnly(2026, 9, 28);
        var v = CarteiraFormulario.De(new CarteiraDto { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Joao,
            InicioEm = new DateOnly(2026, 9, 1), FimEm = new DateOnly(2026, 10, 10) }, hoje);
        var opcoes = Opcoes();
        opcoes.DiasAvisoFimVinculo = 7;

        v.DefinirOpcoes(new OpcoesComercial(opcoes));

        Assert.False(v.PertoDoFim); // faltam 12 dias, aviso com 7
    }
}
