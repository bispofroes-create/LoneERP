using System.Net;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Tests.Cliente;

/// <summary>Lone Contextual, Fase 1: o erro leva ao campo (resumo fixo, aba certa, pedido de foco), sem quebrar o erro antigo.</summary>
public class ErrosQueLevamAoCampoTests
{
    // ---- Transporte (ClienteApi) ----

    [Fact]
    public async Task Resposta_com_itens_vira_excecao_com_o_campo_de_cada_erro()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var endereco = Guid.NewGuid();
        ambiente.Servidor.Problema(HttpStatusCode.BadRequest, ErrosApi.Validacao, "Dados inválidos.",
            (ErrosApi.CampoErros, new[] { "CPF inválido.", "Endereço 1: informe o logradouro.", "Regra sem campo." }),
            (ErrosApi.CampoItens, new object[]
            {
                new { mensagem = "CPF inválido.", campo = CamposFichaPessoa.Documento },
                new { mensagem = "Endereço 1: informe o logradouro.", campo = CamposFichaPessoa.Logradouro, item = endereco },
                new { mensagem = "Regra sem campo." }
            }));

        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => ambiente.Api.PostAsync("api/v1/teste", new { }));

        Assert.Equal(new[] { "CPF inválido.", "Endereço 1: informe o logradouro.", "Regra sem campo." }, erro.Erros);
        Assert.Equal(CamposFichaPessoa.Documento, erro.Itens[0].Campo);
        Assert.Equal(endereco, erro.Itens[1].Item);
        Assert.False(erro.Itens[2].TemCampo);
    }

    [Fact]
    public async Task Resposta_antiga_so_com_textos_continua_funcionando_como_erro_geral()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor.Problema(HttpStatusCode.BadRequest, ErrosApi.Validacao, "Dados inválidos.",
            (ErrosApi.CampoErros, new[] { "Informe o nome.", "CPF inválido." }));

        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => ambiente.Api.PostAsync("api/v1/teste", new { }));

        Assert.Equal(new[] { "Informe o nome.", "CPF inválido." }, erro.Erros);
        Assert.All(erro.Itens, i => Assert.False(i.TemCampo));
    }

    // ---- Campo → aba ----

    [Fact]
    public void Cada_campo_leva_a_aba_onde_ele_fica()
    {
        Assert.Equal(SecaoPessoa.Geral, AbaDoCampo.De(CamposFichaPessoa.Documento)); // CPF/CNPJ: Identificação, não Documentos
        Assert.Equal(SecaoPessoa.Documentos, AbaDoCampo.De(CamposFichaPessoa.DocumentoNumero, Guid.NewGuid()));
        Assert.Equal(SecaoPessoa.Enderecos, AbaDoCampo.De(CamposFichaPessoa.Municipio, Guid.NewGuid()));
        Assert.Equal(SecaoPessoa.Pessoais, AbaDoCampo.De(CamposFichaPessoa.Profissao));
        Assert.Equal(SecaoPessoa.Contatos, AbaDoCampo.De(CamposFichaPessoa.ContatoEmail, Guid.NewGuid()));
        Assert.Equal(SecaoPessoa.Estabelecimentos, AbaDoCampo.De(CamposFichaPessoa.InscricaoEstadual, Guid.NewGuid()));
        Assert.Equal(SecaoPessoa.Comercial, AbaDoCampo.De(CamposFichaPessoa.LimiteCredito));
        Assert.Equal(SecaoPessoa.Relacionamento, AbaDoCampo.De(CamposFichaPessoa.PrimeiroContato));
        // Natureza jurídica: a do principal na Identificação; a da filial no cartão dela.
        Assert.Equal(SecaoPessoa.Geral, AbaDoCampo.De(CamposFichaPessoa.NaturezaJuridica));
        Assert.Equal(SecaoPessoa.Estabelecimentos, AbaDoCampo.De(CamposFichaPessoa.NaturezaJuridica, Guid.NewGuid()));
        Assert.Null(AbaDoCampo.De("desconhecido.campo"));
        Assert.Null(AbaDoCampo.De(null));
    }

    // ---- Resumo ----

    [Fact]
    public void Resumo_conta_mostra_e_so_leva_quem_tem_campo()
    {
        var resumo = new ResumoValidacao();
        var focos = new List<ErroValidacao>();
        resumo.FocoPedido += focos.Add;
        resumo.AntesDeIr = _ => true;
        var item = Guid.NewGuid();

        Assert.False(resumo.Visivel);
        resumo.Definir([new ErroValidacao("Regra geral."), new ErroValidacao("Endereço 1: CEP.", CamposFichaPessoa.Cep, item)]);

        Assert.True(resumo.Visivel);
        Assert.Equal("Corrija 2 informações para continuar.", resumo.Contagem);
        Assert.Equal("Endereço 1: CEP.", resumo.ErroDe(CamposFichaPessoa.Cep, item));
        Assert.Null(resumo.ErroDe(CamposFichaPessoa.Cep, Guid.NewGuid())); // outro endereço: sem erro

        resumo.IrParaCommand.Execute(resumo.Itens[0]); // erro geral: não leva a lugar nenhum
        Assert.Empty(focos);
        Assert.True(resumo.IrParaPrimeiro());          // o primeiro COM campo, mesmo sendo o segundo da lista
        Assert.Equal(CamposFichaPessoa.Cep, Assert.Single(focos).Campo);

        resumo.Definir([new ErroValidacao("Só um.", CamposFichaPessoa.Nome)]);
        Assert.Equal("Corrija 1 informação para continuar.", resumo.Contagem);
        Assert.Null(resumo.ErroDe(CamposFichaPessoa.Cep, item)); // a conferência nova substitui: nada fica de antes

        resumo.Limpar();
        Assert.False(resumo.Visivel);
    }

    [Fact]
    public void Sem_a_aba_do_campo_o_resumo_nao_pede_foco_e_lista_longa_rola_dentro()
    {
        var resumo = new ResumoValidacao { AntesDeIr = _ => false };
        var focos = 0;
        resumo.FocoPedido += _ => focos++;
        resumo.Definir(Enumerable.Range(1, 5).Select(i => new ErroValidacao($"Erro {i}", CamposFichaPessoa.Nome)));

        Assert.False(resumo.IrParaPrimeiro());
        Assert.Equal(0, focos);
        Assert.True(resumo.ListaLonga);
    }

    // ---- Ficha de Pessoas ----

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente, List<ErroValidacao> Focos)> NovaFichaAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        await tela.NovoCommand.ExecuteAsync(null);
        var focos = new List<ErroValidacao>();
        tela.Validacao.FocoPedido += focos.Add;
        return (tela, ambiente, focos);
    }

    [Fact]
    public async Task Conferencia_do_app_vai_para_o_resumo_troca_a_aba_e_pede_o_foco_no_campo()
    {
        var (tela, ambiente, focos) = await NovaFichaAsync();
        var f = tela.Formulario!;
        f.Nome = "Ana";
        f.DataNascimento = "31/02/2020";
        tela.SecaoSelecionada = tela.Secoes.First(s => s.Secao == SecaoPessoa.Contatos);
        var chamadas = ambiente.Servidor.Recebidas.Count;

        await tela.SalvarCommand.ExecuteAsync(null);

        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count); // nada foi para a API
        Assert.True(tela.Validacao.Visivel);
        Assert.Contains(tela.Validacao.Itens, i => i.Erro.Campo == CamposFichaPessoa.DataNascimento);
        Assert.Equal(SecaoPessoa.Geral, tela.SecaoSelecionada!.Secao);
        Assert.Equal(CamposFichaPessoa.DataNascimento, focos.First().Campo);
        Assert.True(string.IsNullOrEmpty(tela.Mensagem)); // o erro da ficha fica no resumo, não na barra de mensagens
    }

    [Fact]
    public async Task Erro_da_API_com_campo_marca_a_aba_e_so_sai_numa_nova_conferencia()
    {
        var (tela, ambiente, focos) = await NovaFichaAsync();
        var f = tela.Formulario!;
        f.Nome = "Carlos";
        f.Enderecos[0].Municipio.Definir(3550308, "São Paulo", "SP");
        var endereco = f.Enderecos[0].Id;
        ambiente.Servidor.Problema(HttpStatusCode.BadRequest, ErrosApi.Validacao, "Dados inválidos.",
            (ErrosApi.CampoErros, new[] { "Endereço 1: o CEP deve ter 8 dígitos.", "Regra comercial." }),
            (ErrosApi.CampoItens, new object[]
            {
                new { mensagem = "Endereço 1: o CEP deve ter 8 dígitos.", campo = CamposFichaPessoa.Cep, item = endereco },
                new { mensagem = "Regra comercial." }
            }));

        await tela.SalvarCommand.ExecuteAsync(null);

        Assert.Equal(2, tela.Validacao.Itens.Count);
        Assert.Equal("Endereço 1: o CEP deve ter 8 dígitos.", tela.Validacao.ErroDe(CamposFichaPessoa.Cep, endereco));
        Assert.False(tela.Validacao.Itens[1].TemCampo); // erro geral: continua aparecendo, sem link
        Assert.Equal(SecaoPessoa.Enderecos, tela.SecaoSelecionada!.Secao);
        Assert.Equal(endereco, focos.Single().Item);
        Assert.Equal(1, tela.ErrosPorAba[SecaoPessoa.Enderecos]);
        Assert.True(string.IsNullOrEmpty(tela.Mensagem));

        // Mexer no endereço não resolve o erro: só uma nova conferência diz.
        f.Enderecos[0].Logradouro = "Avenida Paulista";
        Assert.NotNull(tela.Validacao.ErroDe(CamposFichaPessoa.Cep, endereco));

        // Salvou: os erros somem.
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, new ResultadoSalvarPessoa { Pessoa = f.ParaDto(), Avisos = [] })
            .Responder(HttpStatusCode.OK, new PaginaListaPessoas());
        await tela.SalvarCommand.ExecuteAsync(null);

        Assert.False(tela.Validacao.Visivel);
        Assert.Empty(tela.ErrosPorAba);
    }

    [Fact]
    public async Task Erro_antigo_sem_campo_aparece_no_resumo_sem_trocar_de_aba()
    {
        var (tela, ambiente, focos) = await NovaFichaAsync();
        var f = tela.Formulario!;
        f.Nome = "Carlos";
        f.Enderecos[0].Municipio.Definir(3550308, "São Paulo", "SP");
        tela.SecaoSelecionada = tela.Secoes.First(s => s.Secao == SecaoPessoa.Contatos);
        ambiente.Servidor.Problema(HttpStatusCode.BadRequest, ErrosApi.Validacao, "Dados inválidos.",
            (ErrosApi.CampoErros, new[] { "Regra antiga sem campo." }));

        await tela.SalvarCommand.ExecuteAsync(null);

        var item = Assert.Single(tela.Validacao.Itens);
        Assert.Equal("Regra antiga sem campo.", item.Mensagem);
        Assert.False(item.TemCampo);
        Assert.Empty(focos);
        Assert.Equal(SecaoPessoa.Contatos, tela.SecaoSelecionada!.Secao);
    }
}
