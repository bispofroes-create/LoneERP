using Lone.Domain.Entidades;
using Lone.Domain.Enderecos;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

/// <summary>
/// Bloco A (país, UF e endereços do exterior), domínio: o código postal do exterior guarda letras (D-1) — maiúsculas,
/// espaços reduzidos, nada tirado em silêncio; o que não cabe na regra (até 8; A–Z, 0–9, espaço, hífen) é recusado no campo.
/// O CEP do Brasil continua só com algarismos, e o exterior continua sem UF, município e regra de CEP.
/// </summary>
public class EnderecoExteriorTests
{
    private static Pessoa ComEndereco(PessoaEndereco endereco)
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica };
        p.Estabelecimentos.Add(new Estabelecimento { Id = Guid.NewGuid(), PessoaId = p.Id, Principal = true });
        p.Enderecos.Add(endereco);
        return p;
    }

    private static PessoaEndereco Exterior(string? postal, string codigoPais = "6289", string pais = "Reino Unido") => new()
    {
        Id = Guid.NewGuid(), Logradouro = "Downing Street", Numero = "10", Cidade = "Londres", Pais = pais, CodigoPais = codigoPais, Cep = postal
    };

    [Theory]
    [InlineData("SW1A 1AA", "SW1A 1AA")]
    [InlineData("sw1a 1aa", "SW1A 1AA")]       // minúsculas
    [InlineData("  K1A   0B6 ", "K1A 0B6")]     // espaços nas pontas e repetidos
    [InlineData("1000-001", "1000-001")]        // Portugal, com hífen
    [InlineData("94043", "94043")]              // só algarismos também vale
    public void Codigo_postal_do_exterior_guarda_as_letras(string digitado, string gravado)
    {
        var p = ComEndereco(Exterior(digitado));

        PessoaNormalizador.Normalizar(p);

        Assert.Equal(gravado, p.Enderecos[0].Cep);
        Assert.Empty(PessoaValidador.ValidarComCampos(p));
    }

    [Fact]
    public void Codigo_postal_vazio_no_exterior_fica_nulo()
    {
        var p = ComEndereco(Exterior("   "));

        PessoaNormalizador.Normalizar(p);

        Assert.Null(p.Enderecos[0].Cep);
        Assert.Empty(PessoaValidador.ValidarComCampos(p));
    }

    [Theory]
    [InlineData("SW1A@1AA")]
    [InlineData("12.345")]
    [InlineData("ÄB12")]
    public void Caractere_fora_da_regra_e_recusado_no_campo_e_nao_tirado(string digitado)
    {
        var endereco = Exterior(digitado);
        var p = ComEndereco(endereco);

        PessoaNormalizador.Normalizar(p);

        Assert.Equal(digitado.ToUpperInvariant(), endereco.Cep); // nada tirado em silêncio
        var erro = Assert.Single(PessoaValidador.ValidarComCampos(p));
        Assert.Equal("Endereço 1: o código postal aceita só letras, algarismos, espaço e hífen.", erro.Mensagem);
        Assert.Equal((CamposFichaPessoa.Cep, (Guid?)endereco.Id), (erro.Campo, erro.Item));
    }

    [Fact]
    public void Codigo_postal_maior_que_a_coluna_e_recusado()
    {
        var endereco = Exterior("SW1A 1AAX");
        var p = ComEndereco(endereco);

        PessoaNormalizador.Normalizar(p);

        var erro = Assert.Single(PessoaValidador.ValidarComCampos(p));
        Assert.Equal("Endereço 1: o código postal pode ter no máximo 8 caracteres.", erro.Mensagem);
        Assert.Equal(CamposFichaPessoa.Cep, erro.Campo);
    }

    [Fact]
    public void Cep_do_brasil_continua_so_com_algarismos()
    {
        var p = ComEndereco(new PessoaEndereco
        {
            Id = Guid.NewGuid(), Logradouro = "Rua Oscar Freire", Numero = "1000", Bairro = "Cerqueira César", MunicipioId = 3550308,
            Cidade = "São Paulo", Uf = "SP", Cep = "01426-002"
        });

        PessoaNormalizador.Normalizar(p);

        Assert.Equal("01426002", p.Enderecos[0].Cep);
        Assert.True(p.Enderecos[0].EhBrasil);
    }

    [Fact]
    public void Exterior_continua_sem_uf_e_sem_municipio_e_fora_da_regra_do_endereco_completo()
    {
        var endereco = Exterior("SW1A 1AA");
        endereco.MunicipioId = 3550308;
        endereco.Uf = "SP";
        endereco.CodigoMunicipioIbge = "3550308";
        var p = ComEndereco(endereco);

        PessoaNormalizador.Normalizar(p);

        Assert.Equal((Ufs.Exterior, (int?)null, (string?)null), (endereco.Uf, endereco.MunicipioId, endereco.CodigoMunicipioIbge));
        Assert.Empty(RegrasEndereco.Faltando(endereco));
        Assert.Empty(PessoaValidador.ValidarComCampos(p));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("SW1A 1AA", true)]
    [InlineData("A-1 B", true)]
    [InlineData("SW1A 1AAX", false)]
    [InlineData("sw1a", false)] // a regra vale para o gravado (já em maiúsculas)
    [InlineData("12_34", false)]
    public void Regra_do_codigo_postal(string? codigo, bool valido)
    {
        Assert.Equal(valido, RegrasEndereco.CodigoPostalValido(codigo));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" a1 b2 ", "A1 B2")]
    public void Normalizacao_do_codigo_postal(string? codigo, string? esperado)
    {
        Assert.Equal(esperado, RegrasEndereco.NormalizarCodigoPostal(codigo));
    }
}
