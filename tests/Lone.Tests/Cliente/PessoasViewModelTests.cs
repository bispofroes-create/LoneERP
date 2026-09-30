using System.Net;
using Lone.Cliente.Api;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Etiquetas;
using Lone.Contracts.Profissoes;
using Lone.Contracts.Papeis;
using Lone.Contracts.Contatos;
using Lone.Contracts.Enderecos;
using Lone.Contracts.Documentos;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class PessoasViewModelTests
{
    internal static PessoasViewModel NovaTela(AmbienteCliente ambiente) =>
        new(new PessoasApi(ambiente.Api), new ConsultasApi(ambiente.Api), ambiente.Sessao, ambiente.Autenticacao,
            new MunicipiosApi(ambiente.Api), new CamposPersonalizadosApi(ambiente.Api), new EtiquetasApi(ambiente.Api),
            new ProfissoesApi(ambiente.Api), new PapeisApi(ambiente.Api),
            new TiposMeioContatoApi(ambiente.Api), new TiposEnderecoApi(ambiente.Api), new TiposDocumentoApi(ambiente.Api),
            new AnexosApi(ambiente.Api), new ColaboradoresApi(ambiente.Api), new ComercialApi(ambiente.Api), new TerritoriosApi(ambiente.Api), ambiente.Arquivos, ambiente.Dialogos);

    internal static readonly EtiquetaDto Vip = new() { Id = Guid.NewGuid(), Nome = "VIP", Ativo = true };
    internal static readonly ProfissaoDto Advogado = new() { Id = Guid.NewGuid(), Nome = "Advogado", Ativo = true };

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente)> AbrirTelaAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao()); // administrador: pode tudo
        return await AbrirTelaComAsync(ambiente);
    }

    /// <summary>Tela de pessoas com as leituras iniciais respondidas (sessão já definida pelo chamador).</summary>
    internal static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente)> AbrirTelaComAsync(AmbienteCliente ambiente,
                                                                                                 CatalogoFiltrosPessoasDto? catalogo = null)
    {
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<EtiquetaDto> { Vip }); // cadastro de etiquetas (ficha e filtro)
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<CampoPersonalizadoDto>()); // campos personalizados
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<ProfissaoDto> { Advogado }); // cadastro de profissões
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<PapelCadastroDto>()); // cadastro de papéis (vazio: usa os de sistema)
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<TipoMeioContatoDto>()); // tipos de telefone/e-mail
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<TipoEnderecoDto>()); // tipos de endereço
        ambiente.Servidor.Responder(HttpStatusCode.OK, Finalidades.Cadastro); // finalidades de endereço
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<TipoDocumentoDto>()); // tipos de documento
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<CampoPersonalizadoDto>()); // campos dos documentos
        ambiente.Servidor.Responder(HttpStatusCode.OK, catalogo ?? new CatalogoFiltrosPessoasDto()); // catálogo do painel de filtros
        ambiente.Servidor.Responder(HttpStatusCode.OK, new PaginaListaPessoas
        {
            Itens = [new() { Id = Guid.NewGuid(), Codigo = 1, Nome = "Ana", Natureza = NaturezaPessoa.Fisica }],
            Total = 1
        }); // primeira página da lista (com o total)

        var tela = NovaTela(ambiente);
        await tela.CarregarCommand.ExecuteAsync(null);
        return (tela, ambiente);
    }

    [Fact]
    public void Filtro_da_lista_vira_query_string_so_com_o_que_foi_informado()
    {
        Assert.Equal(string.Empty, PessoasApi.Consulta(new FiltroPessoas()));
        var cliente = Lone.Domain.Papeis.PapeisSistema.Id(TipoPapel.Cliente);
        Assert.Equal($"?texto=Jo%C3%A3o%20Silva&papelId={cliente}&incluirInativos=true",
            PessoasApi.Consulta(new FiltroPessoas { Texto = " João Silva ", PapelId = cliente, IncluirInativos = true }));
    }

    [Fact]
    public async Task Abas_acompanham_natureza_e_papeis()
    {
        var (tela, ambiente) = await AbrirTelaAsync();
        await tela.NovoCommand.ExecuteAsync(null);
        var f = tela.Formulario!;

        Assert.Contains(tela.Secoes, s => s.Secao == SecaoPessoa.Comercial); // papel Cliente: aba Comercial
        Assert.DoesNotContain(tela.Secoes, s => s.Secao == SecaoPessoa.Historico); // pessoa nova
        Assert.DoesNotContain(tela.Secoes, s => s.Secao == SecaoPessoa.RelacionamentosPessoas); // só depois de gravar

        // Cliente e Fornecedor: a mesma aba "Comercial", com os dois blocos (uma pessoa só).
        f.PapelFornecedor.Ativo = true;
        Assert.Single(tela.Secoes, s => s.Secao == SecaoPessoa.Comercial);
        ambiente.Servidor.Responder(HttpStatusCode.OK, new Lone.Contracts.Comercial.ComercialOpcoesDto()); // opções lidas quando a aba abre
        tela.SecaoSelecionada = tela.Secoes.First(s => s.Secao == SecaoPessoa.Comercial);
        Assert.True(tela.NoCliente);
        Assert.True(tela.NoFornecedor);
        f.PapelCliente.Ativo = false;
        Assert.False(tela.NoCliente);
        Assert.True(tela.NoFornecedor);
        f.PapelCliente.Ativo = true;

        tela.SecaoSelecionada = tela.Secoes.First(s => s.Secao == SecaoPessoa.Pessoais);
        // Virar PJ na aba Dados pessoais: a aba some e a ficha volta para a Identificação, que lê os grupos empresariais.
        ambiente.Servidor.Responder(HttpStatusCode.OK, new EstruturaEmpresarialOpcoesDto());
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        Assert.Equal(SecaoPessoa.Geral, tela.SecaoSelecionada!.Secao);
        Assert.Equal("Identificação", tela.Secoes[0].Texto);
        Assert.Contains(tela.Secoes, s => s.Secao == SecaoPessoa.Estabelecimentos && s.Texto == "Fiscal e estabelecimentos");
        Assert.Contains(tela.Secoes, s => s.Secao == SecaoPessoa.Documentos); // documentos valem para qualquer natureza
        Assert.DoesNotContain(tela.Secoes, s => s.Secao == SecaoPessoa.Pessoais); // dados pessoais só na pessoa física
        await Task.Delay(50);
        Assert.Single(ambiente.Servidor.Recebidas, r => r.Caminho == "/" + Rotas.Pessoas.OpcoesEstrutura);
    }

    [Fact]
    public async Task Pessoa_juridica_aberta_le_os_grupos_na_Identificacao_uma_vez_so()
    {
        var (tela, ambiente) = await AbrirTelaAsync();
        var grupo = new Lone.Contracts.GruposEmpresariais.GrupoEmpresarialDto { Id = Guid.NewGuid(), Nome = "Grupo João", Ativo = true };
        var empresa = new PessoaDto
        {
            Id = Guid.NewGuid(), Codigo = 12, Natureza = NaturezaPessoa.Juridica, Nome = "ABC Comércio Ltda",
            GrupoEmpresarialId = grupo.Id, GrupoEmpresarialNome = grupo.Nome,
            Estabelecimentos = [new EstabelecimentoDto { Id = Guid.NewGuid(), Cnpj = "11222333000181", Principal = true, NomeFantasia = "ABC" }]
        };
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, empresa)
            .Responder(HttpStatusCode.OK, new EstruturaEmpresarialOpcoesDto { GruposEmpresariais = [grupo] });

        tela.Selecionado = new PessoaResumo { Id = empresa.Id, Nome = empresa.Nome };
        await Task.Delay(50);

        var f = tela.Formulario!;
        Assert.Equal(SecaoPessoa.Geral, tela.SecaoSelecionada!.Secao);
        Assert.True(f.OpcoesEstruturaCarregadas);
        Assert.Equal(grupo.Id, f.GrupoEmpresarial.Valor); // o gravado continua escolhido
        Assert.Contains(f.GruposEmpresariais, o => o.Valor == grupo.Id);
        Assert.True(f.Principal.PrincipalDaPJ); // no cartão do principal, CNPJ e fantasia só leitura
        Assert.False(f.Principal.FilialDaPJ);

        tela.SecaoSelecionada = tela.Secoes.First(s => s.Secao == SecaoPessoa.Estabelecimentos);
        tela.SecaoSelecionada = tela.Secoes.First(s => s.Secao == SecaoPessoa.Geral);
        await Task.Delay(20);
        Assert.Single(ambiente.Servidor.Recebidas, r => r.Caminho == "/" + Rotas.Pessoas.OpcoesEstrutura);
        Assert.False(tela.TemAlteracoes);
    }

    [Fact]
    public async Task Contatos_em_duas_listas_cada_uma_com_o_seu_Adicionar()
    {
        var (tela, _) = await AbrirTelaAsync();
        await tela.NovoCommand.ExecuteAsync(null);
        var f = tela.Formulario!;
        Assert.True(f.SemTelefones);
        Assert.True(f.SemEmails);

        tela.AdicionarTelefoneCommand.Execute(null);
        tela.AdicionarEmailCommand.Execute(null);
        tela.AdicionarEmailCommand.Execute(null);

        Assert.False(f.SemTelefones);
        Assert.False(f.SemEmails);
        var telefone = Assert.Single(f.MeiosContato, m => m.NaListaTelefones);
        Assert.Equal(TipoContato.Celular, telefone.Tipo.Valor);
        Assert.True(telefone.Principal);
        var emails = f.MeiosContato.Where(m => m.NaListaEmails).ToList();
        Assert.Equal(2, emails.Count);
        Assert.True(emails[0].Principal); // o primeiro de cada tipo vira principal
        Assert.False(emails[1].Principal);
        Assert.DoesNotContain(telefone.TiposTelefone, t => t.Valor == TipoContato.Email);

        // Remover um e-mail novo tira da lista; a lista de e-mails continua com o outro.
        emails[1].RemoverCommand.Execute(null);
        Assert.Single(f.MeiosContato, m => m.NaListaEmails);
        emails[0].RemoverCommand.Execute(null);
        Assert.True(f.SemEmails);
        Assert.False(f.SemTelefones);

        // O que é gravado não muda: uma coleção só, com o tipo de cada item.
        Assert.Equal(TipoContato.Celular, Assert.Single(f.ParaDto().MeiosContato).Tipo);
    }

    [Fact]
    public async Task Salvar_envia_para_o_id_da_ficha_e_mostra_os_avisos_de_duplicidade()
    {
        var (tela, ambiente) = await AbrirTelaAsync();
        await tela.NovoCommand.ExecuteAsync(null);
        var f = tela.Formulario!;
        f.Nome = "Carlos";
        f.Enderecos[0].Municipio.Definir(3550308, "São Paulo", "SP"); // município escolhido da lista

        var gravada = f.ParaDto();
        gravada.Codigo = 7;
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, new ResultadoSalvarPessoa { Pessoa = gravada, Avisos = ["Já existe \"Carlos\" com o mesmo nome."] })
            .Responder(HttpStatusCode.OK, new PaginaListaPessoas());

        await tela.SalvarCommand.ExecuteAsync(null);

        var put = Assert.Single(ambiente.Servidor.Recebidas, r => r.Metodo == HttpMethod.Put);
        Assert.Equal(HttpMethod.Put, put.Metodo);
        Assert.Equal("/" + Rotas.Pessoas.PorId(f.Id), put.Caminho);
        Assert.Equal(TipoMensagem.Aviso, tela.TipoMensagem);
        Assert.Contains("mesmo nome", tela.Mensagem);
        Assert.False(tela.Formulario!.Nova);
        Assert.Equal("Código 000007", tela.Formulario.CodigoTexto);
    }

    [Fact]
    public async Task CEP_invalido_avisa_sem_chamar_o_servidor()
    {
        var (tela, ambiente) = await AbrirTelaAsync();
        await tela.NovoCommand.ExecuteAsync(null);
        var endereco = tela.Formulario!.Enderecos[0];
        endereco.Cep = "123";
        var chamadas = ambiente.Servidor.Recebidas.Count;

        await endereco.BuscarCepCommand.ExecuteAsync(null);

        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count);
        Assert.Equal(TipoMensagem.Aviso, tela.TipoMensagem);
    }

    [Fact]
    public async Task CEP_encontrado_preenche_o_endereco()
    {
        var (tela, ambiente) = await AbrirTelaAsync();
        await tela.NovoCommand.ExecuteAsync(null);
        var endereco = tela.Formulario!.Enderecos[0];
        ambiente.Servidor.Responder(HttpStatusCode.OK,
            new Lone.Contracts.Integracoes.DadosCep { Cep = "01310100", Logradouro = "Avenida Paulista", Cidade = "São Paulo", Uf = "SP", CodigoMunicipioIbge = "3550308" });

        endereco.Cep = "01310-100"; // 8 dígitos: a consulta já sai sozinha
        await Task.Delay(50);

        Assert.Equal("/" + Rotas.Consultas.Cep("01310100"), ambiente.Servidor.Recebidas[^1].Caminho);
        Assert.Equal("Avenida Paulista", endereco.Logradouro);
        Assert.Equal("SP", endereco.Municipio.Uf);
        Assert.Equal(3550308, endereco.Municipio.MunicipioId); // o CEP traz o código IBGE: município já escolhido
    }

    [Fact]
    public void Textos_de_data_e_numero_seguem_o_padrao_brasileiro()
    {
        Assert.True(TextoTela.TentarData("05/01/2026", out var data));
        Assert.Equal(new DateOnly(2026, 1, 5), data);
        Assert.True(TextoTela.TentarData("  ", out var vazia));
        Assert.Null(vazia);
        Assert.False(TextoTela.TentarData("2026-01-05", out _));

        Assert.True(TextoTela.TentarDecimal("1.234,56", out var valor));
        Assert.Equal(1234.56m, valor);
        Assert.Equal("1.234,56", TextoTela.Decimal(1234.56m));
    }
}

public class PessoasViewModelCepTests
{
    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente, EnderecoFormulario Endereco)> NovaFichaAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<EtiquetaDto>());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<CampoPersonalizadoDto>());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<ProfissaoDto>());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<PapelCadastroDto>());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<TipoMeioContatoDto>());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<TipoEnderecoDto>());
        ambiente.Servidor.Responder(HttpStatusCode.OK, Finalidades.Cadastro);
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<TipoDocumentoDto>());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<CampoPersonalizadoDto>());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new CatalogoFiltrosPessoasDto());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new PaginaListaPessoas());
        var tela = PessoasViewModelTests.NovaTela(ambiente);
        await tela.CarregarCommand.ExecuteAsync(null);
        await tela.NovoCommand.ExecuteAsync(null);
        return (tela, ambiente, tela.Formulario!.Enderecos[0]);
    }

    [Fact]
    public async Task CEP_inexistente_e_avisado_assim_que_os_8_digitos_sao_digitados()
    {
        var (tela, ambiente, endereco) = await NovaFichaAsync();
        ambiente.Servidor.Problema(HttpStatusCode.NotFound, ErrosApi.NaoEncontrado, "CEP não encontrado.");

        endereco.Cep = "99999-999";
        await Task.Delay(50); // a consulta automática não é aguardada por quem digita

        Assert.Equal("/" + Rotas.Consultas.Cep("99999999"), ambiente.Servidor.Recebidas[^1].Caminho);
        Assert.Equal(TipoMensagem.Aviso, tela.TipoMensagem);
        Assert.Contains("não existe", tela.Mensagem);
    }

    [Fact]
    public async Task CEP_geral_de_cidade_avisa_para_informar_o_logradouro()
    {
        var (tela, ambiente, endereco) = await NovaFichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK,
            new Lone.Contracts.Integracoes.DadosCep { Cep = "78175000", Logradouro = "", Cidade = "Poconé", Uf = "MT", CodigoMunicipioIbge = "5106505" });

        endereco.Cep = "78175-000";
        await Task.Delay(50);

        Assert.Equal("Poconé", endereco.Municipio.Selecionado!.Nome);
        Assert.Equal(TipoMensagem.Informacao, tela.TipoMensagem);
        Assert.Contains("logradouro", tela.Mensagem);
    }

    [Fact]
    public async Task CEP_incompleto_ou_repetido_nao_consulta_de_novo()
    {
        var (_, ambiente, endereco) = await NovaFichaAsync();
        ambiente.Servidor.Responder(HttpStatusCode.OK,
            new Lone.Contracts.Integracoes.DadosCep { Cep = "01310100", Logradouro = "Avenida Paulista", Cidade = "São Paulo", Uf = "SP", CodigoMunicipioIbge = "3550308" });

        endereco.Cep = "01310-10";
        endereco.Cep = "01310-100";
        await Task.Delay(50);
        var chamadas = ambiente.Servidor.Recebidas.Count;
        endereco.Cep = "01310100"; // mesmo número, outra formatação

        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count);
        Assert.Equal("Avenida Paulista", endereco.Logradouro);
    }

    [Fact]
    public async Task Idade_acompanha_a_data_de_nascimento()
    {
        var (tela, _, _) = await NovaFichaAsync();
        var f = tela.Formulario!;
        var nascimento = DateTime.Today.AddYears(-30);

        f.DataNascimento = nascimento.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("30 anos", f.Idade);

        f.DataNascimento = DateTime.Today.AddDays(3).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("Data no futuro", f.Idade);
        Assert.Contains("A data de nascimento está no futuro.", f.ValidarLocalmente());
    }
}
