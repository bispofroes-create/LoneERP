using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Application.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Entidades;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Dominio;

/// <summary>
/// F3: estado persistido da conferência do CEP no endereço. Só resultado com conclusão vira estado; o estado vale enquanto
/// os dados conferidos (CEP, UF, município, logradouro, número, bairro) não mudam; mudou sem nova conferência, volta a
/// NaoConferido, sem fonte e sem data. Salvar sem conferir não muda a data. Nada é presumido em endereço antigo.
/// </summary>
public class EstadoConferenciaCepTests
{
    private static readonly DateTime Ontem = new(2026, 10, 4, 20, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Hoje = new(2026, 10, 5, 20, 30, 0, DateTimeKind.Utc);

    private static PessoaEndereco Endereco(Guid? id = null) => new()
    {
        Id = id ?? Guid.Parse("00000000-0000-0000-0000-0000000000e1"), Cep = "35790000", Logradouro = "R. Barao", Numero = "150",
        Bairro = "Centro", MunicipioId = 3120904, Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904",
        Complemento = "Apto 1", Descricao = "Casa", Observacoes = "obs"
    };

    private static PessoaEndereco Conferido(CepSituacao situacao = CepSituacao.Conferido)
    {
        var e = Endereco();
        e.CepSituacao = situacao;
        e.CepFonte = CepFonte.ViaCep;
        e.CepConferidoEm = Ontem;
        return e;
    }

    // ---- Matriz Resultado → situação persistida ----

    [Theory]
    [InlineData(ResultadoDecisaoCep.Conferido, CepSituacao.Conferido)]
    [InlineData(ResultadoDecisaoCep.Divergente, CepSituacao.Divergente)]
    [InlineData(ResultadoDecisaoCep.NaoEncontrado, CepSituacao.NaoEncontrado)]
    [InlineData(ResultadoDecisaoCep.NenhumCandidato, CepSituacao.NaoEncontrado)]
    [InlineData(ResultadoDecisaoCep.UmCandidato, CepSituacao.NaoEncontrado)]
    [InlineData(ResultadoDecisaoCep.VariosCandidatos, CepSituacao.NaoEncontrado)]
    public void Resultado_com_conclusao_vira_situacao(ResultadoDecisaoCep resultado, CepSituacao situacao)
    {
        Assert.Equal(situacao, EstadoConferenciaCep.SituacaoPersistida(resultado));
    }

    [Fact]
    public void Fonte_indisponivel_nao_vira_situacao()
    {
        Assert.Null(EstadoConferenciaCep.SituacaoPersistida(ResultadoDecisaoCep.FonteIndisponivel));
    }

    // ---- Aplicar ----

    [Fact]
    public void Endereco_antigo_sem_situacao_continua_nao_conferido()
    {
        var novo = Endereco();

        EstadoConferenciaCep.Aplicar(novo, Endereco(), conferencia: null);

        Assert.Equal((CepSituacao.NaoConferido, (CepFonte?)null, (DateTime?)null), (novo.CepSituacao, novo.CepFonte, novo.CepConferidoEm));
    }

    [Theory]
    [InlineData(CepSituacao.Conferido)]
    [InlineData(CepSituacao.Divergente)]
    [InlineData(CepSituacao.NaoEncontrado)]
    public void Conferencia_feita_grava_situacao_fonte_e_data_da_conferencia(CepSituacao situacao)
    {
        var novo = Endereco();

        EstadoConferenciaCep.Aplicar(novo, gravado: null, new ConferenciaCepRealizada(situacao, CepFonte.BrasilApi, Hoje));

        Assert.Equal((situacao, (CepFonte?)CepFonte.BrasilApi, (DateTime?)Hoje), (novo.CepSituacao, novo.CepFonte, novo.CepConferidoEm));
    }

    [Fact]
    public void Salvar_sem_conferir_e_sem_mudar_mantem_situacao_fonte_e_a_data_original()
    {
        var novo = Endereco();

        EstadoConferenciaCep.Aplicar(novo, Conferido(), conferencia: null);

        Assert.Equal((CepSituacao.Conferido, (CepFonte?)CepFonte.ViaCep, (DateTime?)Ontem), (novo.CepSituacao, novo.CepFonte, novo.CepConferidoEm));
    }

    public static TheoryData<string> MudancasQueInvalidam() => new()
    {
        "cep", "uf", "municipio", "cidade", "logradouro", "numero", "sem-numero", "bairro", "exterior"
    };

    private static void Mudar(PessoaEndereco e, string campo)
    {
        switch (campo)
        {
            case "cep": e.Cep = "35790001"; break;
            case "uf": e.Uf = "BA"; break;
            case "municipio": e.MunicipioId = 3119401; e.CodigoMunicipioIbge = "3119401"; break;
            case "cidade": e.Cidade = "Corinto"; break;
            case "logradouro": e.Logradouro = "Rua Outra"; break;
            case "numero": e.Numero = "151"; break;
            case "sem-numero": e.Numero = "S/N"; break;
            case "bairro": e.Bairro = "Bela Vista"; break;
            case "exterior": e.CodigoPais = "2496"; break;
        }
    }

    [Theory]
    [MemberData(nameof(MudancasQueInvalidam))]
    public void Mudar_dado_conferido_sem_nova_conferencia_volta_a_nao_conferido(string campo)
    {
        var novo = Endereco();
        Mudar(novo, campo);

        EstadoConferenciaCep.Aplicar(novo, Conferido(), conferencia: null);

        Assert.Equal((CepSituacao.NaoConferido, (CepFonte?)null, (DateTime?)null), (novo.CepSituacao, novo.CepFonte, novo.CepConferidoEm));
    }

    [Fact]
    public void Numero_vazio_e_S_N_sao_dados_diferentes()
    {
        var antes = Endereco();
        antes.Numero = null;
        var depois = Endereco();
        depois.Numero = "S/N";

        Assert.True(EstadoConferenciaCep.DadosMudaram(antes, depois));
    }

    [Fact]
    public void Complemento_descricao_e_observacoes_nao_invalidam()
    {
        var novo = Endereco();
        novo.Complemento = "Sala 9";
        novo.Descricao = "Depósito";
        novo.Observacoes = "outra";

        EstadoConferenciaCep.Aplicar(novo, Conferido(), conferencia: null);

        Assert.Equal(CepSituacao.Conferido, novo.CepSituacao);
    }

    [Fact]
    public void Caixa_acento_e_pontuacao_nao_contam_como_mudanca()
    {
        var novo = Endereco();
        novo.Logradouro = "r. barão";
        novo.Cep = "35790-000";

        Assert.False(EstadoConferenciaCep.DadosMudaram(Endereco(), novo));
    }

    [Fact]
    public void Divergente_antigo_continua_divergente_so_enquanto_os_dados_sao_os_mesmos()
    {
        var mantido = Endereco();
        EstadoConferenciaCep.Aplicar(mantido, Conferido(CepSituacao.Divergente), conferencia: null);
        var corrigido = Endereco();
        corrigido.Cep = "35790001";
        EstadoConferenciaCep.Aplicar(corrigido, Conferido(CepSituacao.Divergente), conferencia: null);

        Assert.Equal(CepSituacao.Divergente, mantido.CepSituacao);   // antigo ≠ erro novo: só fica como estava
        Assert.Equal(CepSituacao.NaoConferido, corrigido.CepSituacao); // mudou: não se presume nada
    }

    [Fact]
    public void Endereco_no_exterior_nunca_tem_conferencia_de_cep()
    {
        var novo = Endereco();
        novo.CodigoPais = "2496";

        EstadoConferenciaCep.Aplicar(novo, gravado: null, new ConferenciaCepRealizada(CepSituacao.Conferido, CepFonte.ViaCep, Hoje));

        Assert.Equal(CepSituacao.NaoConferido, novo.CepSituacao);
    }

    [Fact]
    public void A_assinatura_do_pedido_da_ficha_e_a_do_endereco_gravado_batem()
    {
        // O pedido de conferência traz o CEP formatado e o município como Id; o gravado traz dígitos e o código IBGE copiado.
        var pedido = new EnderecoConferenciaCep("35790-000", "R. Barao", "150", "Centro", "Curvelo", "MG", "3120904");

        Assert.Equal(EstadoConferenciaCep.Assinatura(pedido), EstadoConferenciaCep.Assinatura(Endereco()));
    }

    // ---- No Salvar (Application): só a conferência feita pela API para os mesmos dados, e só se a ficha pediu ----

    private static ConferenciaCepRealizada? Feita(string? assinatura) =>
        assinatura == EstadoConferenciaCep.Assinatura(Endereco()) ? new ConferenciaCepRealizada(CepSituacao.Conferido, CepFonte.ViaCep, Hoje) : null;

    [Fact]
    public void No_salvar_a_conferencia_da_ficha_vale_quando_a_api_a_fez_para_os_mesmos_dados()
    {
        var novo = Endereco();

        EstadoConferenciaCepNoSalvar.Aplicar([novo], gravados: null, [new EnderecoDto { Id = novo.Id, ConferenciaCepNaFicha = true }], Feita);

        Assert.Equal((CepSituacao.Conferido, (DateTime?)Hoje), (novo.CepSituacao, novo.CepConferidoEm));
    }

    [Fact]
    public void No_salvar_conferir_mudar_o_numero_e_salvar_nao_grava_a_conferencia_antiga()
    {
        var novo = Endereco();
        novo.Numero = "151"; // conferiu com 150, mudou e salvou

        EstadoConferenciaCepNoSalvar.Aplicar([novo], [Endereco()], [new EnderecoDto { Id = novo.Id, ConferenciaCepNaFicha = true }], Feita);

        Assert.Equal(CepSituacao.NaoConferido, novo.CepSituacao);
    }

    [Fact]
    public void No_salvar_sem_pedido_da_ficha_nada_e_presumido_e_o_aplicativo_nao_inventa_estado()
    {
        var novo = Endereco();
        novo.CepSituacao = CepSituacao.Conferido; // o que viesse do aplicativo é ignorado
        novo.CepFonte = CepFonte.Correios;
        novo.CepConferidoEm = Hoje;

        EstadoConferenciaCepNoSalvar.Aplicar([novo], gravados: null, [new EnderecoDto { Id = novo.Id }], Feita);

        Assert.Equal((CepSituacao.NaoConferido, (CepFonte?)null), (novo.CepSituacao, novo.CepFonte));
    }

    [Fact]
    public void No_salvar_pedido_sem_conferencia_feita_pela_api_nao_vale()
    {
        var novo = Endereco();

        EstadoConferenciaCepNoSalvar.Aplicar([novo], gravados: null, [new EnderecoDto { Id = novo.Id, ConferenciaCepNaFicha = true }], _ => null);

        Assert.Equal(CepSituacao.NaoConferido, novo.CepSituacao);
    }

    // ---- Conferências emitidas pela API (memória, 1 h) ----

    private static DecisaoCep Decidir(RespostaConsultaCep consulta) =>
        MotorCep.Decidir(new EnderecoConferenciaCep("35790-000", "R. Barao", "150", "Centro", "Curvelo", "MG", "3120904"), consulta);

    private static readonly RegistroCep Registro = new("35790000", "Rua Barão", "até 999/1000", "Centro", "Curvelo", "MG", "3120904");

    [Fact]
    public void Conferencias_emitidas_guardam_a_conclusao_a_fonte_e_a_hora_por_uma_hora()
    {
        var relogio = new FakeTimeProvider(new DateTimeOffset(Hoje));
        var emitidas = new ConferenciasCepEmitidas(relogio);
        var pedido = new EnderecoConferenciaCep("35790-000", "R. Barao", "150", "Centro", "Curvelo", "MG", "3120904");

        emitidas.Registrar(pedido, Decidir(RespostaConsultaCep.Encontrado(Registro, CepFonte.ViaCep)));

        Assert.Equal(new ConferenciaCepRealizada(CepSituacao.Conferido, CepFonte.ViaCep, Hoje),
            emitidas.Obter(EstadoConferenciaCep.Assinatura(Endereco())));
        relogio.Advance(ConferenciasCepEmitidas.Validade);
        Assert.Null(emitidas.Obter(EstadoConferenciaCep.Assinatura(Endereco())));
    }

    [Fact]
    public void Fonte_indisponivel_nao_e_registrada_como_conferencia()
    {
        var emitidas = new ConferenciasCepEmitidas(new FakeTimeProvider(new DateTimeOffset(Hoje)));
        var pedido = new EnderecoConferenciaCep("35790-000", "R. Barao", "150", "Centro", "Curvelo", "MG", "3120904");

        emitidas.Registrar(pedido, Decidir(RespostaConsultaCep.Indisponivel(CepFonte.ViaCep)));

        Assert.Null(emitidas.Obter(EstadoConferenciaCep.Assinatura(Endereco())));
    }
}
