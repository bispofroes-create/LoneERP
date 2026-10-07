using System.Net;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comum;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;

namespace Lone.Tests.Cliente;

/// <summary>
/// Bloco A (país primeiro; endereço do exterior), na ficha:
/// <list type="bullet">
/// <item>no exterior nada do CEP do Brasil acontece: nem a consulta ao completar algarismos, nem "Buscar CEP", "Conferir
/// CEP" ou "Não sei o CEP" — o ViaCEP nunca é chamado e o endereço nunca é trocado para o Brasil (D-2);</item>
/// <item>o código postal do exterior é um campo próprio, com letras, e vai e volta pela mesma coluna do CEP (D-1);</item>
/// <item>trocar Brasil ↔ exterior não apaga nada da tela: o do outro lado fica guardado, não é gravado e o aviso diz o quê (D5);</item>
/// <item>o código do país vazio ou 1058 no exterior é recusado pela ficha (viraria Brasil na API, D-3).</item>
/// </list>
/// </summary>
public class EnderecoPaisPrimeiroTests
{
    private static EnderecoFormulario Brasil() =>
        EnderecoFormulario.De(new EnderecoDto
        {
            Id = Guid.NewGuid(), Cep = "01426002", Logradouro = "Rua Oscar Freire", Numero = "1000", Complemento = "Apto 12",
            Bairro = "Cerqueira César", MunicipioId = 3550308, Cidade = "São Paulo", Uf = "SP"
        });

    private static EnderecoFormulario Exterior(string? postal = "SW1A 1AA", string codigo = "6289") =>
        EnderecoFormulario.De(new EnderecoDto
        {
            Id = Guid.NewGuid(), Cep = postal, Logradouro = "Downing Street", Numero = "10", Cidade = "Londres", Uf = "EX",
            CodigoPais = codigo, Pais = "Reino Unido"
        });

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente, EnderecoFormulario Endereco)> FichaAsync(
        NaturezaPessoa natureza = NaturezaPessoa.Fisica)
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        await tela.NovoCommand.ExecuteAsync(null);
        var f = tela.Formulario!;
        ambiente.Servidor.ResponderEm("/" + Rotas.Pessoas.OpcoesEstrutura, HttpStatusCode.OK, new EstruturaEmpresarialOpcoesDto(), vezes: 5);
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, natureza);
        f.Nome = "John Smith";
        var e = f.Enderecos[0];
        e.NoExterior = true;
        e.Logradouro = "Downing Street";
        e.Numero = "10";
        e.Cidade = "Londres";
        e.Pais = "Reino Unido";
        e.CodigoPais = "6289";
        return (tela, ambiente, e);
    }

    // ---- D-2: no exterior, nada do CEP do Brasil ----

    [Theory]
    [InlineData(NaturezaPessoa.Fisica)]
    [InlineData(NaturezaPessoa.Juridica)]
    [InlineData(NaturezaPessoa.Estrangeiro)]
    public async Task Exterior_com_oito_algarismos_no_codigo_postal_nao_consulta_cep_nem_troca_para_o_brasil(NaturezaPessoa natureza)
    {
        var (tela, ambiente, e) = await FichaAsync(natureza);
        var chamadas = ambiente.Servidor.Recebidas.Count;

        e.CodigoPostal = "01426002"; // número de CEP do Brasil digitado num código postal do exterior
        e.Cep = "01426-002";         // nem o campo do CEP do Brasil (escondido) consulta enquanto o endereço é do exterior
        await Task.Delay(50);

        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count);
        Assert.True(e.NoExterior);
        Assert.Equal(("Downing Street", "10", "Londres"), (e.Logradouro, e.Numero, e.Cidade));
        Assert.False(tela.TemMensagem);
    }

    [Fact]
    public async Task Buscar_e_conferir_cep_no_exterior_nao_chamam_a_api()
    {
        var (_, ambiente, e) = await FichaAsync();
        e.Cep = "01426-002";
        var chamadas = ambiente.Servidor.Recebidas.Count;

        await e.BuscarCepCommand.ExecuteAsync(null);
        await e.ConferirCepCommand.ExecuteAsync(null);
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);

        Assert.Equal(chamadas, ambiente.Servidor.Recebidas.Count);
        Assert.True(e.NoExterior);
        Assert.False(e.TemConferenciaCep);
    }

    [Fact]
    public void No_exterior_o_bloco_do_cep_do_brasil_fica_escondido()
    {
        var e = Exterior();

        Assert.False(e.NoBrasil);           // CEP, Buscar, Conferir e "Não sei o CEP" ficam visíveis só com NoBrasil
        Assert.True(Brasil().NoBrasil);
    }

    [Fact]
    public void Gravacao_do_exterior_nao_pede_conferencia_de_cep()
    {
        var e = Exterior();

        var dto = e.ParaDto(0);

        Assert.False(dto.ConferenciaCepNaFicha);
        Assert.Null(dto.SugestaoCepAplicada);
        Assert.Null(dto.MunicipioId);
    }

    // ---- D-1: o código postal do exterior ----

    [Fact]
    public void Codigo_postal_do_exterior_vai_e_volta_pela_coluna_do_cep_com_as_letras()
    {
        var e = Exterior("SW1A 1AA");

        Assert.Equal("SW1A 1AA", e.CodigoPostal);
        Assert.Equal("", e.Cep);                               // não vira "CEP" com a máscara do Brasil
        Assert.Equal("SW1A 1AA", e.ParaDto(0).Cep);
        Assert.Equal(("6289", "Reino Unido"), (e.ParaDto(0).CodigoPais, e.ParaDto(0).Pais));
    }

    [Fact]
    public void Codigo_postal_de_oito_algarismos_no_exterior_nao_e_formatado_como_cep()
    {
        var e = Exterior("12345678");

        Assert.Equal("12345678", e.CodigoPostal);
        Assert.Equal("12345678", e.ParaDto(0).Cep);
    }

    [Fact]
    public void Brasil_continua_com_o_cep_formatado_e_sem_codigo_postal()
    {
        var e = Brasil();

        Assert.Equal("01426-002", e.Cep);
        Assert.Equal("", e.CodigoPostal);
        Assert.Equal("01426-002", e.ParaDto(0).Cep);
        Assert.Equal(PessoaEndereco.CodigoPaisBrasil, e.ParaDto(0).CodigoPais);
    }

    // ---- D5: troca de país sem perda silenciosa ----

    [Fact]
    public void Brasil_para_exterior_guarda_cep_e_municipio_nao_grava_e_avisa()
    {
        var e = Brasil();
        Assert.False(e.TemAvisoTrocaPais);

        e.NoExterior = true;

        Assert.Equal("01426-002", e.Cep);                                  // guardado na tela
        Assert.Equal(3550308, e.Municipio.MunicipioId);
        var dto = e.ParaDto(0);
        Assert.Null(dto.Cep);                                              // não vai na gravação do exterior
        Assert.Null(dto.MunicipioId);
        Assert.Null(dto.Uf);
        Assert.True(e.TemAvisoTrocaPais);
        Assert.Contains("CEP, UF e município", e.AvisoTrocaPais);
        Assert.Equal(("Rua Oscar Freire", "1000", "Apto 12"), (e.Logradouro, e.Numero, e.Complemento)); // comuns aos dois lados
    }

    [Fact]
    public void Voltar_ao_brasil_traz_tudo_de_volta_sem_aviso()
    {
        var e = Brasil();
        e.NoExterior = true;

        e.NoExterior = false;

        var dto = e.ParaDto(0);
        Assert.Equal(("01426-002", (int?)3550308, "SP", PessoaEndereco.CodigoPaisBrasil, "Brasil"),
            (dto.Cep, dto.MunicipioId, dto.Uf, dto.CodigoPais, dto.Pais));
        Assert.False(e.TemAvisoTrocaPais);
    }

    [Fact]
    public void Exterior_para_brasil_e_de_volta_nao_perde_pais_codigo_codigo_postal_nem_cidade()
    {
        var e = Exterior("SW1A 1AA");

        e.NoExterior = false;
        Assert.True(e.TemAvisoTrocaPais);
        Assert.Contains("país, código do país, código postal e cidade", e.AvisoTrocaPais);
        var noBrasil = e.ParaDto(0);
        Assert.Equal((PessoaEndereco.CodigoPaisBrasil, "Brasil"), (noBrasil.CodigoPais, noBrasil.Pais)); // no Brasil vai o Brasil
        Assert.Null(noBrasil.Cep);

        e.NoExterior = true;                                                // e voltando ao exterior, nada se perdeu

        var dto = e.ParaDto(0);
        Assert.Equal(("6289", "Reino Unido", "SW1A 1AA", "Londres"), (dto.CodigoPais, dto.Pais, dto.Cep, dto.Cidade));
        Assert.False(e.TemAvisoTrocaPais);
    }

    [Fact]
    public void Endereco_novo_que_vai_para_o_exterior_comeca_sem_o_pais_do_brasil()
    {
        var e = new EnderecoFormulario();

        e.NoExterior = true;

        Assert.Equal(("", ""), (e.CodigoPais, e.Pais));
        Assert.False(e.TemAvisoTrocaPais);                                  // nada do Brasil digitado: nada a avisar
    }

    // ---- D-3: o código do país no exterior ----

    [Fact]
    public void Exterior_sem_codigo_do_pais_e_recusado_pela_ficha()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Nome = "John Smith";
        var e = f.Enderecos[0];
        e.NoExterior = true;
        e.Logradouro = "Main St";
        e.Cidade = "Nova York";
        e.Pais = "Estados Unidos";

        var erro = Assert.Single(f.ValidarLocalmenteComCampos(), x => x.Campo == CamposFichaPessoa.Pais);

        Assert.Equal("Endereço 1: informe o código do país (Bacen, 4 algarismos).", erro.Mensagem);
        Assert.Equal(e.Id, erro.Item);
    }

    [Fact]
    public void Exterior_com_o_codigo_do_brasil_e_recusado_pela_ficha()
    {
        var e = Exterior();
        e.CodigoPais = "1058";

        Assert.Equal("Endereço 1: 1058 é o código do Brasil. Para endereço no Brasil, desmarque \"Endereço no exterior\".",
            e.ValidarPais("Endereço 1"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("06289")]
    public void Codigo_do_pais_novo_sem_quatro_algarismos_e_recusado(string codigo)
    {
        var e = new EnderecoFormulario { NoExterior = true, Logradouro = "Main St", Cidade = "Londres", Pais = "Reino Unido" };
        e.CodigoPais = codigo;

        Assert.Equal("Endereço 1: o código do país (Bacen) deve ter 4 algarismos.", e.ValidarPais("Endereço 1"));
    }

    [Fact]
    public void Codigo_antigo_fora_do_padrao_nao_bloqueia_o_cadastro_gravado()
    {
        var e = Exterior(codigo: "249");                                    // gravado assim antes do Bloco A

        Assert.Null(e.ValidarPais("Endereço 1"));
        e.CodigoPais = "24";                                                // mexeu: vale a regra
        Assert.NotNull(e.ValidarPais("Endereço 1"));
    }

    [Fact]
    public void Codigo_do_pais_valido_e_endereco_do_brasil_passam()
    {
        Assert.Null(Exterior().ValidarPais("Endereço 1"));
        Assert.Null(Brasil().ValidarPais("Endereço 1"));
        var inativo = Exterior();
        inativo.CodigoPais = "";
        inativo.Ativo = false;
        Assert.Null(inativo.ValidarPais("Endereço 1"));                     // inativo não é conferido
    }

    // ---- Regressão: o Brasil continua consultando o CEP ----

    [Fact]
    public async Task No_brasil_o_cep_digitado_continua_consultado()
    {
        var (_, ambiente, e) = await FichaAsync();
        e.NoExterior = false;
        ambiente.Servidor.Responder(HttpStatusCode.OK, new DadosCep
        {
            Cep = "01426002", Logradouro = "Rua Oscar Freire", Bairro = "Cerqueira César", Cidade = "São Paulo", Uf = "SP", CodigoMunicipioIbge = "3550308"
        });

        e.Cep = "01426-002";
        for (var i = 0; i < 100 && ambiente.Servidor.Recebidas[^1].Caminho != "/" + Rotas.Consultas.Cep("01426002"); i++) await Task.Delay(10);

        Assert.Equal("/" + Rotas.Consultas.Cep("01426002"), ambiente.Servidor.Recebidas[^1].Caminho);
        Assert.False(e.NoExterior);
    }

    [Fact]
    public void Duplicidade_do_exterior_usa_o_codigo_postal()
    {
        var a = Exterior("SW1A 1AA");
        var b = Exterior("SW1A 1AA");

        Assert.Equal("SW1A 1AA", a.ParaComparacao().Cep);
        Assert.Equal(a.ParaComparacao().CodigoPais, b.ParaComparacao().CodigoPais);
    }
}
