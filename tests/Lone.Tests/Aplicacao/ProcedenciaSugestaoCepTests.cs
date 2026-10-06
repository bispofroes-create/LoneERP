using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Application.Pessoas;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Entidades;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// F2, DM3: o CEP aplicado a partir da sugestão é alteração do usuário; a fonte da sugestão vira frase de evento só se a
/// marca do aplicativo for coerente (CEP salvo = sugerido, mudou agora e a sugestão saiu da API). O aplicativo não
/// consegue inventar a procedência.
/// </summary>
public class ProcedenciaSugestaoCepTests
{
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
    private readonly SugestoesCepEmitidas _emitidas;
    private readonly Guid _id = Guid.NewGuid();

    public ProcedenciaSugestaoCepTests()
    {
        _emitidas = new SugestoesCepEmitidas(_relogio);
        _emitidas.Registrar("35790000", "35790001", CepFonte.ViaCep); // o que a API emitiu na conferência
    }

    private PessoaEndereco Endereco(string cep, bool ativo = true, string codigoPais = PessoaEndereco.CodigoPaisBrasil) => new()
    {
        Id = _id, Cep = cep, Logradouro = "Rua Barão", Numero = "150", Bairro = "Centro", Cidade = "Curvelo", Uf = "MG",
        Ativo = ativo, CodigoPais = codigoPais
    };

    private EnderecoDto Enviado(SugestaoCepAplicadaDto? marca) => new() { Id = _id, SugestaoCepAplicada = marca };

    private static SugestaoCepAplicadaDto Marca(string conferido = "35790-000", string sugerido = "35790-001", CepFonte fonte = CepFonte.ViaCep) =>
        new() { CepConferido = conferido, CepSugerido = sugerido, Fonte = fonte };

    private List<string> Frases(SugestaoCepAplicadaDto? marca, PessoaEndereco atual, PessoaEndereco? gravado) =>
        ProcedenciaSugestaoCep.Frases([Enviado(marca)], [atual], gravado is null ? null : new[] { gravado }, _emitidas.FoiEmitida);

    [Fact]
    public void Marca_coerente_vira_a_frase_com_a_fonte_e_os_dois_ceps()
    {
        var frase = Assert.Single(Frases(Marca(), Endereco("35790001"), Endereco("35790000")));

        Assert.Equal("Endereço 'Rua Barão, 150 - Centro - Curvelo/MG': CEP 35790-000 → 35790-001 aplicado pelo usuário a partir da " +
                     "sugestão da conferência de CEP (fonte: ViaCEP).", frase);
    }

    [Fact]
    public void Endereco_novo_com_a_sugestao_tambem_registra()
    {
        Assert.Single(Frases(Marca(), Endereco("35790001"), gravado: null));
    }

    [Fact]
    public void Cep_salvo_diferente_do_sugerido_nao_registra()
    {
        Assert.Empty(Frases(Marca(), Endereco("35790009"), Endereco("35790000")));
    }

    [Fact]
    public void Cliente_nao_inventa_procedencia_que_a_api_nao_emitiu()
    {
        Assert.Empty(Frases(Marca(sugerido: "35790009"), Endereco("35790009"), Endereco("35790000")));     // par não emitido
        Assert.Empty(Frases(Marca(fonte: CepFonte.BrasilApi), Endereco("35790001"), Endereco("35790000"))); // outra fonte
        Assert.Empty(Frases(Marca(conferido: "35790005"), Endereco("35790001"), Endereco("35790000")));     // outro CEP conferido
        Assert.Empty(Frases(Marca(fonte: (CepFonte)99), Endereco("35790001"), Endereco("35790000")));       // fonte inexistente
    }

    [Fact]
    public void Sugestao_vencida_nao_registra_e_o_cep_grava_normalmente()
    {
        _relogio.Advance(SugestoesCepEmitidas.Validade + TimeSpan.FromMinutes(1));

        Assert.Empty(Frases(Marca(), Endereco("35790001"), Endereco("35790000")));
    }

    [Fact]
    public void Cep_que_nao_mudou_nesta_gravacao_nao_registra_de_novo()
    {
        Assert.Empty(Frases(Marca(), Endereco("35790001"), Endereco("35790001")));
    }

    [Fact]
    public void Endereco_inativo_ou_no_exterior_nao_registra()
    {
        Assert.Empty(Frases(Marca(), Endereco("35790001", ativo: false), Endereco("35790000")));
        Assert.Empty(Frases(Marca(), Endereco("35790001", codigoPais: "2496"), Endereco("35790000")));
    }

    [Fact]
    public void Marca_mal_formada_nao_registra()
    {
        Assert.Empty(Frases(Marca(sugerido: "123"), Endereco("35790001"), Endereco("35790000")));
        Assert.Empty(Frases(Marca(conferido: "35790001", sugerido: "35790001"), Endereco("35790001"), Endereco("35790000")));
        Assert.Empty(Frases(null, Endereco("35790001"), Endereco("35790000")));
    }

    [Fact]
    public void A_marca_nao_e_gravada_no_endereco()
    {
        Assert.DoesNotContain(typeof(PessoaEndereco).GetProperties(), p => p.Name.Contains("Sugestao"));
    }

    // ---- Checkpoint D: CEP escolhido na busca pelo endereço sem CEP (mesma infraestrutura DM3) ----

    private static SugestaoCepAplicadaDto MarcaDaBusca(string sugerido = "35790-002", CepFonte fonte = CepFonte.ViaCep) =>
        new() { CepConferido = "", CepSugerido = sugerido, Fonte = fonte };

    [Fact]
    public void Cep_escolhido_na_busca_sem_cep_registra_a_frase_da_busca()
    {
        _emitidas.Registrar("", "35790002", CepFonte.ViaCep); // o que a busca emitiu

        var frase = Assert.Single(Frases(MarcaDaBusca(), Endereco("35790002"), Endereco("")));

        Assert.Equal("Endereço 'Rua Barão, 150 - Centro - Curvelo/MG': CEP 35790-002 escolhido pelo usuário entre os candidatos da " +
                     "busca de CEP pelo endereço (fonte: ViaCEP).", frase);
    }

    [Fact]
    public void Busca_sem_cep_tambem_nao_aceita_procedencia_que_a_api_nao_emitiu()
    {
        _emitidas.Registrar("", "35790002", CepFonte.ViaCep);

        Assert.Empty(Frases(MarcaDaBusca(sugerido: "35790009"), Endereco("35790009"), Endereco("")));       // não emitido
        Assert.Empty(Frases(MarcaDaBusca(fonte: CepFonte.BrasilApi), Endereco("35790002"), Endereco(""))); // outra fonte
        Assert.Empty(Frases(MarcaDaBusca(), Endereco("35790001"), Endereco("")));                          // salvo ≠ escolhido
        Assert.Empty(Frases(MarcaDaBusca(), Endereco("35790002"), Endereco("35790002")));                  // não mudou agora
        // O par da conferência não vale como busca (e vice-versa): as chaves são diferentes.
        Assert.Empty(Frases(MarcaDaBusca(sugerido: "35790-001"), Endereco("35790001"), Endereco("")));
    }

    [Fact]
    public void Cep_conferido_que_nao_e_cep_nem_vazio_continua_incoerente()
    {
        _emitidas.Registrar("", "35790002", CepFonte.ViaCep);

        Assert.Empty(Frases(Marca(conferido: "123", sugerido: "35790-002"), Endereco("35790002"), Endereco("")));
    }
}
