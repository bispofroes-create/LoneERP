using System.Net;
using Lone.Cliente.Api;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Etiquetas;
using Lone.Contracts.Profissoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class PessoasViewModelTests
{
    internal static PessoasViewModel NovaTela(AmbienteCliente ambiente) =>
        new(new PessoasApi(ambiente.Api), new ConsultasApi(ambiente.Api), ambiente.Sessao, ambiente.Autenticacao,
            new MunicipiosApi(ambiente.Api), new CamposPersonalizadosApi(ambiente.Api), new EtiquetasApi(ambiente.Api),
            new ProfissoesApi(ambiente.Api), ambiente.Dialogos);

    internal static readonly EtiquetaDto Vip = new() { Id = Guid.NewGuid(), Nome = "VIP", Ativo = true };
    internal static readonly ProfissaoDto Advogado = new() { Id = Guid.NewGuid(), Nome = "Advogado", Ativo = true };

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente)> AbrirTelaAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao()); // administrador: pode tudo
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<EtiquetaDto> { Vip }); // cadastro de etiquetas (ficha e filtro)
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<CampoPersonalizadoDto>()); // campos personalizados
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<ProfissaoDto> { Advogado }); // cadastro de profissões
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<PessoaResumo>
        {
            new() { Id = Guid.NewGuid(), Codigo = 1, Nome = "Ana", Natureza = NaturezaPessoa.Fisica }
        });

        var tela = NovaTela(ambiente);
        await tela.CarregarCommand.ExecuteAsync(null);
        return (tela, ambiente);
    }

    [Fact]
    public void Filtro_da_lista_vira_query_string_so_com_o_que_foi_informado()
    {
        Assert.Equal(string.Empty, PessoasApi.Consulta(new FiltroPessoas()));
        Assert.Equal("?texto=Jo%C3%A3o%20Silva&papel=Cliente&incluirInativos=true",
            PessoasApi.Consulta(new FiltroPessoas { Texto = " João Silva ", Papel = TipoPapel.Cliente, IncluirInativos = true }));
    }

    [Fact]
    public async Task Abas_acompanham_natureza_e_papeis()
    {
        var (tela, _) = await AbrirTelaAsync();
        await tela.NovoCommand.ExecuteAsync(null);
        var f = tela.Formulario!;

        Assert.Contains(tela.Secoes, s => s.Secao == SecaoPessoa.Cliente);
        Assert.DoesNotContain(tela.Secoes, s => s.Secao == SecaoPessoa.Historico); // pessoa nova

        f.PapelFornecedor.Ativo = true;
        Assert.Contains(tela.Secoes, s => s.Secao == SecaoPessoa.Fornecedor);

        tela.SecaoSelecionada = tela.Secoes.First(s => s.Secao == SecaoPessoa.Documentos);
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        Assert.DoesNotContain(tela.Secoes, s => s.Secao == SecaoPessoa.Documentos);
        Assert.Equal(SecaoPessoa.Geral, tela.SecaoSelecionada!.Secao); // a aba sumiu: volta para Geral
        Assert.Equal("Empresa e estabelecimentos", tela.Secoes[1].Texto);
        Assert.DoesNotContain(tela.Secoes, s => s.Secao == SecaoPessoa.Pessoais); // dados pessoais só na pessoa física
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
            .Responder(HttpStatusCode.OK, new List<PessoaResumo>());

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
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<PessoaResumo>());
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
