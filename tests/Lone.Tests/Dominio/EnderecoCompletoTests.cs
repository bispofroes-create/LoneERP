using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Integracoes;
using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Domain.Pessoas;

namespace Lone.Tests.Dominio;

/// <summary>
/// Endereço completo: no Brasil, endereço novo ou alterado precisa de CEP, número ("S/N" vale) e bairro; o gravado que não
/// mudou não impede gravar (vira pendência no resumo). O CEP conferido na consulta: inexistente ou de outro município recusa.
/// </summary>
public class EnderecoCompletoTests
{
    private static PessoaEndereco Endereco(string? cep = null, string? numero = null, string? bairro = null) => new()
    {
        Id = Guid.NewGuid(), Logradouro = "Rua A", Cep = cep, Numero = numero, Bairro = bairro, MunicipioId = 3120904, Cidade = "Curvelo",
        Uf = "MG", CodigoPais = PessoaEndereco.CodigoPaisBrasil, Pais = "Brasil"
    };

    private static PessoaEndereco Copia(PessoaEndereco e) => new()
    {
        Id = e.Id, Ativo = e.Ativo, Logradouro = e.Logradouro, Cep = e.Cep, Numero = e.Numero, Bairro = e.Bairro, MunicipioId = e.MunicipioId,
        Cidade = e.Cidade, Uf = e.Uf, CodigoPais = e.CodigoPais, Pais = e.Pais
    };

    [Fact]
    public void Endereco_novo_sem_cep_numero_e_bairro_nao_grava_e_cada_erro_leva_ao_campo()
    {
        var novo = Endereco();

        var erros = RegrasEndereco.ValidarCompletos([novo], []);

        Assert.Equal([CamposFichaPessoa.Cep, CamposFichaPessoa.Numero, CamposFichaPessoa.Bairro], erros.Select(e => e.Campo));
        Assert.All(erros, e => Assert.Equal(novo.Id, e.Item));
        Assert.Contains(erros, e => e.Mensagem == "Endereço 1: informe o número ou marque \"Sem número\".");
    }

    [Theory]
    [InlineData("SN")]
    [InlineData("s/n")]
    [InlineData("S.N.")]
    [InlineData("sem número")]
    [InlineData("-")] // só pontuação: já é "sem número" (vira S/N, nem chega a mostrar a dica)
    public void Qualquer_forma_de_sem_numero_vira_SN(string digitado)
    {
        Assert.Equal("S/N", RegrasEndereco.NormalizarNumero(digitado));
        Assert.Equal("100", RegrasEndereco.NormalizarNumero("100"));
        Assert.Null(RegrasEndereco.NormalizarNumero(null)); // vazio continua vazio (e a regra pede o número)
    }

    [Theory]
    [InlineData("casa", true)]
    [InlineData("x", true)]
    [InlineData("0", true)]
    [InlineData("00", true)]
    [InlineData("100A", false)]
    [InlineData("KM 23", false)]
    [InlineData("Lote 5 Quadra 3", false)]
    [InlineData("S/N", false)] // já é sem número
    [InlineData("", false)]
    public void So_sugere_sem_numero_quando_o_texto_nao_parece_numero(string digitado, bool sugere)
    {
        Assert.Equal(sugere, RegrasEndereco.PareceSemNumero(digitado));
    }

    [Fact]
    public void Dica_some_ao_marcar_sem_numero()
    {
        var e = new EnderecoFormulario { Numero = "casa" };
        Assert.True(e.SugerirSemNumero);

        e.MarcarSemNumeroCommand.Execute(null);

        Assert.False(e.SugerirSemNumero);
        Assert.Equal("S/N", e.Numero);
    }

    [Fact]
    public void Caixa_sem_numero_poe_SN_e_desmarcar_volta_vazio()
    {
        var e = new EnderecoFormulario();

        e.SemNumero = true;
        Assert.Equal("S/N", e.Numero);

        e.SemNumero = false;
        Assert.Equal(string.Empty, e.Numero);

        e.Numero = "sn"; // digitado: marca a caixa e padroniza
        Assert.True(e.SemNumero);
        Assert.Equal("S/N", e.Numero);
    }

    [Fact]
    public void Endereco_completo_ou_com_SN_grava()
    {
        Assert.Empty(RegrasEndereco.ValidarCompletos([Endereco("35790000", "S/N", "Centro")], []));
    }

    [Fact]
    public void Endereco_gravado_incompleto_que_nao_mudou_nao_impede_gravar_mas_alterado_precisa_completar()
    {
        var gravado = Endereco(cep: "35790000");

        Assert.Empty(RegrasEndereco.ValidarCompletos([Copia(gravado)], [gravado]));

        var alterado = Copia(gravado);
        alterado.Logradouro = "Rua B";
        Assert.Equal([CamposFichaPessoa.Numero, CamposFichaPessoa.Bairro], RegrasEndereco.ValidarCompletos([alterado], [gravado]).Select(e => e.Campo));

        var semCep = Copia(gravado);
        semCep.Cep = null; // apagar o CEP é alterar
        Assert.Contains(RegrasEndereco.ValidarCompletos([semCep], [gravado]), e => e.Campo == CamposFichaPessoa.Cep);
    }

    [Fact]
    public void Exterior_e_inativo_nao_entram_na_regra()
    {
        var exterior = Endereco();
        exterior.CodigoPais = "2496"; // cPais NF-e dos Estados Unidos (249 é o código RFB, outro sistema)
        exterior.Pais = "Estados Unidos";
        var inativo = Endereco();
        inativo.Ativo = false;

        Assert.Empty(RegrasEndereco.ValidarCompletos([exterior, inativo], []));
        Assert.Empty(RegrasEndereco.Faltando(exterior));
        Assert.Empty(RegrasEndereco.Faltando(inativo));
    }

    [Fact]
    public void Ficha_recusa_endereco_novo_incompleto_antes_de_chamar_a_api()
    {
        var f = PessoaFormulario.NovaPessoa();
        var endereco = f.Enderecos.First();
        endereco.Logradouro = "Rua A";
        endereco.Municipio.Definir(3120904, "Curvelo", "MG");

        var erros = f.ValidarLocalmenteComCampos();

        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.Cep && e.Item == endereco.Id);
        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.Numero && e.Item == endereco.Id);
        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.Bairro && e.Item == endereco.Id);
    }

    [Fact]
    public void Cep_inexistente_na_consulta_avisa_no_campo_e_recusa_ate_mudar_o_cep()
    {
        var e = new EnderecoFormulario { Cep = "99999-999" };

        e.MarcarCepInexistente();

        Assert.True(e.TemAvisoCep);
        Assert.Equal("Endereço 1: o CEP 99999-999 não foi encontrado na consulta de CEP. Confira o número.", e.ValidarCep("Endereço 1"));

        e.Cep = "35790-000"; // outro CEP: a conferência anterior não vale mais
        Assert.False(e.TemAvisoCep);
        Assert.Null(e.ValidarCep("Endereço 1"));
    }

    [Fact]
    public void Cep_de_outro_municipio_que_nao_o_escolhido_recusa()
    {
        var e = new EnderecoFormulario();
        e.AplicarCep(new DadosCep { Cep = "35790000", Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904", Bairro = "Centro" });
        Assert.Null(e.ValidarCep("Endereço 1"));

        e.Municipio.Definir(3106200, "Belo Horizonte", "MG");

        Assert.Equal("Endereço 1: o CEP 35790-000 é de Curvelo/MG, mas o município escolhido é outro. Confira o CEP ou o município.",
            e.ValidarCep("Endereço 1"));
    }

    [Fact]
    public void Sem_consulta_o_cep_nao_e_conferido()
    {
        var e = new EnderecoFormulario { Cep = "35790-000" };

        Assert.Null(e.ValidarCep("Endereço 1")); // sem internet ou CEP gravado antes: a gravação segue (só o formato é da API)
    }

    [Fact]
    public void Endereco_incompleto_vira_pendencia_que_leva_ao_primeiro_campo_que_falta()
    {
        var f = PessoaFormulario.NovaPessoa();
        var endereco = f.Enderecos.First();
        endereco.Logradouro = "Rua A";
        endereco.Cep = "35790-000";
        endereco.Municipio.Definir(3120904, "Curvelo", "MG");
        var destinos = new List<DestinoFicha>();
        var resumo = new ResumoPessoa();

        resumo.Atualizar(f, destinos.Add);
        var item = resumo.Blocos.SelectMany(b => b.Itens).Single(i => i.Texto == "Endereço incompleto (falta número, bairro)");
        item.IrCommand!.Execute(null);

        Assert.Equal(new DestinoFicha(SecaoPessoa.Enderecos, CamposFichaPessoa.Numero, endereco.Id), Assert.Single(destinos));
    }
}
