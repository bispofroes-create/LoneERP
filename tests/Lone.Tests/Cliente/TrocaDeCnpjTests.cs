using System.Net;
using System.Text.Json;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comum;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using DocumentoFiscal = Lone.Domain.Validacao.Documento;

namespace Lone.Tests.Cliente;

/// <summary>
/// Troca de CNPJ na ficha (plano revisão 1, D-T1 pelo valor). Ficha nova consultada de novo com outro CNPJ (R1, pergunta
/// antes) ou o mesmo (R2, sem perguntar): uma rotina só troca, em memória, o que a consulta anterior pôs e o usuário não
/// alterou; o que ele digitou fica. Cadastro gravado com CNPJ de outra raiz (R4): recusa e oferece cadastrar como nova pessoa.
/// </summary>
public class TrocaDeCnpjTests
{
    private const string CnpjA = "11222333000181";
    private const string CnpjB = "60746948000112";

    private static DadosCnpj EmpresaA() => new()
    {
        Cnpj = CnpjA, RazaoSocial = "EMPRESA A LTDA", NomeFantasia = "LOJA A", NaturezaJuridica = "2062", CnaePrincipal = "4711301",
        Cep = "35790000", Logradouro = "Rua A", Numero = "100", Complemento = "Sala 1", Bairro = "Centro", Cidade = "Curvelo", Uf = "MG",
        CodigoMunicipioIbge = "3120904", Porte = "DEMAIS", Telefone = "(38) 3721-1000", Email = "contato@empresaa.com.br",
        Socios = [new SocioDto { Id = Guid.NewGuid(), Nome = "SOCIO A" }], Fonte = "BrasilAPI"
    };

    private static DadosCnpj EmpresaB(string? nomeFantasia = null) => new()
    {
        Cnpj = CnpjB, RazaoSocial = "EMPRESA B S.A.", NomeFantasia = nomeFantasia, Cep = "06029900", Logradouro = "Avenida B",
        Numero = "200", Bairro = "Vila Yara", Cidade = "Osasco", Uf = "SP", CodigoMunicipioIbge = "3534401", Porte = "DEMAIS",
        Telefone = "(11) 3684-4011", Email = "b@empresab.com.br", Fonte = "BrasilAPI"
    };

    private static PessoaFormulario NovaJuridica()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        return f;
    }

    private static MeioContatoFormulario Telefone(PessoaFormulario f) =>
        f.MeiosContato.First(m => m.Valor.Contains("3721", StringComparison.Ordinal));

    // ---- Rastro da consulta (§3.1) ----

    [Fact]
    public void Primeira_consulta_guarda_o_CNPJ_o_endereco_e_os_meios_que_criou()
    {
        var f = NovaJuridica();

        f.AplicarCnpj(f.Principal, EmpresaA());

        var c = f.ConsultaAplicada!;
        Assert.Equal(CnpjA, c.Cnpj);
        Assert.Equal("EMPRESA A LTDA", c.RazaoSocial);
        Assert.Equal(f.Enderecos.Single().Id, c.EnderecoId);
        Assert.True(c.EnderecoCriado); // o endereço em branco da ficha nova é todo da consulta
        Assert.Equal(f.MeiosContato.Select(m => m.Id).Order(), c.Meios.Select(m => m.Id).Order());
        Assert.Equal(2, c.Meios.Count);
    }

    [Fact]
    public void Endereco_que_ja_existia_com_logradouro_fica_marcado_como_nao_criado_pela_consulta()
    {
        var f = NovaJuridica();
        var existente = f.Enderecos.Single();
        PreencherComoA(existente);

        f.AplicarCnpj(f.Principal, EmpresaA());

        var c = f.ConsultaAplicada!;
        Assert.Equal(existente.Id, c.EnderecoId);
        Assert.False(c.EnderecoCriado);
        Assert.Equal("35790-000", existente.Cep); // o que estava vazio a consulta preencheu
    }

    [Fact]
    public void Telefone_que_ja_existia_nao_entra_no_rastro()
    {
        var f = NovaJuridica();
        var digitado = TelefoneDigitado(f, "(38) 3721-1000");

        f.AplicarCnpj(f.Principal, EmpresaA());

        var meio = Assert.Single(f.ConsultaAplicada!.Meios);
        Assert.Equal("contato@empresaa.com.br", meio.Valor);
        Assert.DoesNotContain(f.ConsultaAplicada.Meios, m => m.Id == digitado.Id);
    }

    // ---- Receita × digitado (§3.2, D-T1 pelo valor) ----

    [Fact]
    public void Troca_sem_edicoes_substitui_tudo_e_limpa_o_que_a_nova_nao_traz()
    {
        var f = NovaJuridica();
        f.AplicarCnpj(f.Principal, EmpresaA());
        var endereco = f.Enderecos.Single();

        f.AplicarCnpj(f.Principal, EmpresaB());

        Assert.Equal("EMPRESA B S.A.", f.Nome);
        Assert.Equal(string.Empty, f.Principal.NomeFantasia);     // B não tem: limpo (não fica o de A)
        Assert.Equal(string.Empty, f.Principal.NaturezaJuridica);
        Assert.Equal(string.Empty, f.Principal.CnaePrincipal);
        var unico = Assert.Single(f.Enderecos, e => e.Ativo);     // não nasce um segundo endereço
        Assert.Equal(endereco.Id, unico.Id);
        Assert.Equal("Avenida B", unico.Logradouro);
        Assert.Equal("200", unico.Numero);
        Assert.Equal(string.Empty, unico.Complemento);            // "Sala 1" era de A
        Assert.Equal("Osasco", unico.Municipio.Selecionado!.Nome);
        Assert.Equal(["(11) 3684-4011", "b@empresab.com.br"], f.MeiosContato.Select(m => m.Valor).Order().ToArray());
        Assert.Empty(f.Socios);                                   // B sem sócios: lista vazia
        Assert.Equal(CnpjB, f.ConsultaAplicada!.Cnpj);
        Assert.Equal(DocumentoFiscal.Formatar(CnpjB), f.Principal.Cnpj);
    }

    [Fact]
    public void Campo_editado_pelo_usuario_fica_na_troca()
    {
        var f = NovaJuridica();
        f.AplicarCnpj(f.Principal, EmpresaA());
        f.Principal.NomeFantasia = "MINHA LOJA";

        f.AplicarCnpj(f.Principal, EmpresaB(nomeFantasia: "LOJA B"));

        Assert.Equal("MINHA LOJA", f.Principal.NomeFantasia);
        Assert.Equal("EMPRESA B S.A.", f.Nome);
    }

    [Fact]
    public void Campo_editado_e_devolvido_ao_valor_da_consulta_continua_da_consulta()
    {
        var f = NovaJuridica();
        f.AplicarCnpj(f.Principal, EmpresaA());
        f.Principal.NomeFantasia = "OUTRA";
        f.Principal.NomeFantasia = "loja a"; // mesmo valor (sem contar maiúsculas)

        f.AplicarCnpj(f.Principal, EmpresaB(nomeFantasia: "LOJA B"));

        Assert.Equal("LOJA B", f.Principal.NomeFantasia);
    }

    [Fact]
    public void Endereco_da_consulta_alterado_fica_inteiro_e_a_nova_entra_em_outro()
    {
        var f = NovaJuridica();
        f.AplicarCnpj(f.Principal, EmpresaA());
        var deA = f.Enderecos.Single();
        deA.Numero = "150";

        var previsao = f.PrevisaoDaTroca();
        f.AplicarCnpj(f.Principal, EmpresaB());

        Assert.NotNull(previsao.EnderecoMantido);
        Assert.Contains(deA, f.Enderecos);
        Assert.Equal(("Rua A", "150", "Sala 1"), (deA.Logradouro, deA.Numero, deA.Complemento)); // nada de A misturado com B
        Assert.Equal("Curvelo", deA.Municipio.Selecionado!.Nome);
        var deB = Assert.Single(f.Enderecos, e => e.Ativo && !ReferenceEquals(e, deA));
        Assert.Equal("Avenida B", deB.Logradouro);
        Assert.Equal(deB.Id, f.ConsultaAplicada!.EnderecoId);
    }

    [Fact]
    public void Endereco_que_ja_existia_perde_so_o_que_a_consulta_completou_e_a_nova_entra_em_outro()
    {
        var f = NovaJuridica();
        var existente = f.Enderecos.Single();
        PreencherComoA(existente);
        f.AplicarCnpj(f.Principal, EmpresaA()); // só o CEP veio da consulta

        f.AplicarCnpj(f.Principal, EmpresaB());

        Assert.Contains(existente, f.Enderecos);
        Assert.Equal(string.Empty, existente.Cep);                     // era da consulta: limpo (nunca o CEP de B aqui)
        Assert.Equal(("Rua A", "100", "Sala 1"), (existente.Logradouro, existente.Numero, existente.Complemento)); // digitado: fica
        var deB = Assert.Single(f.Enderecos, e => e.Ativo && !ReferenceEquals(e, existente));
        Assert.Equal(("Avenida B", "06029-900"), (deB.Logradouro, deB.Cep));
        Assert.Equal(deB.Id, f.ConsultaAplicada!.EnderecoId);
        Assert.True(f.ConsultaAplicada.EnderecoCriado);
    }

    [Fact]
    public void Telefone_da_consulta_com_a_descricao_alterada_fica_e_o_email_sai()
    {
        var f = NovaJuridica();
        f.AplicarCnpj(f.Principal, EmpresaA());
        var telefone = Telefone(f);
        telefone.Descricao = "Recepção";

        f.AplicarCnpj(f.Principal, EmpresaB());

        Assert.Contains(telefone, f.MeiosContato);
        Assert.DoesNotContain(f.MeiosContato, m => m.Valor == "contato@empresaa.com.br");
        Assert.Contains(f.MeiosContato, m => m.Valor == "b@empresab.com.br");
    }

    [Fact]
    public void Telefone_digitado_igual_ao_da_consulta_anterior_fica()
    {
        var f = NovaJuridica();
        var digitado = TelefoneDigitado(f, "(38) 3721-1000");
        f.AplicarCnpj(f.Principal, EmpresaA());

        f.AplicarCnpj(f.Principal, EmpresaB());

        Assert.Contains(digitado, f.MeiosContato);
    }

    [Fact]
    public void Socios_da_consulta_anterior_saem_mesmo_sem_socios_na_nova()
    {
        var f = NovaJuridica();
        f.AplicarCnpj(f.Principal, EmpresaA());
        Assert.Single(f.Socios);

        f.AplicarCnpj(f.Principal, EmpresaB());

        Assert.Empty(f.Socios);
        Assert.False(f.TemSocios);
    }

    // ---- Só em memória (§3.3) ----

    [Fact]
    public void Cadastro_gravado_nunca_passa_pela_troca()
    {
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Natureza = NaturezaPessoa.Juridica, Nome = "EMPRESA A LTDA",
            Estabelecimentos = [new EstabelecimentoDto { Id = Guid.NewGuid(), Principal = true, Cnpj = CnpjA, NomeFantasia = "LOJA A" }]
        });

        f.SubstituirConsultaAnterior(EmpresaB()); // guarda
        Assert.Equal("EMPRESA A LTDA", f.Nome);
        Assert.Equal("LOJA A", f.Principal.NomeFantasia);

        f.AplicarCnpj(f.Principal, EmpresaA());   // conferência de sempre, sem rastro de troca
        Assert.Null(f.ConsultaAplicada);
        Assert.Equal(CnpjA, f.CnpjGravado);
    }

    [Fact]
    public void Depois_da_troca_a_gravacao_nao_leva_nada_da_empresa_anterior()
    {
        var f = NovaJuridica();
        f.AplicarCnpj(f.Principal, EmpresaA());
        f.AplicarCnpj(f.Principal, EmpresaB());

        var json = JsonSerializer.Serialize(f.ParaDto());

        foreach (var deA in new[] { "EMPRESA A", "LOJA A", "Rua A", "Sala 1", "3721", "empresaa", "SOCIO A", CnpjA, "4711301" })
            Assert.DoesNotContain(deA, json, StringComparison.Ordinal);
        Assert.Contains("Avenida B", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trocar_a_natureza_esquece_a_consulta()
    {
        var f = NovaJuridica();
        f.AplicarCnpj(f.Principal, EmpresaA());
        f.Confirmar = (_, _, _, _) => Task.FromResult(true);

        f.NaturezaNaTela = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Fisica);
        await Task.Delay(50); // a confirmação da troca de natureza é assíncrona

        Assert.Null(f.ConsultaAplicada);
    }

    // ---- Uma rotina só (§3.4) ----

    [Fact]
    public void Mesmo_CNPJ_de_novo_usa_a_mesma_troca_sem_duplicar_nada()
    {
        var porR1 = NovaJuridica();
        porR1.AplicarCnpj(porR1.Principal, EmpresaA());
        porR1.AplicarCnpj(porR1.Principal, EmpresaB());

        var porR2 = NovaJuridica();
        porR2.AplicarCnpj(porR2.Principal, EmpresaB());
        porR2.AplicarCnpj(porR2.Principal, EmpresaB());

        foreach (var f in new[] { porR1, porR2 })
        {
            Assert.Equal("EMPRESA B S.A.", f.Nome);
            Assert.Equal("Avenida B", Assert.Single(f.Enderecos, e => e.Ativo).Logradouro);
            Assert.Equal(["(11) 3684-4011", "b@empresab.com.br"], f.MeiosContato.Select(m => m.Valor).Order().ToArray());
        }
    }

    [Fact]
    public void Previsao_conta_os_dados_da_consulta_que_serao_trocados()
    {
        var f = NovaJuridica();
        f.AplicarCnpj(f.Principal, EmpresaA());
        var todos = f.PrevisaoDaTroca().Substituidos;

        f.Principal.NomeFantasia = "MINHA LOJA";

        Assert.True(todos > 5);
        Assert.Equal(todos - 1, f.PrevisaoDaTroca().Substituidos);
        Assert.Null(f.PrevisaoDaTroca().EnderecoMantido);
    }

    [Fact]
    public void Previsao_nao_conta_o_telefone_que_fica_por_ter_a_descricao_alterada()
    {
        var f = NovaJuridica();
        f.AplicarCnpj(f.Principal, EmpresaA());
        var todos = f.PrevisaoDaTroca().Substituidos;

        Telefone(f).Descricao = "Recepção"; // achado no teste no app: a contagem incluía este telefone, que fica

        Assert.Equal(todos - 1, f.PrevisaoDaTroca().Substituidos);
    }

    [Theory]
    [InlineData("11222333000181", "11.222.333/0001-81", true)]
    [InlineData("11222333000181", "11222333000262", true)] // filial da mesma empresa
    [InlineData("11222333000181", "60746948000112", false)]
    public void Raiz_do_CNPJ_sao_os_8_primeiros_digitos(string a, string b, bool mesma) =>
        Assert.Equal(mesma, PessoasViewModel.MesmaRaiz(a, b));

    // ---- Na tela: R1, R2, R4 ----

    private static string EmUso => "/" + Rotas.Pessoas.DocumentoEmUso;
    private static string Consulta(string cnpj) => "/" + Rotas.Consultas.Cnpj(cnpj);

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente)> FichaNovaJuridicaAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        ambiente.Servidor.ResponderEm("/" + Rotas.Pessoas.OpcoesEstrutura, HttpStatusCode.OK, new EstruturaEmpresarialOpcoesDto(), vezes: 5);
        await tela.NovoCommand.ExecuteAsync(null);
        tela.Formulario!.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        await Task.Delay(50);
        return (tela, ambiente);
    }

    /// <summary>Digita o CNPJ na ficha nova: confere o documento e consulta a Receita sozinha.</summary>
    private static async Task DigitarCnpjAsync(PessoasViewModel tela, AmbienteCliente ambiente, DadosCnpj dados)
    {
        ambiente.Servidor.ResponderEm(EmUso, HttpStatusCode.OK, new DocumentoEmUsoResposta { EmUso = false });
        ambiente.Servidor.ResponderEm(Consulta(dados.Cnpj), HttpStatusCode.OK, dados);
        tela.Formulario!.Principal.Cnpj = DocumentoFiscal.Formatar(dados.Cnpj);
        await Task.Delay(80);
    }

    [Fact]
    public async Task Outro_CNPJ_na_ficha_nova_pergunta_e_troca_sem_gravar_nada()
    {
        var (tela, ambiente) = await FichaNovaJuridicaAsync();
        await DigitarCnpjAsync(tela, ambiente, EmpresaA());
        Assert.Equal("EMPRESA A LTDA", tela.Formulario!.Nome);
        var antes = ambiente.Servidor.Recebidas.Count;
        ambiente.Dialogos.RespostaConfirmacao = true;

        await DigitarCnpjAsync(tela, ambiente, EmpresaB());

        var f = tela.Formulario!;
        Assert.Equal("EMPRESA B S.A.", f.Nome);
        Assert.Single(f.Enderecos, e => e.Ativo);
        var pergunta = Assert.Single(ambiente.Dialogos.Perguntas);
        Assert.Contains("EMPRESA A LTDA", pergunta);
        Assert.Contains("EMPRESA B S.A.", pergunta);
        Assert.Contains("O que você digitou será mantido", pergunta);
        // Só a conferência do documento e a consulta (e leituras): nada gravado, nada excluído.
        Assert.All(ambiente.Servidor.Recebidas.Skip(antes),
            r => Assert.True(r.Caminho == EmUso || r.Caminho == Consulta(CnpjB) || r.Metodo == HttpMethod.Get, $"{r.Metodo} {r.Caminho}"));
        Assert.DoesNotContain(ambiente.Servidor.Recebidas.Skip(antes), r => r.Metodo == HttpMethod.Delete || r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task Troca_cancelada_volta_o_CNPJ_e_nao_consulta_de_novo()
    {
        var (tela, ambiente) = await FichaNovaJuridicaAsync();
        await DigitarCnpjAsync(tela, ambiente, EmpresaA());
        ambiente.Dialogos.RespostaConfirmacao = false;

        await DigitarCnpjAsync(tela, ambiente, EmpresaB());
        await Task.Delay(50);

        var f = tela.Formulario!;
        Assert.Equal("EMPRESA A LTDA", f.Nome);
        Assert.Equal("LOJA A", f.Principal.NomeFantasia);
        Assert.Equal(DocumentoFiscal.Formatar(CnpjA), f.Principal.Cnpj);
        Assert.Equal(CnpjA, f.ConsultaAplicada!.Cnpj);
        Assert.Single(ambiente.Servidor.Recebidas, r => r.Caminho == Consulta(CnpjA)); // voltar o CNPJ não consulta A outra vez
    }

    [Fact]
    public async Task Mesmo_CNPJ_consultado_de_novo_nao_pergunta()
    {
        var (tela, ambiente) = await FichaNovaJuridicaAsync();
        await DigitarCnpjAsync(tela, ambiente, EmpresaA());
        ambiente.Servidor.ResponderEm(Consulta(CnpjA), HttpStatusCode.OK, EmpresaA());

        await tela.Formulario!.Principal.ConsultarCnpjCommand.ExecuteAsync(null);

        var f = tela.Formulario!;
        Assert.Empty(ambiente.Dialogos.Perguntas);
        Assert.Single(f.Enderecos, e => e.Ativo);
        Assert.Equal(2, f.MeiosContato.Count);
        Assert.Equal(2, ambiente.Servidor.Recebidas.Count(r => r.Caminho == Consulta(CnpjA)));
    }

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente, PessoaFormulario Ficha)> GravadaComOutroCnpjDigitadoAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        var empresa = new PessoaDto
        {
            Id = Guid.NewGuid(), Codigo = 12, Natureza = NaturezaPessoa.Juridica, Nome = "EMPRESA A LTDA",
            Estabelecimentos = [new EstabelecimentoDto { Id = Guid.NewGuid(), Cnpj = CnpjA, Principal = true, NomeFantasia = "LOJA A" }]
        };
        ambiente.Servidor.Responder(HttpStatusCode.OK, empresa);
        ambiente.Servidor.ResponderEm("/" + Rotas.Pessoas.OpcoesEstrutura, HttpStatusCode.OK, new EstruturaEmpresarialOpcoesDto(), vezes: 5);
        tela.Selecionado = new PessoaResumo { Id = empresa.Id, Nome = empresa.Nome };
        await Task.Delay(50);

        var ficha = tela.Formulario!;
        ambiente.Servidor.ResponderEm(EmUso, HttpStatusCode.OK, new DocumentoEmUsoResposta { EmUso = false }); // ao digitar
        ficha.Principal.Cnpj = DocumentoFiscal.Formatar(CnpjB);
        await Task.Delay(50);
        ambiente.Servidor.ResponderEm(Consulta(CnpjB), HttpStatusCode.OK, EmpresaB());
        return (tela, ambiente, ficha);
    }

    [Fact]
    public async Task Gravado_com_CNPJ_de_outra_raiz_recusa_e_cancelar_nao_muda_nada()
    {
        var (tela, ambiente, ficha) = await GravadaComOutroCnpjDigitadoAsync();
        ambiente.Dialogos.RespostaConfirmacao = false;

        await ficha.Principal.ConsultarCnpjCommand.ExecuteAsync(null);

        Assert.Same(ficha, tela.Formulario);
        Assert.Contains(ambiente.Dialogos.Perguntas, p => p.Contains("raiz diferente") && p.Contains("nova pessoa"));
        Assert.Equal(DocumentoFiscal.Formatar(CnpjA), ficha.Principal.Cnpj);
        Assert.Equal("EMPRESA A LTDA", ficha.Nome);
        Assert.Equal("LOJA A", ficha.Principal.NomeFantasia);
        Assert.False(tela.TemAlteracoes);
    }

    [Fact]
    public async Task Gravado_com_outra_raiz_ja_cadastrada_oferece_abrir_o_cadastro_existente()
    {
        var (tela, ambiente, ficha) = await GravadaComOutroCnpjDigitadoAsync();
        var outra = new PessoaDto
        {
            Id = Guid.NewGuid(), Codigo = 77, Natureza = NaturezaPessoa.Juridica, Nome = "EMPRESA B S.A.",
            Estabelecimentos = [new EstabelecimentoDto { Id = Guid.NewGuid(), Cnpj = CnpjB, Principal = true }]
        };
        ambiente.Servidor.ResponderEm(EmUso, HttpStatusCode.OK,
            new DocumentoEmUsoResposta { EmUso = true, Id = outra.Id, Codigo = 77, Nome = outra.Nome });
        ambiente.Servidor.Responder(HttpStatusCode.OK, outra);

        await ficha.Principal.ConsultarCnpjCommand.ExecuteAsync(null);
        await Task.Delay(80);

        Assert.Contains(ambiente.Dialogos.Perguntas, p => p.Contains("Já existe cadastro para este CNPJ") && p.Contains("000077"));
        Assert.Equal(outra.Id, tela.Formulario!.Id);
        Assert.Equal(DocumentoFiscal.Formatar(CnpjA), ficha.Principal.Cnpj); // a ficha anterior voltou ao CNPJ gravado
    }

    [Fact]
    public async Task Gravado_com_outra_raiz_abre_ficha_nova_com_o_CNPJ_sem_salvar_a_anterior()
    {
        var (tela, ambiente, ficha) = await GravadaComOutroCnpjDigitadoAsync();
        ambiente.Servidor.ResponderEm(EmUso, HttpStatusCode.OK, new DocumentoEmUsoResposta { EmUso = false }, vezes: 2);
        ambiente.Servidor.ResponderEm(Consulta(CnpjB), HttpStatusCode.OK, EmpresaB());
        var antes = ambiente.Servidor.Recebidas.Count;

        await ficha.Principal.ConsultarCnpjCommand.ExecuteAsync(null);
        await Task.Delay(80);

        var nova = tela.Formulario!;
        Assert.NotSame(ficha, nova);
        Assert.True(nova.Nova);
        Assert.True(nova.EhJuridica);
        Assert.Equal(DocumentoFiscal.Formatar(CnpjB), nova.Principal.Cnpj);
        Assert.Equal("EMPRESA B S.A.", nova.Nome); // a consulta da ficha nova preencheu
        Assert.Equal(DocumentoFiscal.Formatar(CnpjA), ficha.Principal.Cnpj);
        Assert.DoesNotContain(ambiente.Servidor.Recebidas.Skip(antes), r => r.Metodo == HttpMethod.Put || r.Metodo == HttpMethod.Delete ||
                                                              (r.Metodo == HttpMethod.Post && r.Caminho != EmUso));
    }

    // ---- Apoio ----

    /// <summary>O lugar de A digitado pelo usuário, sem o CEP (a consulta completa o CEP; sem ele, é "possível" o mesmo lugar).</summary>
    private static void PreencherComoA(EnderecoFormulario e)
    {
        e.Logradouro = "Rua A";
        e.Numero = "100";
        e.Complemento = "Sala 1";
        e.Bairro = "Centro";
        e.Municipio.Definir(3120904, "Curvelo", "MG");
    }

    private static MeioContatoFormulario TelefoneDigitado(PessoaFormulario f, string valor)
    {
        var meio = new MeioContatoFormulario { Tipo = Opcao.De(OpcoesPessoa.TiposContato, TipoContato.Telefone), Valor = valor };
        f.AdicionarMeio(meio);
        return meio;
    }
}
