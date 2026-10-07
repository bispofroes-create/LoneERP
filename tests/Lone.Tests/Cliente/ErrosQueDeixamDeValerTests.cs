using System.Net;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Documentos;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Tests.Cliente;

/// <summary>
/// Correções de UX pós-P1-8, item 2: o resumo de erros (barra, marcas dos campos e contadores das abas) perde só o erro
/// que comprovadamente deixou de valer: o do item que saiu da ficha e, quando os erros vieram da conferência do app, o do
/// campo que a mesma conferência já não aponta. Erro da API que o app não sabe repetir fica até a próxima tentativa
/// (decisão de 04/10/2026), e nenhum erro novo aparece sem o usuário pedir para salvar.
/// </summary>
public class ErrosQueDeixamDeValerTests
{
    // ---- ResumoValidacao.Reconferir ----

    [Fact]
    public void Reconferir_tira_so_os_que_deixaram_de_valer_e_mantem_a_ordem()
    {
        var item = Guid.NewGuid();
        var resumo = new ResumoValidacao();
        var avisos = 0;
        resumo.Mudou += () => avisos++;
        resumo.Definir([
            new ErroValidacao("A", CamposFichaPessoa.Nome),
            new ErroValidacao("B", CamposFichaPessoa.DocumentoNumero, item),
            new ErroValidacao("C")]);
        avisos = 0;

        Assert.False(resumo.Reconferir(_ => true)); // nada saiu: nada é avisado
        Assert.Equal(0, avisos);

        Assert.True(resumo.Reconferir(e => e.Item != item));

        Assert.Equal(new[] { "A", "C" }, resumo.Itens.Select(i => i.Mensagem));
        Assert.Null(resumo.ErroDe(CamposFichaPessoa.DocumentoNumero, item));
        Assert.Equal("Corrija 2 informações para continuar.", resumo.Contagem);
        Assert.Equal(1, avisos);

        resumo.Reconferir(_ => false);
        Assert.False(resumo.Visivel); // sem erros, a barra some
    }

    // ---- Ficha de Pessoas ----

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente, List<DestinoCampo> Focos)> NovaFichaAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        await tela.NovoCommand.ExecuteAsync(null);
        var focos = new List<DestinoCampo>();
        tela.Validacao.FocoPedido += focos.Add;
        tela.Formulario!.Nome = "Ana";
        return (tela, ambiente, focos);
    }

    private static DocumentoFormulario NovoRg(PessoaFormulario f, string numero)
    {
        var d = new DocumentoFormulario();
        f.AdicionarDocumento(d);
        d.Tipo = d.Tipos.First(o => o.Valor == TiposDocumentoSistema.Id(TipoDocumento.Rg));
        d.Numero = numero;
        return d;
    }

    private static void RecusaDaApi(AmbienteCliente ambiente, params (string Mensagem, string? Campo, Guid? Item)[] erros) =>
        ambiente.Servidor.Problema(HttpStatusCode.BadRequest, ErrosApi.Validacao, "Dados inválidos.",
            (ErrosApi.CampoErros, erros.Select(e => e.Mensagem).ToArray()),
            (ErrosApi.CampoItens, erros.Select(e => (object)new { mensagem = e.Mensagem, campo = e.Campo, item = e.Item }).ToArray()));

    [Fact]
    public async Task Documento_removido_leva_junto_o_erro_dele_e_a_marca_da_aba()
    {
        var (tela, ambiente, _) = await NovaFichaAsync();
        var f = tela.Formulario!;
        var repetido = NovoRg(f, "777");
        RecusaDaApi(ambiente,
            ("RG terminado em 777: não permite repetir.", CamposFichaPessoa.DocumentoNumero, repetido.Id),
            ("Regra comercial.", null, null));
        await tela.SalvarCommand.ExecuteAsync(null);
        Assert.Equal(1, tela.ErrosPorAba[SecaoPessoa.Documentos]);

        repetido.RemoverCommand.Execute(null); // documento novo: sai da lista

        Assert.DoesNotContain(SecaoPessoa.Documentos, tela.ErrosPorAba.Keys);
        Assert.Null(tela.Validacao.ErroDe(CamposFichaPessoa.DocumentoNumero, repetido.Id));
        var restante = Assert.Single(tela.Validacao.Itens); // o erro geral da API continua valendo
        Assert.Equal("Regra comercial.", restante.Mensagem);
        Assert.True(tela.Validacao.Visivel);
    }

    [Fact]
    public async Task Documento_gravado_removido_inativo_tambem_leva_o_erro()
    {
        var (tela, ambiente, _) = await NovaFichaAsync();
        var f = tela.Formulario!;
        var rg = NovoRg(f, "777");
        RecusaDaApi(ambiente, ("RG: número repetido.", CamposFichaPessoa.DocumentoNumero, rg.Id));
        await tela.SalvarCommand.ExecuteAsync(null);
        Assert.True(tela.Validacao.Visivel);

        rg.Ativo = false; // o mesmo que "Remover" num documento gravado: fica inativo e deixa de ser conferido

        Assert.False(tela.Validacao.Visivel);
        Assert.Empty(tela.ErrosPorAba);
    }

    [Fact]
    public async Task Erro_da_API_continua_quando_so_se_mexe_no_campo()
    {
        var (tela, ambiente, _) = await NovaFichaAsync();
        var f = tela.Formulario!;
        var rg = NovoRg(f, "777");
        RecusaDaApi(ambiente, ("RG terminado em 777: já está em outra pessoa.", CamposFichaPessoa.DocumentoNumero, rg.Id));
        await tela.SalvarCommand.ExecuteAsync(null);

        rg.Numero = "778"; // o app não sabe se o número novo também está em outra pessoa: só a API diz

        Assert.NotNull(tela.Validacao.ErroDe(CamposFichaPessoa.DocumentoNumero, rg.Id));
        Assert.Equal(1, tela.ErrosPorAba[SecaoPessoa.Documentos]);
    }

    [Fact]
    public async Task Erro_do_app_corrigido_sai_e_o_outro_continua_e_ainda_leva_ao_campo()
    {
        var (tela, ambiente, focos) = await NovaFichaAsync();
        var f = tela.Formulario!;
        f.DataNascimento = "31/02/2020";
        var rg = NovoRg(f, "123");
        rg.ValidoAte = "amanhã";            // validade inválida: o app aponta o campo do documento
        var chamadas = ambiente.Servidor.Recebidas.Count;
        await tela.SalvarCommand.ExecuteAsync(null);
        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count); // conferência do app: nada foi para a API
        Assert.NotNull(tela.Validacao.ErroDe(CamposFichaPessoa.DataNascimento, null));
        Assert.Contains(tela.Validacao.Itens, i => i.Erro.Item == rg.Id);
        var errosAntes = tela.Validacao.Itens.Count;

        f.DataNascimento = "28/02/2020";

        Assert.Null(tela.Validacao.ErroDe(CamposFichaPessoa.DataNascimento, null));
        Assert.Equal(errosAntes - 1, tela.Validacao.Itens.Count);
        Assert.DoesNotContain(SecaoPessoa.Geral, tela.ErrosPorAba.Keys);
        Assert.Equal(1, tela.ErrosPorAba[SecaoPessoa.Documentos]);   // o do documento continua
        focos.Clear();
        Assert.True(tela.Validacao.IrParaPrimeiro());
        Assert.Equal(rg.Id, Assert.Single(focos).Item);                // e é para lá que a ficha leva agora
    }

    [Fact]
    public async Task Erro_geral_do_app_fica_enquanto_valer_e_sai_quando_corrigido()
    {
        var (tela, _, _) = await NovaFichaAsync();
        var f = tela.Formulario!;
        f.ContaCliente.LimiteCredito = "mil";
        await tela.SalvarCommand.ExecuteAsync(null);
        Assert.Contains("Cliente: limite de crédito inválido.", tela.Validacao.Itens.Select(i => i.Mensagem));

        f.Apelido = "Aninha"; // outra mudança: o erro geral ainda vale
        Assert.Contains("Cliente: limite de crédito inválido.", tela.Validacao.Itens.Select(i => i.Mensagem));

        f.ContaCliente.LimiteCredito = "1000";
        Assert.False(tela.Validacao.Visivel);
    }

    [Fact]
    public async Task Mexer_na_ficha_nunca_acrescenta_erro_sem_salvar()
    {
        var (tela, _, _) = await NovaFichaAsync();
        var f = tela.Formulario!;
        f.DataNascimento = "31/02/2020";
        await tela.SalvarCommand.ExecuteAsync(null);
        var antes = tela.Validacao.Itens.Select(i => i.Erro).ToList();

        f.ContaCliente.LimiteCredito = "mil"; // erro novo: só aparece quando o usuário tentar salvar de novo

        Assert.Equal(antes, tela.Validacao.Itens.Select(i => i.Erro));
    }

    [Fact]
    public async Task Endereco_removido_tira_so_o_erro_dele_mesmo_com_a_numeracao_mudando()
    {
        var (tela, _, _) = await NovaFichaAsync();
        var f = tela.Formulario!;
        var primeiro = f.Enderecos[0];
        primeiro.Municipio.Uf = "MG";              // começado, sem município
        var segundo = new EnderecoFormulario();
        f.AdicionarEndereco(segundo);
        segundo.Municipio.Uf = "SP";
        await tela.SalvarCommand.ExecuteAsync(null);
        Assert.NotNull(tela.Validacao.ErroDe(CamposFichaPessoa.Municipio, primeiro.Id));
        Assert.NotNull(tela.Validacao.ErroDe(CamposFichaPessoa.Municipio, segundo.Id));

        primeiro.RemoverCommand.Execute(null);    // o segundo vira "Endereço 1", mas o problema dele continua

        Assert.Null(tela.Validacao.ErroDe(CamposFichaPessoa.Municipio, primeiro.Id));
        Assert.NotNull(tela.Validacao.ErroDe(CamposFichaPessoa.Municipio, segundo.Id));
        Assert.Equal(1, tela.ErrosPorAba[SecaoPessoa.Enderecos]);
    }

    [Fact]
    public async Task Nova_tentativa_de_salvar_mostra_os_erros_reais_de_novo()
    {
        var (tela, ambiente, _) = await NovaFichaAsync();
        var f = tela.Formulario!;
        var rg = NovoRg(f, "777");
        RecusaDaApi(ambiente, ("RG: número repetido.", CamposFichaPessoa.DocumentoNumero, rg.Id));
        await tela.SalvarCommand.ExecuteAsync(null);
        rg.RemoverCommand.Execute(null);
        Assert.False(tela.Validacao.Visivel);

        var outro = NovoRg(f, "888");
        RecusaDaApi(ambiente, ("RG terminado em 888: não permite repetir.", CamposFichaPessoa.DocumentoNumero, outro.Id));
        await tela.SalvarCommand.ExecuteAsync(null);

        Assert.Equal("RG terminado em 888: não permite repetir.", tela.Validacao.ErroDe(CamposFichaPessoa.DocumentoNumero, outro.Id));
        Assert.Equal(1, tela.ErrosPorAba[SecaoPessoa.Documentos]);
    }
}
