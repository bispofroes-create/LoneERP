using System.Reflection;
using Lone.Contracts.Pessoas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

/// <summary>Lone Contextual, Fase 1: o erro diz o campo da ficha (e o registro da lista), sem mudar os textos de sempre.</summary>
public class ErrosComCampoTests
{
    [Fact]
    public void Os_textos_continuam_os_mesmos_e_na_mesma_ordem()
    {
        var p = new Pessoa { Nome = "", Natureza = NaturezaPessoa.Fisica, DocumentoPrincipal = "12345678900" };
        p.Enderecos.Add(new PessoaEndereco { Id = Guid.NewGuid(), Logradouro = "", Ativo = true });

        var comCampos = PessoaValidador.ValidarComCampos(p);

        Assert.Equal(PessoaValidador.Validar(p), comCampos.Mensagens());
        Assert.Equal(PessoaValidador.Validar(p), comCampos.Select(e => e.Mensagem));
    }

    [Fact]
    public void Cpf_invalido_aponta_o_documento_da_identificacao()
    {
        var p = new Pessoa { Nome = "Ana", Natureza = NaturezaPessoa.Fisica, DocumentoPrincipal = "12345678900" };

        var erro = Assert.Single(PessoaValidador.ValidarComCampos(p), e => e.Mensagem == "CPF inválido.");

        Assert.Equal(CamposFichaPessoa.Documento, erro.Campo);
        Assert.Null(erro.Item);
    }

    [Fact]
    public void Erro_de_endereco_leva_o_id_do_endereco_e_nao_a_posicao()
    {
        var p = new Pessoa { Nome = "Ana" };
        var primeiro = new PessoaEndereco { Id = Guid.NewGuid(), Logradouro = "Rua A", Ativo = true, MunicipioId = 3550308 };
        var segundo = new PessoaEndereco { Id = Guid.NewGuid(), Logradouro = "", Ativo = true };
        p.Enderecos.AddRange([primeiro, segundo]);

        var erros = PessoaValidador.ValidarComCampos(p);

        var logradouro = Assert.Single(erros, e => e.Campo == CamposFichaPessoa.Logradouro);
        Assert.Equal(segundo.Id, logradouro.Item);
        Assert.StartsWith("Endereço 2:", logradouro.Mensagem); // o texto continua dizendo a posição para o usuário
        var municipio = Assert.Single(erros, e => e.Campo == CamposFichaPessoa.Municipio);
        Assert.Equal(segundo.Id, municipio.Item);
    }

    [Fact]
    public void Cnpj_do_principal_aponta_a_identificacao_e_o_da_filial_aponta_o_cartao_dela()
    {
        var p = new Pessoa { Nome = "ABC", Natureza = NaturezaPessoa.Juridica, DocumentoPrincipal = "11222333" };
        var principal = new Estabelecimento { Id = Guid.NewGuid(), Principal = true, Cnpj = null };
        var filial = new Estabelecimento { Id = Guid.NewGuid(), Principal = false, Cnpj = "11222333000299" };
        p.Estabelecimentos.AddRange([principal, filial]);

        var erros = PessoaValidador.ValidarComCampos(p);

        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.Documento && e.Item is null && e.Mensagem.Contains("informe o CNPJ"));
        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.Cnpj && e.Item == filial.Id && e.Mensagem.Contains("CNPJ inválido"));
    }

    [Fact]
    public void Excecao_so_com_textos_tem_itens_sem_campo_e_com_itens_tem_os_textos()
    {
        var textos = new ValidacaoException(["Informe o nome.", "Regra comercial."]);
        Assert.All(textos.Itens, i => Assert.Null(i.Campo));
        Assert.Equal(textos.Erros, textos.Itens.Select(i => i.Mensagem));

        var item = Guid.NewGuid();
        var comCampos = new ValidacaoException([new ErroValidacao("CPF inválido.", CamposFichaPessoa.Documento), new ErroValidacao("Endereço 1: x", CamposFichaPessoa.Cep, item)]);
        Assert.Equal(new[] { "CPF inválido.", "Endereço 1: x" }, comCampos.Erros);
        Assert.Equal(item, comCampos.Itens[1].Item);
        Assert.Equal("CPF inválido." + Environment.NewLine + "Endereço 1: x", comCampos.Message);
    }

    [Fact]
    public void Campo_que_tambem_e_filtro_tem_o_mesmo_id_nos_dois_catalogos()
    {
        static Dictionary<string, string> Constantes(Type tipo) => tipo
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);

        var ficha = Constantes(typeof(CamposFichaPessoa));
        var filtro = Constantes(typeof(CamposFiltroPessoas));

        // Mesmo nome nos dois = mesmo campo de negócio = mesmo id.
        foreach (var (nome, id) in ficha.Where(f => filtro.ContainsKey(f.Key)))
            Assert.True(filtro[nome] == id, $"{nome}: ficha \"{id}\" × filtro \"{filtro[nome]}\"");
        Assert.Equal(CamposFiltroPessoas.Documento, CamposFichaPessoa.Documento);
        Assert.Equal(CamposFiltroPessoas.Municipio, CamposFichaPessoa.Municipio);
        Assert.Equal(CamposFiltroPessoas.Profissao, CamposFichaPessoa.Profissao);
        // Ids únicos na ficha.
        Assert.Equal(ficha.Count, ficha.Values.Distinct().Count());
    }
}
