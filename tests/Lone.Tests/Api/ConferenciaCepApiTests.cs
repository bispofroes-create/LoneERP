using System.Text.Json;
using Lone.Api.Erros;
using Lone.Application.Integracoes;
using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Application.Seguranca;
using Lone.Contracts.Comum;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Seguranca;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Validacao;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Api;

/// <summary>
/// F2: o contrato da conferência de CEP (POST consultas/cep/conferir), pelo caso de uso que a rota chama. Pedido inválido
/// = 400; fonte fora do ar = resultado (200), não erro; nada é gravado; a rota antiga continua com o mesmo contrato.
/// </summary>
public class ConferenciaCepApiTests
{
    private sealed class FonteDeTeste(CepFonte fonte, bool busca, Func<string, ResultadoProvedorCep> consulta,
                               Func<BuscaEnderecoCep, ResultadoBuscaProvedorCep>? buscar = null) : IProvedorCep
    {
        public CepFonte Fonte { get; } = fonte;
        public bool BuscaPorEndereco { get; } = busca;
        public Task<ResultadoProvedorCep> ConsultarPorCepAsync(string cep, CancellationToken ct = default) => Task.FromResult(consulta(cep));
        public Task<ResultadoBuscaProvedorCep> BuscarPorEnderecoAsync(BuscaEnderecoCep b, CancellationToken ct = default) =>
            Task.FromResult(buscar!(b));
    }

    private sealed class Autorizacao : IAutorizacao
    {
        public bool Permite { get; set; } = true;
        public bool Possui(string permissao) => Permite;
        public void Exigir(string permissao)
        {
            if (!Permite) throw new AcessoNegadoException(permissao);
        }
    }

    private sealed class CepAntigo : ICepConsulta
    {
        public Task<DadosCep?> ConsultarAsync(string cep, CancellationToken ct = default) =>
            Task.FromResult<DadosCep?>(cep == "35790000" ? new DadosCep { Cep = cep, Cidade = "Curvelo" } : null);
    }

    private sealed class SemCnpj : ICnpjConsulta, IInscricaoEstadualConsulta
    {
        Task<DadosCnpj?> ICnpjConsulta.ConsultarAsync(string cnpj, CancellationToken ct) => Task.FromResult<DadosCnpj?>(null);
        Task<List<InscricaoEstadualEncontrada>> IInscricaoEstadualConsulta.ConsultarAsync(string cnpj, CancellationToken ct) => Task.FromResult(new List<InscricaoEstadualEncontrada>());
    }

    private readonly Autorizacao _autorizacao = new();

    private ConsultasAppService Servico(params IProvedorCep[] fontes)
    {
        var relogio = new FakeTimeProvider();
        var conferencia = new ServicoConferenciaCep(fontes, new CacheConferenciaCep(relogio), new SugestoesCepEmitidas(relogio), relogio);
        var semCnpj = new SemCnpj();
        return new ConsultasAppService(new CepAntigo(), semCnpj, semCnpj, _autorizacao, conferencia);
    }

    private static RegistroCep Registro(string cep) => new(cep, "Rua Barão", "até 999/1000", "Centro", "Curvelo", "MG", "3120904");

    private static ConferirCepRequisicao Pedido(string? cep = "35790-000", string? uf = "mg") => new()
    {
        Cep = cep, Logradouro = "R. Barao", Numero = "150", Bairro = "Centro", Cidade = "Curvelo", Uf = uf, CodigoMunicipioIbge = "3120904"
    };

    [Fact]
    public void A_rota_nova_fica_no_grupo_de_consultas()
    {
        Assert.Equal("api/v1/consultas/cep/conferir", Rotas.Consultas.ConferirCep);
        Assert.Equal("api/v1/consultas/cep/35790000", Rotas.Consultas.Cep("35790000")); // a antiga não mudou
    }

    [Fact]
    public async Task Sucesso_devolve_a_decisao_tipada()
    {
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, c => ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro(c))));

        var d = await api.ConferirCepAsync(Pedido());

        Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado);
        Assert.Equal(CepSituacao.Conferido, d.Situacao);
        Assert.Equal(CepFonte.ViaCep, d.Fonte);
        Assert.Equal("35790000", d.CepInformado);
        Assert.Equal("até 999/1000", d.RegistroConsultado!.Faixa);
        Assert.Null(d.CepSugerido);
    }

    [Fact]
    public async Task Nao_encontrado_com_um_candidato_devolve_a_sugestao()
    {
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, _ => ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep),
            _ => ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001")])));

        var d = await api.ConferirCepAsync(Pedido());

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, d.Resultado);
        Assert.Equal("35790001", d.CepSugerido);
        Assert.Equal("35790001", Assert.Single(d.Candidatos).Cep);
    }

    [Fact]
    public async Task Nao_encontrado_sem_busca_possivel_pede_busca_pelo_endereco()
    {
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, false, _ => ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep)));

        var d = await api.ConferirCepAsync(Pedido());

        Assert.Equal(ResultadoDecisaoCep.NaoEncontrado, d.Resultado);
        Assert.True(d.DeveBuscarPorEndereco);
        Assert.Empty(d.Candidatos);
    }

    [Fact]
    public async Task Multiplos_candidatos_vem_todos_sem_escolha()
    {
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, _ => ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep),
            _ => ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790003"), Registro("35790001"), Registro("35790002")])));

        var d = await api.ConferirCepAsync(Pedido());

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, d.Resultado);
        Assert.Null(d.CepSugerido);
        Assert.Equal(["35790001", "35790002", "35790003"], d.Candidatos.Select(c => c.Cep));
    }

    [Fact]
    public async Task Busca_no_limite_leva_o_aviso_no_contrato()
    {
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, _ => ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep),
            _ => ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001")], limiteAtingido: true)));

        var d = await api.ConferirCepAsync(Pedido());

        Assert.Equal(ResultadoDecisaoCep.UmCandidato, d.Resultado);
        Assert.Equal([ServicoConferenciaCep.AvisoListaIncompleta], d.Avisos);
    }

    [Fact]
    public async Task Fonte_indisponivel_e_resultado_e_nao_excecao()
    {
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, _ => ResultadoProvedorCep.Indisponivel(CepFonte.ViaCep, "tempo esgotado")),
                          new FonteDeTeste(CepFonte.BrasilApi, false, _ => ResultadoProvedorCep.Indisponivel(CepFonte.BrasilApi, "HTTP 503")));

        var d = await api.ConferirCepAsync(Pedido());

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, d.Resultado);
        Assert.Equal(CepSituacao.FonteIndisponivel, d.Situacao);
        Assert.Contains(d.Motivos, m => m.Contains("serviço indisponível"));
        Assert.DoesNotContain(d.Motivos, m => m.Contains("tempo esgotado")); // detalhe técnico não vai para a tela
    }

    [Theory]
    [InlineData(null, "MG")]
    [InlineData("3579", "MG")]
    [InlineData("00000000", "MG")]
    [InlineData("35790000", "MGX")]
    public async Task Pedido_invalido_vira_400(string? cep, string uf)
    {
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, _ => throw new InvalidOperationException("não deveria consultar")));

        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => api.ConferirCepAsync(Pedido(cep, uf)));

        Assert.Equal(400, Problemas.De(erro)!.Status);
    }

    [Fact]
    public async Task Sem_permissao_de_ver_cadastros_vira_403()
    {
        _autorizacao.Permite = false;
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, _ => throw new InvalidOperationException("não deveria consultar")));

        var erro = await Assert.ThrowsAsync<AcessoNegadoException>(() => api.ConferirCepAsync(Pedido()));

        Assert.Equal(403, Problemas.De(erro)!.Status);
    }

    [Fact]
    public void Falha_tecnica_da_rota_antiga_continua_502()
    {
        Assert.Equal(502, Problemas.De(new ServicoExternoException("fora"))!.Status);
    }

    [Fact]
    public async Task Rota_antiga_continua_com_o_mesmo_contrato()
    {
        var api = Servico();

        Assert.Equal("Curvelo", (await api.ConsultarCepAsync("35790-000"))!.Cidade);
        Assert.Null(await api.ConsultarCepAsync("35790999"));
        await Assert.ThrowsAsync<ValidacaoException>(() => api.ConsultarCepAsync("123"));
    }

    [Fact]
    public void Decisao_viaja_em_json_com_os_enums_pelo_nome()
    {
        var dto = new DecisaoCepDto
        {
            Resultado = ResultadoDecisaoCep.VariosCandidatos, Situacao = CepSituacao.MultiplosCandidatos, CepInformado = "35790000",
            Fonte = CepFonte.ViaCep, Candidatos = [new CandidatoCepDto { Cep = "35790001", Faixa = "até 999/1000" }], Motivos = ["m"]
        };

        var json = JsonSerializer.Serialize(dto, OpcoesJson.Padrao);
        var volta = JsonSerializer.Deserialize<DecisaoCepDto>(json, OpcoesJson.Padrao)!;

        Assert.Contains("\"resultado\":\"VariosCandidatos\"", json);
        Assert.Contains("\"fonte\":\"ViaCep\"", json);
        Assert.Equal(dto.Resultado, volta.Resultado);
        Assert.Equal("até 999/1000", volta.Candidatos.Single().Faixa);
    }

    // ---- Checkpoint C: componentes no contrato (aditivo) ----

    [Fact]
    public async Task Componentes_vem_no_contrato_na_ordem_e_com_o_estado_tipado()
    {
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, c => ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, Registro(c))));
        var pedido = Pedido();
        pedido.Numero = "S/N";

        var d = await api.ConferirCepAsync(pedido);

        Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado); // parcial continua conferido
        Assert.Equal([ComponenteCep.Cep, ComponenteCep.Uf, ComponenteCep.Municipio, ComponenteCep.Logradouro, ComponenteCep.Numero],
            d.Componentes.Select(c => c.Componente));
        Assert.Equal(SituacaoComponenteCep.NaoValidavel, d.Componentes[4].Situacao);
        Assert.All(d.Componentes, c => Assert.False(string.IsNullOrWhiteSpace(c.Motivo)));
    }

    [Fact]
    public void Componentes_viajam_no_json_como_texto_e_resposta_antiga_sem_componentes_continua_valida()
    {
        var dto = new DecisaoCepDto
        {
            Resultado = ResultadoDecisaoCep.Conferido, CepInformado = "35790000",
            Componentes = [new ComponenteCepDto { Componente = ComponenteCep.Numero, Situacao = SituacaoComponenteCep.NaoValidavel, Motivo = "m" }]
        };

        var json = JsonSerializer.Serialize(dto, OpcoesJson.Padrao);
        var volta = JsonSerializer.Deserialize<DecisaoCepDto>(json, OpcoesJson.Padrao)!;
        var antiga = JsonSerializer.Deserialize<DecisaoCepDto>("""{"resultado":"Conferido","cepInformado":"35790000"}""", OpcoesJson.Padrao)!;

        Assert.Contains("\"NaoValidavel\"", json);
        Assert.Equal(SituacaoComponenteCep.NaoValidavel, Assert.Single(volta.Componentes).Situacao);
        Assert.Empty(antiga.Componentes);
        Assert.Equal(ResultadoDecisaoCep.Conferido, antiga.Resultado);
    }

    // ---- Checkpoint D: busca de CEP pelo endereço sem CEP ----

    private static BuscarCepPorEnderecoRequisicao PedidoBusca(string? uf = "mg", string? logradouro = "R. Barao") => new()
    {
        Logradouro = logradouro, Numero = "150", Bairro = "Centro", Cidade = "Curvelo", Uf = uf, CodigoMunicipioIbge = "3120904"
    };

    [Fact]
    public void A_rota_da_busca_fica_no_grupo_de_consultas()
    {
        Assert.Equal("api/v1/consultas/cep/buscar-por-endereco", Rotas.Consultas.BuscarCepPorEndereco);
        Assert.DoesNotContain(typeof(BuscarCepPorEnderecoRequisicao).GetProperties(), p => p.Name == "Cep"); // não pede CEP
    }

    [Fact]
    public async Task Busca_devolve_candidatos_com_componentes_e_unidade_sem_cep_informado_e_sem_sugestao()
    {
        var predio = new RegistroCep("35790911", "Rua Barão", "150", "Centro", "Curvelo", "MG", "3120904", "Edifício Central");
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, _ => throw new InvalidOperationException("não consulta CEP"),
            _ => ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, [Registro("35790001"), predio])));

        var d = await api.BuscarCepPorEnderecoAsync(PedidoBusca());

        Assert.Equal(ResultadoDecisaoCep.VariosCandidatos, d.Resultado);
        Assert.Equal("", d.CepInformado);
        Assert.Null(d.CepSugerido);
        Assert.Empty(d.Componentes);                                   // não há CEP informado para avaliar
        Assert.Equal(["35790001", "35790911"], d.Candidatos.Select(c => c.Cep));
        Assert.Equal("Edifício Central", d.Candidatos[1].Unidade);
        Assert.All(d.Candidatos, c =>
        {
            Assert.Equal([ComponenteCep.Uf, ComponenteCep.Municipio, ComponenteCep.Logradouro, ComponenteCep.Numero],
                c.Componentes.Select(x => x.Componente));
            Assert.DoesNotContain(c.Componentes, x => x.Situacao == SituacaoComponenteCep.Divergente);
        });
    }

    [Fact]
    public async Task Busca_com_dados_insuficientes_ou_uf_invalida_e_400()
    {
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException("não deveria buscar")));

        var semLogradouro = await Assert.ThrowsAsync<ValidacaoException>(() => api.BuscarCepPorEnderecoAsync(PedidoBusca(logradouro: null)));
        await Assert.ThrowsAsync<ValidacaoException>(() => api.BuscarCepPorEnderecoAsync(PedidoBusca(uf: "MGG")));

        Assert.Equal([ServicoConferenciaCep.MensagemDadosInsuficientes], semLogradouro.Erros);
    }

    [Fact]
    public async Task Busca_exige_permissao_de_ver_pessoas()
    {
        _autorizacao.Permite = false;
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, _ => throw new InvalidOperationException()));

        await Assert.ThrowsAsync<AcessoNegadoException>(() => api.BuscarCepPorEnderecoAsync(PedidoBusca()));
    }

    [Fact]
    public async Task Busca_com_fonte_fora_do_ar_e_200_com_fonte_indisponivel()
    {
        var api = Servico(new FonteDeTeste(CepFonte.ViaCep, true, _ => throw new InvalidOperationException(),
            _ => ResultadoBuscaProvedorCep.Indisponivel(CepFonte.ViaCep, "timeout")));

        var d = await api.BuscarCepPorEnderecoAsync(PedidoBusca());

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, d.Resultado);
        Assert.Empty(d.Candidatos);
    }
}
