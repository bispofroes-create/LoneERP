using Lone.Application.Municipios;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

public class MunicipiosTests
{
    private static Municipio M(int id, string nome) => new()
    {
        Id = id, Nome = nome, NomeBusca = TextoBusca.Normalizar(nome), Uf = Ufs.DoMunicipio(id)!, CodigoUf = (byte)(id / 100000)
    };

    private static readonly IndiceMunicipios Indice = new(
    [
        M(3120904, "Curvelo"), M(3136504, "Juramento"), M(3162500, "São João del-Rei"),
        M(3106200, "Belo Horizonte"),
        // Nome repetido em estados diferentes
        M(3156700, "Santa Rita do Sapucaí"), M(2513703, "Santa Rita"), M(3547304, "Santa Rita do Passa Quatro"),
        M(4205407, "Florianópolis"), M(3144805, "Nova Lima"), M(3508504, "Bom Jesus dos Perdões"), M(2901007, "Bom Jesus da Lapa"),
        M(3168606, "Teófilo Otoni"), M(5300108, "Brasília"), M(2408003, "Mossoró"),
        M(3147907, "Paraopeba"), M(4118204, "Paranaguá"), M(3147006, "Paracatu"), M(3547809, "Santo André"), M(2528809, "Santo André")
    ]);

    [Theory]
    [InlineData("São João del-Rei", "SAO JOAO DEL REI")]
    [InlineData("  sao   joão DEL rei ", "SAO JOAO DEL REI")]
    [InlineData("Santa Bárbara d'Oeste", "SANTA BARBARA D OESTE")]
    [InlineData("", "")]
    public void Texto_de_busca_ignora_acentos_maiusculas_e_separadores(string texto, string esperado)
    {
        Assert.Equal(esperado, TextoBusca.Normalizar(texto));
    }

    [Theory]
    [InlineData("Curvelo", null)]
    [InlineData("CURVELO", null)]
    [InlineData("curvelo", "MG")]
    [InlineData("Curvelo - MG", null)]
    [InlineData("Curvelo/MG", null)]
    [InlineData("Curvelo (MG)", null)]
    [InlineData("Curvelo MG", "mg")]
    public void Textos_antigos_do_mesmo_municipio_viram_o_mesmo_codigo(string texto, string? uf)
    {
        var resultado = Indice.Resolver(texto, uf);

        Assert.Equal(3120904, resultado.Municipio?.Id);
        Assert.Null(resultado.Observacao);
    }

    [Fact]
    public void Codigo_IBGE_ja_gravado_tem_prioridade()
    {
        Assert.Equal(3136504, Indice.Resolver("Nome errado", "SP", "3136504").Municipio?.Id);
    }

    [Fact]
    public void Nome_em_mais_de_um_estado_sem_UF_nao_e_adivinhado()
    {
        var resultado = Indice.Resolver("Santo André", null);

        Assert.Null(resultado.Municipio);
        Assert.Contains("2 estados", resultado.Observacao);
        Assert.Equal(3547809, Indice.Resolver("Santo André", "SP").Municipio?.Id);
    }

    [Fact]
    public void Nome_inexistente_fica_para_correcao_manual_com_o_motivo()
    {
        var naUf = Indice.Resolver("Curvelo", "SP");
        var semUf = Indice.Resolver("Curvlo", null);

        Assert.Null(naUf.Municipio);
        Assert.Contains("SP", naUf.Observacao);
        Assert.Null(semUf.Municipio);
        Assert.Equal("não encontrado na tabela do IBGE", semUf.Observacao);
    }

    [Fact]
    public void UF_sai_dos_dois_primeiros_digitos_do_codigo()
    {
        Assert.Equal("MG", Ufs.DoMunicipio(3120904));
        Assert.Equal("DF", Ufs.DoMunicipio(5300108));
        Assert.Null(Ufs.DoMunicipio(9900000));
        Assert.Null(Ufs.DoMunicipio(123));
    }

    [Fact]
    public void Lista_oficial_vira_municipios_sem_codigos_invalidos_nem_repetidos()
    {
        var municipios = ServicoMunicipios.Converter(
            [new(3120904, " Curvelo "), new(3120904, "Curvelo"), new(123, "Inválido"), new(3136504, "Juramento")],
            new DateTime(2026, 9, 24));

        Assert.Equal(2, municipios.Count);
        var curvelo = municipios.Single(m => m.Id == 3120904);
        Assert.Equal(("Curvelo", "CURVELO", "MG", (byte)31), (curvelo.Nome, curvelo.NomeBusca, curvelo.Uf, curvelo.CodigoUf));
    }

    [Fact]
    public void Endereco_no_Brasil_recebe_nome_UF_e_codigo_do_IBGE_e_exige_municipio_existente()
    {
        var endereco = new PessoaEndereco { Id = Guid.NewGuid(), MunicipioId = 3120904, Cidade = "curvelo digitado", Uf = "SP" };
        var outro = new PessoaEndereco { Id = Guid.NewGuid(), MunicipioId = 9999999 };
        var pessoa = new Pessoa { NaturalidadeMunicipioId = 3136504, Enderecos = [endereco, outro] };
        var tabela = new Dictionary<int, Municipio> { [3120904] = M(3120904, "Curvelo"), [3136504] = M(3136504, "Juramento") };

        Assert.Equal(new[] { 3120904, 3136504, 9999999 }, ReferenciasMunicipio.Ids(pessoa).Order());
        var erros = ReferenciasMunicipio.Aplicar(pessoa, tabela);

        Assert.Equal(("Curvelo", "MG", "3120904"), (endereco.Cidade, endereco.Uf, endereco.CodigoMunicipioIbge));
        Assert.Equal("Endereço 2: município não encontrado na tabela do IBGE. Escolha na lista.", Assert.Single(erros));
    }

    [Fact]
    public void Endereco_no_Brasil_sem_municipio_nao_passa_e_no_exterior_pede_cidade_e_pais()
    {
        var pessoa = new Pessoa
        {
            Nome = "Ana",
            Estabelecimentos = [new Estabelecimento { Id = Guid.NewGuid(), Principal = true }],
            Enderecos =
            [
                new PessoaEndereco { Id = Guid.NewGuid(), Logradouro = "Rua A", Cidade = "Curvelo", Uf = "MG" },
                new PessoaEndereco { Id = Guid.NewGuid(), Logradouro = "Main St", CodigoPais = "2496", Pais = "" }
            ]
        };
        PessoaNormalizador.Normalizar(pessoa);

        var erros = PessoaValidador.Validar(pessoa);

        Assert.Contains("Endereço 1: escolha a UF e o município na lista.", erros);
        Assert.Contains("Endereço 2: informe a cidade.", erros);
        Assert.Contains("Endereço 2: informe o país.", erros);
        Assert.Null(pessoa.Enderecos[1].MunicipioId);
        Assert.Equal(Ufs.Exterior, pessoa.Enderecos[1].Uf);
    }
}
