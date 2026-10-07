using Lone.Application.Pessoas;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Tests.Cliente;

/// <summary>
/// Correções de UX pós-P1-8, item 1: a ficha nova já abre com um endereço vazio para facilitar a digitação. Enquanto o
/// usuário não mexe nele, ele não bloqueia a gravação nem é enviado; qualquer dado faz voltar toda a conferência.
/// Endereço gravado nunca conta como em branco, e a API continua recusando endereço vazio que chegue até ela.
/// </summary>
public class EnderecoEmBrancoTests
{
    private static PessoaFormulario Nova(NaturezaPessoa natureza)
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, natureza);
        f.Nome = "Teste";
        return f;
    }

    private static bool ErroDeEndereco(string mensagem) => mensagem.StartsWith("Endereço", StringComparison.Ordinal);

    [Theory]
    [InlineData(NaturezaPessoa.Fisica)]
    [InlineData(NaturezaPessoa.Juridica)]
    [InlineData(NaturezaPessoa.Estrangeiro)]
    public void Endereco_vazio_da_ficha_nova_nao_bloqueia_e_nao_e_enviado(NaturezaPessoa natureza)
    {
        var f = Nova(natureza);
        var endereco = Assert.Single(f.Enderecos);

        Assert.True(endereco.EmBranco);
        Assert.DoesNotContain(f.ValidarLocalmente(), ErroDeEndereco);
        Assert.Empty(f.ParaDto().Enderecos); // nada fictício vai para a API
        Assert.Single(f.Enderecos);           // continua na tela, pronto para digitar
    }

    [Fact]
    public void Endereco_comecado_volta_a_ser_conferido_por_inteiro()
    {
        var f = Nova(NaturezaPessoa.Fisica);
        var endereco = f.Enderecos[0];

        endereco.Logradouro = "Rua A";

        Assert.False(endereco.EmBranco);
        var erros = f.ValidarLocalmenteComCampos();
        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.Municipio && e.Item == endereco.Id);
        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.Cep && e.Item == endereco.Id);
        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.Numero && e.Item == endereco.Id);
        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.Bairro && e.Item == endereco.Id);
        Assert.Single(f.ParaDto().Enderecos);
    }

    [Fact]
    public void So_escolher_a_UF_ja_conta_como_comecado()
    {
        var f = Nova(NaturezaPessoa.Fisica);
        var endereco = f.Enderecos[0];

        endereco.Municipio.Uf = "MG";

        Assert.False(endereco.EmBranco);
        Assert.Contains("Endereço 1: escolha a UF e o município na lista.", f.ValidarLocalmente());
    }

    [Fact]
    public void Marcar_sem_numero_ou_exterior_tambem_conta_como_comecado()
    {
        var semNumero = Nova(NaturezaPessoa.Fisica).Enderecos[0];
        semNumero.SemNumero = true;
        Assert.False(semNumero.EmBranco);

        var f = Nova(NaturezaPessoa.Estrangeiro);
        var exterior = f.Enderecos[0];
        exterior.NoExterior = true;
        Assert.False(exterior.EmBranco);
        Assert.Single(f.ParaDto().Enderecos); // vai para a API, que pede cidade e país
    }

    [Fact]
    public void Endereco_apagado_ate_ficar_vazio_volta_a_nao_contar()
    {
        var f = Nova(NaturezaPessoa.Fisica);
        var endereco = f.Enderecos[0];
        endereco.Logradouro = "Rua A";
        endereco.Logradouro = string.Empty;

        Assert.True(endereco.EmBranco);
        Assert.Empty(f.ParaDto().Enderecos);
    }

    [Fact]
    public void Endereco_gravado_nunca_conta_como_em_branco_nem_some_do_envio()
    {
        // Mesmo um endereço gravado sem nada (dado antigo/migrado) continua sendo enviado e conferido como antes.
        var dto = new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica,
            Enderecos = [new EnderecoDto { Id = Guid.NewGuid(), Logradouro = string.Empty, Ativo = true }]
        };
        var f = PessoaFormulario.De(dto);
        var endereco = Assert.Single(f.Enderecos);

        Assert.False(endereco.EmBranco);
        Assert.Contains("Endereço 1: escolha a UF e o município na lista.", f.ValidarLocalmente());
        Assert.Equal(endereco.Id, Assert.Single(f.ParaDto().Enderecos).Id);
    }

    [Fact]
    public void Endereco_gravado_inativo_continua_indo_sem_ser_conferido()
    {
        var dto = new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica,
            Enderecos = [new EnderecoDto { Id = Guid.NewGuid(), Logradouro = string.Empty, Ativo = false }]
        };
        var f = PessoaFormulario.De(dto);

        Assert.DoesNotContain(f.ValidarLocalmente(), ErroDeEndereco);
        Assert.False(Assert.Single(f.ParaDto().Enderecos).Ativo);
    }

    [Fact]
    public void Cep_aplicado_no_endereco_vazio_faz_ele_valer()
    {
        var f = Nova(NaturezaPessoa.Fisica);
        var endereco = f.Enderecos[0];

        endereco.AplicarCep(new DadosCep { Cep = "35790000", Logradouro = "Rua Direita", Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904", Bairro = "Centro" });

        Assert.False(endereco.EmBranco);
        var enviado = Assert.Single(f.ParaDto().Enderecos);
        Assert.Equal("Rua Direita", enviado.Logradouro);
        Assert.Contains(f.ValidarLocalmenteComCampos(), e => e.Campo == CamposFichaPessoa.Numero && e.Item == endereco.Id);
    }

    [Fact]
    public void Filial_que_aponta_o_endereco_vazio_nao_envia_endereco_fiscal_inexistente()
    {
        var f = Nova(NaturezaPessoa.Juridica);
        f.AdicionarEstabelecimento();
        var filial = f.Estabelecimentos[^1];
        filial.EnderecoFiscal = f.Enderecos[0];

        var dto = f.ParaDto();

        Assert.Empty(dto.Enderecos);
        Assert.All(dto.Estabelecimentos, e => Assert.Null(e.EnderecoFiscalId));
    }

    [Fact]
    public void A_API_continua_recusando_endereco_vazio_que_chegue_ate_ela()
    {
        var dto = PessoaFormulario.NovaPessoa().ParaDto();
        dto.Nome = "Teste";
        dto.Enderecos = [new EnderecoDto { Id = Guid.NewGuid(), Logradouro = string.Empty, Ativo = true }];

        var pessoa = PessoaMapeamento.ParaEntidade(dto);
        PessoaNormalizador.Normalizar(pessoa);

        var erros = PessoaValidador.Validar(pessoa);
        Assert.Contains("Endereço 1: informe o logradouro.", erros);
        Assert.Contains("Endereço 1: escolha a UF e o município na lista.", erros);
    }
}
