using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comercial;
using Lone.Contracts.GruposEmpresariais;
using Lone.Contracts.Pessoas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Ficha: filial gravada é desativada (não some), grupo empresarial, cabeçalho, comercial e relacionamentos.</summary>
public class EstruturaEmpresarialFormularioTests
{
    private static readonly GrupoEmpresarialDto GrupoJoao = new() { Id = Guid.NewGuid(), Nome = "Grupo João", Ativo = true };

    private static PessoaDto EmpresaGravada(Guid? grupo = null) => new()
    {
        Id = Guid.NewGuid(),
        Codigo = 12,
        Natureza = NaturezaPessoa.Juridica,
        Nome = "ABC Comércio e Participações Ltda",
        GrupoEmpresarialId = grupo,
        GrupoEmpresarialNome = grupo is null ? null : GrupoJoao.Nome,
        Estabelecimentos =
        [
            new EstabelecimentoDto { Id = Guid.NewGuid(), Cnpj = "11222333000181", Principal = true, NomeFantasia = "ABC Comércio" },
            new EstabelecimentoDto { Id = Guid.NewGuid(), Cnpj = "11222333000262", NomeFantasia = "ABC BH" }
        ],
        Papeis =
        [
            new PapelDto { Papel = TipoPapel.Cliente, Ativo = true, InicioEm = new DateOnly(2025, 1, 1) },
            new PapelDto { Papel = TipoPapel.Fornecedor, Ativo = true, InicioEm = new DateOnly(2025, 1, 1) }
        ]
    };

    [Fact]
    public void Remover_filial_gravada_desativa_e_ela_continua_na_gravacao()
    {
        var f = PessoaFormulario.De(EmpresaGravada());
        var filial = f.Estabelecimentos[1];
        Assert.True(filial.Gravado);
        Assert.Equal("Desativar filial", filial.TextoRemover);

        filial.RemoverCommand.Execute(null);

        Assert.Equal(2, f.Estabelecimentos.Count); // não saiu da lista
        Assert.False(filial.Ativo);
        Assert.True(filial.PodeReativar);
        var dto = f.ParaDto();
        Assert.Equal(2, dto.Estabelecimentos.Count);
        Assert.False(dto.Estabelecimentos.Single(e => e.Id == filial.Id).Ativo);

        filial.ReativarCommand.Execute(null);
        Assert.True(filial.Ativo);
    }

    [Fact]
    public void Remover_filial_nova_tira_da_lista()
    {
        var f = PessoaFormulario.De(EmpresaGravada());
        var nova = f.AdicionarEstabelecimento();
        Assert.False(nova.Gravado);

        nova.RemoverCommand.Execute(null);

        Assert.Equal(2, f.Estabelecimentos.Count);
        Assert.DoesNotContain(nova, f.Estabelecimentos);
    }

    [Fact]
    public void Cabecalho_da_empresa_mostra_nome_fantasia_razao_social_papeis_cnpj_grupo_e_estabelecimentos()
    {
        var f = PessoaFormulario.De(EmpresaGravada(GrupoJoao.Id));

        Assert.Equal("ABC Comércio", f.Titulo); // exibição → nome fantasia do principal → razão social
        Assert.Equal("Razão social: ABC Comércio e Participações Ltda", f.NomeCompletoCabecalho);
        Assert.Contains("Pessoa jurídica", f.TipoEPapeisCabecalho);
        Assert.Contains("Cliente", f.TipoEPapeisCabecalho);
        Assert.Contains("Fornecedor", f.TipoEPapeisCabecalho);
        Assert.Contains("CNPJ 11.222.333/0001-81", f.DocumentoCabecalho);
        Assert.Contains("Código 000012", f.DocumentoCabecalho);
        Assert.Contains("Grupo empresarial: Grupo João", f.EstruturaCabecalho);
        Assert.Contains("2 estabelecimentos", f.EstruturaCabecalho);

        f.NomeExibicao = "ABC";
        Assert.Equal("ABC", f.Titulo);
        f.Estabelecimentos[1].Ativo = false;
        Assert.Contains("2 estabelecimentos (1 ativo)", f.EstruturaCabecalho);
    }

    [Fact]
    public void Razao_social_nome_fantasia_e_nome_de_exibicao_sao_campos_distintos_e_a_ajuda_segue_a_natureza()
    {
        var f = PessoaFormulario.De(EmpresaGravada());

        Assert.Equal("Razão social", f.RotuloNome);
        Assert.Equal("ABC Comércio e Participações Ltda", f.Nome);
        Assert.Equal("ABC Comércio", f.Principal.NomeFantasia);
        Assert.Equal(string.Empty, f.NomeExibicao);
        Assert.Contains("Nome Fantasia e, na ausência dele, a Razão Social", f.AjudaNomeExibicao);

        // Precedência: exibição → fantasia → razão social; nenhum campo é gravado no lugar do outro.
        f.NomeExibicao = "ABC";
        Assert.Equal("ABC", f.Titulo);
        Assert.Equal("Razão social: ABC Comércio e Participações Ltda", f.NomeCompletoCabecalho);
        f.NomeExibicao = string.Empty;
        Assert.Equal("ABC Comércio", f.Titulo);
        Assert.Equal("Razão social: ABC Comércio e Participações Ltda", f.NomeCompletoCabecalho);
        f.Principal.NomeFantasia = string.Empty;
        Assert.Equal("ABC Comércio e Participações Ltda", f.Titulo);
        Assert.Equal(string.Empty, f.NomeCompletoCabecalho); // o título já é a razão social: não repete

        var mudou = new List<string?>();
        f.PropertyChanged += (_, e) => mudou.Add(e.PropertyName);
        f.Natureza = OpcoesPessoa.Naturezas.First(n => n.Valor == NaturezaPessoa.Fisica);
        Assert.Contains(nameof(PessoaFormulario.AjudaNomeExibicao), mudou);
        Assert.Equal("Nome completo", f.RotuloNome);
        Assert.DoesNotContain("Fantasia", f.AjudaNomeExibicao);
        f.Natureza = OpcoesPessoa.Naturezas.First(n => n.Valor == NaturezaPessoa.Estrangeiro);
        Assert.DoesNotContain("Fantasia", f.AjudaNomeExibicao);
    }

    [Fact]
    public void Grupo_gravado_volta_intacto_antes_e_depois_das_opcoes_e_so_vai_para_pessoa_juridica()
    {
        var f = PessoaFormulario.De(EmpresaGravada(GrupoJoao.Id));
        Assert.Equal(GrupoJoao.Id, f.ParaDto().GrupoEmpresarialId);

        f.DefinirGruposEmpresariais([GrupoJoao, new GrupoEmpresarialDto { Id = Guid.NewGuid(), Nome = "Grupo Antigo", Ativo = false }]);
        Assert.Equal(GrupoJoao.Id, f.ParaDto().GrupoEmpresarialId);
        Assert.DoesNotContain(f.GruposEmpresariais, o => o.Texto.StartsWith("Grupo Antigo")); // desativado só aparece para quem já tinha

        f.GrupoEmpresarial = PessoaFormulario.SemGrupoEmpresarial;
        Assert.Null(f.ParaDto().GrupoEmpresarialId); // empresa independente, sem grupo

        var pf = PessoaFormulario.NovaPessoa();
        pf.DefinirGruposEmpresariais([GrupoJoao]);
        pf.GrupoEmpresarial = pf.GruposEmpresariais.Single(o => o.Valor == GrupoJoao.Id);
        Assert.Equal(GrupoJoao.Id, pf.ParaDto().GrupoEmpresarialId); // nunca é limpo em silêncio...
        Assert.Contains(pf.ValidarLocalmente(), e => e.Contains("Só pessoa jurídica")); // ...a gravação é que é recusada
    }

    [Fact]
    public void Empresa_gravada_que_vira_pessoa_fisica_e_recusada_na_ficha_sem_perder_o_grupo()
    {
        var f = PessoaFormulario.De(EmpresaGravada(GrupoJoao.Id));
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Fisica);

        Assert.Contains(f.ValidarLocalmente(), e => e.Contains("não pode virar pessoa física"));
        Assert.Equal(GrupoJoao.Id, f.ParaDto().GrupoEmpresarialId);
    }

    [Fact]
    public void Aba_comercial_junta_cliente_e_fornecedor_e_relacionamentos_so_depois_de_gravar()
    {
        var gravada = PessoaFormulario.De(EmpresaGravada());
        var secoes = SecaoOpcao.Para(gravada);
        Assert.Single(secoes, s => s.Secao == SecaoPessoa.Comercial);
        Assert.Contains(secoes, s => s.Secao == SecaoPessoa.RelacionamentosPessoas);

        var nova = PessoaFormulario.NovaPessoa();
        Assert.DoesNotContain(SecaoOpcao.Para(nova), s => s.Secao == SecaoPessoa.RelacionamentosPessoas);
        nova.PapelCliente.Ativo = false;
        Assert.DoesNotContain(SecaoOpcao.Para(nova), s => s.Secao == SecaoPessoa.Comercial);
        nova.PapelFornecedor.Ativo = true;
        Assert.Contains(SecaoOpcao.Para(nova), s => s.Secao == SecaoPessoa.Comercial);
    }

    [Fact]
    public void Condicao_do_fornecedor_vem_do_cadastro_e_o_texto_anterior_e_preservado()
    {
        var trinta = new CondicaoPagamentoDto { Id = Guid.NewGuid(), Nome = "30 dias", Parcelas = "30", Ativo = true };
        var dto = EmpresaGravada();
        dto.ContasFornecedor = [new ContaFornecedorDto { Id = Guid.NewGuid(), CondicaoPagamento = "28 dias" }];
        var f = PessoaFormulario.De(dto);

        Assert.Contains("não convertida", f.ContaFornecedor.TextoCondicaoAnterior);
        f.DefinirOpcoesComercial(new ComercialOpcoesDto { Condicoes = [trinta] });
        f.ContaFornecedor.Condicao = f.ContaFornecedor.Condicoes.Single(c => c.Valor == trinta.Id);

        var conta = f.ParaDto().ContasFornecedor.Single(c => c.EmpresaId is null);
        Assert.Equal(trinta.Id, conta.CondicaoPagamentoId);
        Assert.Equal("28 dias", conta.CondicaoPagamento); // o texto antigo não é apagado
        Assert.DoesNotContain("não convertida", f.ContaFornecedor.TextoCondicaoAnterior);
    }

    [Fact]
    public void Avaliacao_do_fornecedor_vai_de_1_a_5()
    {
        var f = PessoaFormulario.De(EmpresaGravada());
        f.ContaFornecedor.Avaliacao = "0";
        Assert.Contains(f.ContaFornecedor.Validar(), e => e.Contains("1 a 5"));
        f.ContaFornecedor.Avaliacao = "5";
        Assert.Empty(f.ContaFornecedor.Validar());
        f.ContaFornecedor.Avaliacao = string.Empty;
        Assert.Empty(f.ContaFornecedor.Validar());
    }

    [Fact]
    public void Tipos_de_relacionamento_aparecem_nos_dois_sentidos_e_o_inverso_vai_na_requisicao()
    {
        var r = new RelacionamentosFormulario();
        r.DefinirTipos(
        [
            new TipoRelacionamentoDto { Id = TiposRelacionamentoSistema.SocioDe, Nome = "Sócio de", NomeInverso = "Tem como sócio", Societario = true },
            new TipoRelacionamentoDto { Id = TiposRelacionamentoSistema.ParceiroDe, Nome = "Parceiro de", NomeInverso = "Parceiro de" }
        ]);

        Assert.Equal(4, r.Tipos.Length); // "— escolha —", "Parceiro de" (simétrico, uma vez), "Sócio de", "Tem como sócio"
        Assert.Contains(r.ValidarNovo(), e => e.Contains("tipo"));

        r.NovoTipo = r.Tipos.Single(o => o.Texto == "Tem como sócio");
        r.PessoaEscolhida = new PessoaResumo { Id = Guid.NewGuid(), Nome = "João" };
        r.NovoInicio = "01/02/2024";
        Assert.Empty(r.ValidarNovo());

        var requisicao = r.ParaRequisicao();
        Assert.True(requisicao.Inverso); // a ficha aberta (a empresa) é o destino: grava João → Sócio de → empresa
        Assert.Equal(TiposRelacionamentoSistema.SocioDe, requisicao.TipoRelacionamentoId);
        Assert.Equal(new DateOnly(2024, 2, 1), requisicao.InicioEm);
    }

    [Fact]
    public void Relacionamento_encerrado_so_aparece_com_mostrar_encerrados()
    {
        var r = new RelacionamentosFormulario();
        r.Carregar(
        [
            new PessoaRelacionamentoDto { Id = Guid.NewGuid(), Tipo = "Sócio de", OutraPessoaNome = "ABC", Ativo = true, Vigente = true },
            new PessoaRelacionamentoDto { Id = Guid.NewGuid(), Tipo = "Sócio de", OutraPessoaNome = "XYZ", Ativo = true, Vigente = false, FimEm = new DateOnly(2025, 1, 1) }
        ]);

        Assert.True(r.TemEncerrados);
        Assert.Single(r.Itens, i => i.Visivel);
        r.MostrarEncerrados = true;
        Assert.All(r.Itens, i => Assert.True(i.Visivel));
        Assert.False(r.Itens[1].PodeEncerrar);
    }
}
