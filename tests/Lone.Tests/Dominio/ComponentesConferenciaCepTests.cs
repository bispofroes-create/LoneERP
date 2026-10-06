using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Tests.Dominio;

/// <summary>
/// Checkpoint C: confiança por componente (CEP, UF, município, logradouro, número). Os componentes saem da mesma avaliação
/// que dá o resultado geral e só o descrevem: o <see cref="DecisaoCep.Resultado"/> e os motivos não mudam. Não validável e
/// não informado nunca viram divergência; bairro e complemento do endereço não são componentes.
/// </summary>
public class ComponentesConferenciaCepTests
{
    private static EnderecoConferenciaCep Endereco(string? logradouro = "R. Barao", string? numero = "150", string? bairro = "Centro",
                                                   string? cidade = "Curvelo", string? uf = "MG", string? ibge = "3120904") =>
        new("35790-000", logradouro, numero, bairro, cidade, uf, ibge);

    private static RegistroCep Registro(string? logradouro = "Rua Barão", string? complemento = "até 999/1000", string? bairro = "Centro",
                                        string? cidade = "Curvelo", string? uf = "MG", string? ibge = "3120904") =>
        new("35790000", logradouro, complemento, bairro, cidade, uf, ibge);

    private static DecisaoCep Conferir(EnderecoConferenciaCep? endereco = null, RegistroCep? registro = null) =>
        MotorCep.Decidir(endereco ?? Endereco(), RespostaConsultaCep.Encontrado(registro ?? Registro(), CepFonte.ViaCep));

    private static ConferenciaComponenteCep C(DecisaoCep d, ComponenteCep componente) => d.Componentes.Single(x => x.Componente == componente);
    private static SituacaoComponenteCep S(DecisaoCep d, ComponenteCep componente) => C(d, componente).Situacao;

    private static readonly ComponenteCep[] Ordem =
        [ComponenteCep.Cep, ComponenteCep.Uf, ComponenteCep.Municipio, ComponenteCep.Logradouro, ComponenteCep.Numero];

    // ---- Totalmente confirmado ----

    [Fact]
    public void Endereco_que_bate_tem_os_cinco_componentes_confirmados_e_o_resultado_nao_muda()
    {
        var d = Conferir();

        Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado);
        Assert.Equal(Ordem, d.Componentes.Select(c => c.Componente));
        Assert.All(d.Componentes, c => Assert.Equal(SituacaoComponenteCep.Confirmado, c.Situacao));
        Assert.Equal(["CEP conferido: corresponde ao endereço."], d.Motivos); // motivos de antes, sem frase nova
    }

    // ---- CEP ----

    [Fact]
    public void Cep_encontrado_e_confirmado_mesmo_com_endereco_divergente()
    {
        var d = Conferir(registro: Registro(logradouro: "Rua Padre Corrêa"));

        Assert.Equal(SituacaoComponenteCep.Confirmado, S(d, ComponenteCep.Cep)); // o CEP existe; o endereço é que diverge
        Assert.Contains("não confirma o endereço", C(d, ComponenteCep.Cep).Motivo);
        Assert.Equal(ResultadoDecisaoCep.Divergente, d.Resultado);
    }

    [Fact]
    public void Cep_nao_encontrado_nao_vira_confirmacao_e_o_resto_nao_e_validavel()
    {
        var d = MotorCep.Decidir(Endereco(), RespostaConsultaCep.NaoEncontrado(CepFonte.ViaCep));

        Assert.Equal(ResultadoDecisaoCep.NaoEncontrado, d.Resultado);
        Assert.Equal(Ordem, d.Componentes.Select(c => c.Componente));
        Assert.Equal(SituacaoComponenteCep.Divergente, S(d, ComponenteCep.Cep));
        Assert.All(d.Componentes.Skip(1), c => Assert.Equal(SituacaoComponenteCep.NaoValidavel, c.Situacao));
    }

    [Fact]
    public void Fonte_indisponivel_nunca_marca_divergencia()
    {
        var d = MotorCep.Decidir(Endereco(), RespostaConsultaCep.Indisponivel(CepFonte.ViaCep));

        Assert.Equal(ResultadoDecisaoCep.FonteIndisponivel, d.Resultado);
        Assert.Equal(Ordem, d.Componentes.Select(c => c.Componente));
        Assert.All(d.Componentes, c => Assert.Equal(SituacaoComponenteCep.NaoValidavel, c.Situacao));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Com_busca_os_componentes_falam_do_cep_informado_e_nao_dos_candidatos(int candidatos)
    {
        var registros = Enumerable.Range(1, candidatos).Select(i => Registro() with { Cep = $"3579000{i}" }).ToArray();

        var d = MotorCep.Decidir(Endereco(), RespostaConsultaCep.NaoEncontrado(CepFonte.ViaCep),
            RespostaBuscaEndereco.Realizada(registros, CepFonte.ViaCep));

        Assert.Equal(SituacaoComponenteCep.Divergente, S(d, ComponenteCep.Cep));
        Assert.All(d.Componentes.Skip(1), c => Assert.Equal(SituacaoComponenteCep.NaoValidavel, c.Situacao));
        Assert.Equal(candidatos == 1, d.CepSugerido is not null); // um candidato continua sendo sugestão; o resto não muda
    }

    // ---- UF ----

    [Fact]
    public void Uf_igual_e_confirmada()
    {
        Assert.Equal(SituacaoComponenteCep.Confirmado, S(Conferir(), ComponenteCep.Uf));
    }

    [Fact]
    public void Uf_diferente_e_divergente_e_o_resultado_e_divergente()
    {
        var d = Conferir(registro: Registro(uf: "BA"));

        Assert.Equal(SituacaoComponenteCep.Divergente, S(d, ComponenteCep.Uf));
        Assert.Equal(ResultadoDecisaoCep.Divergente, d.Resultado);
        Assert.Contains("O CEP informado é de outra UF (BA).", d.Motivos);
    }

    [Fact]
    public void Uf_ausente_nao_e_divergencia()
    {
        var semUfNoEndereco = Conferir(Endereco(uf: null));
        var semUfNaFonte = Conferir(registro: Registro(uf: null));

        Assert.Equal(SituacaoComponenteCep.NaoInformado, S(semUfNoEndereco, ComponenteCep.Uf));
        Assert.Equal(SituacaoComponenteCep.NaoInformado, S(semUfNaFonte, ComponenteCep.Uf));
        Assert.Equal(ResultadoDecisaoCep.Conferido, semUfNoEndereco.Resultado);
        Assert.Equal(ResultadoDecisaoCep.Conferido, semUfNaFonte.Resultado);
    }

    // ---- Município (mesma regra do motor: IBGE quando os dois têm; senão, nome) ----

    [Fact]
    public void Municipio_confirmado_pelo_ibge()
    {
        var d = Conferir();

        Assert.Equal(SituacaoComponenteCep.Confirmado, S(d, ComponenteCep.Municipio));
        Assert.Contains("código IBGE", C(d, ComponenteCep.Municipio).Motivo);
    }

    [Fact]
    public void Municipio_confirmado_pelo_nome_quando_falta_o_ibge()
    {
        var d = Conferir(Endereco(ibge: null));

        Assert.Equal(SituacaoComponenteCep.Confirmado, S(d, ComponenteCep.Municipio));
        Assert.Contains("pelo nome", C(d, ComponenteCep.Municipio).Motivo);
    }

    [Fact]
    public void Municipio_divergente_pelo_ibge_mesmo_com_o_mesmo_nome()
    {
        var d = Conferir(registro: Registro(ibge: "3119401"));

        Assert.Equal(SituacaoComponenteCep.Divergente, S(d, ComponenteCep.Municipio));
        Assert.Equal(ResultadoDecisaoCep.Divergente, d.Resultado);
    }

    [Fact]
    public void Municipio_sem_dado_para_comparar_nao_e_divergencia()
    {
        var d = Conferir(Endereco(cidade: null, ibge: null));

        Assert.Equal(SituacaoComponenteCep.NaoInformado, S(d, ComponenteCep.Municipio));
        Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado);
        Assert.Contains("O município não pôde ser comparado (falta o dado).", d.Motivos); // observação de antes
    }

    // ---- Logradouro (normalização atual, sem flexibilizar) ----

    [Fact]
    public void Logradouro_confirmado_pela_normalizacao_atual()
    {
        Assert.Equal(SituacaoComponenteCep.Confirmado, S(Conferir(Endereco(logradouro: "R. Barao")), ComponenteCep.Logradouro));
    }

    [Fact]
    public void Logradouro_diferente_e_divergente()
    {
        var d = Conferir(registro: Registro(logradouro: "Rua Padre Corrêa"));

        Assert.Equal(SituacaoComponenteCep.Divergente, S(d, ComponenteCep.Logradouro));
        Assert.Contains("O CEP informado corresponde a outro logradouro (Rua Padre Corrêa).", d.Motivos);
    }

    [Theory]
    [InlineData("Rua Pres. Vargas", "Rua Presidente Vargas")]
    [InlineData("Paulista", "Avenida Paulista")]
    [InlineData("Al. Santos", "Alameda Santos")]
    [InlineData("Rua Barão", "Rua Barão de Cocais")]
    public void Casos_conservadores_continuam_divergindo(string doEndereco, string daFonte)
    {
        var d = Conferir(Endereco(logradouro: doEndereco), Registro(logradouro: daFonte));

        Assert.Equal(SituacaoComponenteCep.Divergente, S(d, ComponenteCep.Logradouro));
        Assert.Equal(ResultadoDecisaoCep.Divergente, d.Resultado);
    }

    [Fact]
    public void Endereco_sem_logradouro_continua_divergente_como_na_regra_atual()
    {
        var d = Conferir(Endereco(logradouro: null));

        Assert.Equal(SituacaoComponenteCep.Divergente, S(d, ComponenteCep.Logradouro));
        Assert.Contains("O endereço não tem logradouro para comparar com o do CEP.", d.Motivos);
    }

    [Fact]
    public void Cep_geral_do_municipio_nao_informa_logradouro_nem_faixa()
    {
        var d = Conferir(registro: Registro(logradouro: null, complemento: null));

        Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado);
        Assert.Equal(SituacaoComponenteCep.NaoInformado, S(d, ComponenteCep.Logradouro));
        Assert.Equal(SituacaoComponenteCep.NaoInformado, S(d, ComponenteCep.Numero));
    }

    // ---- Número ----

    [Theory]
    [InlineData("960", "960", SituacaoComponenteCep.Confirmado)]                        // prédio exato
    [InlineData("960", "962", SituacaoComponenteCep.Divergente)]                        // outro número que não o do prédio
    [InlineData("1374 12 Andar", "1374", SituacaoComponenteCep.Confirmado)]
    [InlineData("até 999/1000", "150", SituacaoComponenteCep.Confirmado)]               // dentro da faixa
    [InlineData("até 999/1000", "1002", SituacaoComponenteCep.Divergente)]              // fora da faixa
    [InlineData("de 612 a 1510 - lado par", "1000", SituacaoComponenteCep.Confirmado)]  // lado par
    [InlineData("de 612 a 1510 - lado par", "1001", SituacaoComponenteCep.Divergente)]
    [InlineData("até 609 - lado ímpar", "609", SituacaoComponenteCep.Confirmado)]      // lado ímpar
    [InlineData("até 609 - lado ímpar", "608", SituacaoComponenteCep.Divergente)]
    [InlineData("até 999/1000", "S/N", SituacaoComponenteCep.NaoValidavel)]             // S/N
    [InlineData("até 999/1000", "KM 23", SituacaoComponenteCep.NaoValidavel)]           // número ilegível
    [InlineData("até 999/1000", "", SituacaoComponenteCep.NaoInformado)]                // sem número
    [InlineData("lote 5 quadra 2", "150", SituacaoComponenteCep.NaoValidavel)]          // faixa não interpretável
    [InlineData(null, "150", SituacaoComponenteCep.NaoInformado)]                       // fonte sem faixa
    [InlineData(null, "S/N", SituacaoComponenteCep.NaoInformado)]
    public void Numero_reflete_predio_faixa_lado_e_indeterminado(string? complemento, string numero, SituacaoComponenteCep esperado)
    {
        var d = Conferir(Endereco(numero: numero), Registro(complemento: complemento));

        Assert.Equal(esperado, S(d, ComponenteCep.Numero));
        // Só o número fora diverge; indeterminado nunca elimina nem torna o resultado divergente.
        Assert.Equal(esperado == SituacaoComponenteCep.Divergente ? ResultadoDecisaoCep.Divergente : ResultadoDecisaoCep.Conferido,
            d.Resultado);
    }

    [Fact]
    public void Numero_de_predio_e_de_faixa_tem_motivos_proprios()
    {
        Assert.Contains("prédio", C(Conferir(Endereco(numero: "960"), Registro(complemento: "960")), ComponenteCep.Numero).Motivo);
        Assert.Contains("faixa", C(Conferir(), ComponenteCep.Numero).Motivo);
    }

    // ---- Confirmação parcial ----

    [Fact]
    public void Conferido_com_numero_nao_validavel_continua_conferido()
    {
        var d = Conferir(Endereco(numero: "S/N"));

        Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado);
        Assert.Equal(CepSituacao.Conferido, d.Situacao);
        Assert.Equal(
            [SituacaoComponenteCep.Confirmado, SituacaoComponenteCep.Confirmado, SituacaoComponenteCep.Confirmado,
             SituacaoComponenteCep.Confirmado, SituacaoComponenteCep.NaoValidavel],
            d.Componentes.Select(c => c.Situacao));
        Assert.Contains("CEP conferido: corresponde ao endereço.", d.Motivos);
        Assert.Contains("O endereço não tem número legível; a faixa de numeração do CEP não foi conferida.", d.Motivos);
    }

    [Fact]
    public void Bairro_nao_e_componente_nem_criterio_novo_na_conferencia_direta()
    {
        var d = Conferir(registro: Registro(bairro: "Bela Vista"));

        Assert.Equal(ResultadoDecisaoCep.Conferido, d.Resultado);
        Assert.DoesNotContain(d.Componentes, c => c.Motivo.Contains("bairro", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(5, d.Componentes.Count);
    }

    // ---- Coerência entre resultado e componentes (invariantes) ----

    public static TheoryData<string?, string?, string?, string?, string?, string?> Combinacoes()
    {
        var dados = new TheoryData<string?, string?, string?, string?, string?, string?>();
        string?[] ufs = ["MG", "BA", null];
        string?[] ibges = ["3120904", "3119401", null];
        string?[] logradouros = ["Rua Barão", "Rua Padre Corrêa", null];
        string?[] complementos = ["até 999/1000", "960", "lote 5", null];
        string?[] numeros = ["150", "1002", "S/N", ""];
        foreach (var uf in ufs)
        foreach (var ibge in ibges)
        foreach (var logradouro in logradouros)
        foreach (var complemento in complementos)
        foreach (var numero in numeros)
            dados.Add(uf, ibge, logradouro, complemento, numero, null);
        dados.Add("MG", "3120904", "Rua Barão", "até 999/1000", "150", "sem-cidade");
        return dados;
    }

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void Resultado_e_componentes_nunca_discordam(string? uf, string? ibge, string? logradouro, string? complemento, string? numero,
                                                       string? variante)
    {
        var endereco = variante == "sem-cidade" ? Endereco(numero: numero, cidade: null, ibge: null) : Endereco(numero: numero);
        var d = Conferir(endereco, Registro(logradouro, complemento, uf: uf, ibge: ibge));

        Assert.Equal(Ordem, d.Componentes.Select(c => c.Componente));
        Assert.Equal(SituacaoComponenteCep.Confirmado, S(d, ComponenteCep.Cep));
        var divergentes = d.Componentes.Where(c => c.Situacao == SituacaoComponenteCep.Divergente).ToList();

        // Divergente ⇔ algum componente divergente; conferido nunca tem componente divergente.
        Assert.Equal(divergentes.Count > 0, d.Resultado == ResultadoDecisaoCep.Divergente);
        Assert.Equal(divergentes.Count == 0, d.Resultado == ResultadoDecisaoCep.Conferido);
        // Cada componente divergente é um dos motivos da divergência (a mesma avaliação, não outra regra).
        Assert.All(divergentes, c => Assert.Contains(c.Motivo, d.Motivos));
        // Não validável e não informado nunca aparecem como divergência.
        Assert.All(d.Componentes.Where(c => c.Situacao is SituacaoComponenteCep.NaoValidavel or SituacaoComponenteCep.NaoInformado),
            c => Assert.DoesNotContain(c.Motivo, divergentes.Select(x => x.Motivo)));
    }
}
