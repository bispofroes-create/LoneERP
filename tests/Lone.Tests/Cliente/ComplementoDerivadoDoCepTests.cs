using System.Net;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comum;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>
/// V2-0.1 (07/10/2026): o complemento que a consulta de CEP escreveu (no ViaCEP, a faixa "de 607 a 1289 - lado ímpar" ou o
/// número do prédio) é dado derivado daquele CEP. Trocar de CEP, por "Usar" um candidato do "Não sei o CEP" ou por outra
/// consulta, troca ou limpa só esse texto, enquanto o usuário não mexer nele. Complemento digitado, editado ou vindo do
/// cadastro nunca é apagado. Exterior não é afetado.
/// </summary>
public class ComplementoDerivadoDoCepTests
{
    private static readonly string CaminhoBusca = "/" + Rotas.Consultas.BuscarCepPorEndereco;
    private static readonly string CaminhoConferir = "/" + Rotas.Consultas.ConferirCep;

    private const string FaixaImpar = "de 607 a 1289 - lado ímpar";
    private const string FaixaPar = "de 610 a 1290 - lado par";

    /// <summary>Ficha nova de pessoa física com o endereço no Brasil (Curvelo/MG), número 1000 e complemento vazio.</summary>
    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente, EnderecoFormulario Endereco)> FichaAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        await tela.NovoCommand.ExecuteAsync(null);
        var f = tela.Formulario!;
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Fisica);
        f.Nome = "Ana";
        var e = f.Enderecos[0];
        e.Municipio.Definir(3120904, "Curvelo", "MG");
        e.Logradouro = "R. Barao";
        e.Numero = "1000";
        e.Bairro = "Centro";
        return (tela, ambiente, e);
    }

    /// <summary>A resposta da consulta de CEP (a mesma que a ficha recebe de <c>GET consultas/cep/{cep}</c>).</summary>
    private static DadosCep Consulta(string cep, string? complemento) => new()
    {
        Cep = cep, Logradouro = "Rua Barão", Complemento = complemento, Bairro = "Centro", Cidade = "Curvelo", Uf = "MG",
        CodigoMunicipioIbge = "3120904"
    };

    private static CandidatoCepDto Candidato(string cep) =>
        new() { Cep = cep, Logradouro = "Rua Barão", Faixa = FaixaPar, Bairro = "Centro", Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904" };

    private static DecisaoCepDto Busca(params string[] ceps) => new()
    {
        Resultado = ResultadoDecisaoCep.UmCandidato, CepInformado = "", Fonte = CepFonte.ViaCep, Candidatos = ceps.Select(Candidato).ToList(),
        Motivos = ["Encontramos um CEP compatível com os dados informados. Confira antes de usar."]
    };

    private static DecisaoCepDto Conferido(string cep) => new()
    {
        Resultado = ResultadoDecisaoCep.Conferido, CepInformado = cep, Fonte = CepFonte.ViaCep,
        Motivos = ["O CEP corresponde ao endereço informado."], RegistroConsultado = Candidato(cep)
    };

    /// <summary>"Não sei o CEP" com o CEP apagado (como na homologação) e "Usar" no candidato; espera a conferência oficial.</summary>
    private static async Task UsarCandidatoDoNaoSeiOCepAsync(AmbienteCliente ambiente, EnderecoFormulario e, string cep)
    {
        e.Cep = string.Empty;
        ambiente.Servidor.ResponderEm(CaminhoBusca, HttpStatusCode.OK, Busca(cep));
        await e.BuscarCepPorEnderecoCommand.ExecuteAsync(null);
        ambiente.Servidor.ResponderEm(CaminhoConferir, HttpStatusCode.OK, Conferido(cep));
        e.CandidatosCep.Single().UsarCommand.Execute(null);
        await e.ConferenciaAposUsar.WaitAsync(TimeSpan.FromSeconds(5));
    }

    // ---- 1. A faixa do CEP anterior não fica depois de "Usar" outro CEP ----

    [Fact]
    public async Task Faixa_escrita_pela_consulta_sai_ao_usar_outro_cep_do_nao_sei_o_cep()
    {
        var (_, ambiente, e) = await FichaAsync();
        e.AplicarCep(Consulta("35790003", FaixaImpar));
        Assert.Equal(FaixaImpar, e.Complemento);                           // a consulta continua preenchendo o vazio

        await UsarCandidatoDoNaoSeiOCepAsync(ambiente, e, "35790002");

        Assert.Equal("35790-002", e.Cep);
        Assert.Equal(string.Empty, e.Complemento);                         // a faixa do 35790-003 não vale para o 35790-002
        Assert.Equal(("Rua Barão", "1000", "Centro", 3120904), (e.Logradouro, e.Numero, e.Bairro, e.Municipio.MunicipioId));
        Assert.Null(e.ParaDto(0).Complemento);                            // e não vai para o Salvar
    }

    // ---- 2. Complemento editado pelo usuário depois da consulta: fica ----

    [Theory]
    [InlineData("Apto 5")]
    [InlineData(FaixaImpar + " fundos")]   // editado a partir da faixa: já é do usuário
    public async Task Complemento_editado_pelo_usuario_nao_sai_ao_usar_outro_cep(string editado)
    {
        var (_, ambiente, e) = await FichaAsync();
        e.AplicarCep(Consulta("35790003", FaixaImpar));
        e.Complemento = editado;

        await UsarCandidatoDoNaoSeiOCepAsync(ambiente, e, "35790002");

        Assert.Equal("35790-002", e.Cep);
        Assert.Equal(editado, e.Complemento);
    }

    [Fact]
    public async Task Complemento_digitado_antes_de_qualquer_consulta_nao_sai_ao_usar_outro_cep()
    {
        var (_, ambiente, e) = await FichaAsync();
        e.Complemento = "Casa 2";
        e.AplicarCep(Consulta("35790003", FaixaImpar));                  // não sobrescreve o digitado (como antes)
        Assert.Equal("Casa 2", e.Complemento);

        await UsarCandidatoDoNaoSeiOCepAsync(ambiente, e, "35790002");

        Assert.Equal("Casa 2", e.Complemento);
    }

    [Fact]
    public async Task Usuario_que_redigita_o_texto_da_faixa_fica_com_o_que_digitou()
    {
        var (_, ambiente, e) = await FichaAsync();
        e.AplicarCep(Consulta("35790003", FaixaImpar));
        e.Complemento = "de 607";                                          // mexeu: o texto passa a ser do usuário
        e.Complemento = FaixaImpar;                                        // mesmo que volte ao texto da faixa

        await UsarCandidatoDoNaoSeiOCepAsync(ambiente, e, "35790002");

        Assert.Equal(FaixaImpar, e.Complemento);
    }

    [Fact]
    public async Task Usar_o_proprio_cep_da_consulta_mantem_a_faixa_dele()
    {
        var (_, ambiente, e) = await FichaAsync();
        e.AplicarCep(Consulta("35790002", FaixaPar));

        await UsarCandidatoDoNaoSeiOCepAsync(ambiente, e, "35790002");     // o candidato é o mesmo CEP da faixa

        Assert.Equal(("35790-002", FaixaPar), (e.Cep, e.Complemento));
    }

    [Fact]
    public async Task Usar_a_sugestao_do_conferir_cep_tambem_tira_a_faixa_do_cep_anterior()
    {
        var (_, _, e) = await FichaAsync();
        e.AplicarCep(Consulta("35790003", FaixaImpar));
        e.MostrarConferencia(new DecisaoCepDto                             // "Conferir CEP": o 35790-003 não é o do endereço
        {
            Resultado = ResultadoDecisaoCep.UmCandidato, CepInformado = "35790003", Fonte = CepFonte.ViaCep,
            Candidatos = [Candidato("35790002")], Motivos = ["O CEP informado é de outra faixa. Sugestão: 35790-002."]
        });

        e.CandidatosCep.Single().UsarCommand.Execute(null);

        Assert.Equal(("35790-002", string.Empty), (e.Cep, e.Complemento));
    }

    // ---- 3. Reconsulta do mesmo CEP não apaga o complemento do usuário ----

    [Fact]
    public async Task Consultar_de_novo_o_mesmo_cep_nao_apaga_o_complemento_do_usuario()
    {
        var (_, _, e) = await FichaAsync();
        e.AplicarCep(Consulta("35790003", FaixaImpar));
        e.Complemento = "Loja 1";

        e.AplicarCep(Consulta("35790003", FaixaImpar));                  // "Buscar CEP" de novo, mesmo número

        Assert.Equal("Loja 1", e.Complemento);
    }

    [Fact]
    public async Task Consultar_de_novo_o_mesmo_cep_mantem_a_faixa_que_a_consulta_escreveu()
    {
        var (_, _, e) = await FichaAsync();
        e.AplicarCep(Consulta("35790003", FaixaImpar));

        e.AplicarCep(Consulta("35790003", FaixaImpar));

        Assert.Equal(FaixaImpar, e.Complemento);
    }

    // ---- 4. Fluxo tradicional de digitar o CEP ----

    [Fact]
    public async Task Outra_consulta_troca_a_faixa_escrita_pela_anterior_e_limpa_se_nao_houver()
    {
        var (_, _, e) = await FichaAsync();
        e.AplicarCep(Consulta("35790003", FaixaImpar));

        e.AplicarCep(Consulta("35790002", FaixaPar));                     // outro CEP digitado: a faixa dele
        Assert.Equal(FaixaPar, e.Complemento);

        e.AplicarCep(Consulta("35790000", complemento: null));            // CEP sem faixa: a anterior não fica
        Assert.Equal(string.Empty, e.Complemento);

        e.AplicarCep(Consulta("35790003", FaixaImpar));                   // vazio volta a receber, como sempre
        Assert.Equal(FaixaImpar, e.Complemento);
    }

    [Fact]
    public async Task Outra_consulta_nao_toca_no_complemento_do_usuario()
    {
        var (_, _, e) = await FichaAsync();
        e.Complemento = "Bloco B";

        e.AplicarCep(Consulta("35790003", FaixaImpar));
        e.AplicarCep(Consulta("35790002", FaixaPar));

        Assert.Equal("Bloco B", e.Complemento);
    }

    // ---- 5. "Não sei o CEP" continua passando pela conferência oficial depois de "Usar" ----

    [Fact]
    public async Task Usar_continua_chamando_a_conferencia_oficial_uma_vez()
    {
        var (_, ambiente, e) = await FichaAsync();
        e.AplicarCep(Consulta("35790003", FaixaImpar));

        await UsarCandidatoDoNaoSeiOCepAsync(ambiente, e, "35790002");

        Assert.Equal(1, ambiente.Servidor.Recebidas.Count(r => r.Caminho == CaminhoConferir));
        Assert.Contains("O CEP corresponde ao endereço informado.", e.TextoConferenciaCep);
        Assert.True(e.ConferenciaCepVigente);
    }

    // ---- 6. Endereço comum sem complemento ----

    [Fact]
    public async Task Cep_sem_complemento_continua_sem_complemento_ao_usar_outro_cep()
    {
        var (_, ambiente, e) = await FichaAsync();
        e.AplicarCep(Consulta("35790000", complemento: null));
        Assert.Equal(string.Empty, e.Complemento);

        await UsarCandidatoDoNaoSeiOCepAsync(ambiente, e, "35790002");

        Assert.Equal(("35790-002", string.Empty, "1000"), (e.Cep, e.Complemento, e.Numero));
    }

    // ---- 7. Exterior não é afetado ----

    [Fact]
    public async Task Ir_ao_exterior_e_voltar_nao_mexe_no_complemento()
    {
        var (_, _, e) = await FichaAsync();
        e.AplicarCep(Consulta("35790003", FaixaImpar));

        e.NoExterior = true;
        Assert.Equal(FaixaImpar, e.Complemento);                           // troca sem perda (Bloco A)
        e.NoExterior = false;

        Assert.Equal(FaixaImpar, e.Complemento);
    }

    [Fact]
    public async Task No_exterior_o_complemento_digitado_fica_e_nenhum_cep_e_consultado()
    {
        var (_, ambiente, e) = await FichaAsync();
        e.NoExterior = true;
        e.Complemento = "Flat 3";
        var antes = ambiente.Servidor.Recebidas.Count;

        await e.BuscarCepCommand.ExecuteAsync(null);

        Assert.Equal("Flat 3", e.Complemento);
        Assert.Equal(antes, ambiente.Servidor.Recebidas.Count);
    }
}
