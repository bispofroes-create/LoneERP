using Lone.Application.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Tests.Aplicacao;

public class PessoaMapeamentoTests
{
    private static PessoaDto EmpresaComFilial()
    {
        var endereco = new EnderecoDto
        {
            Id = Guid.NewGuid(),
            // Formato atual: finalidades nas relações (Usos), com o principal explícito; os bits legados não são a fonte.
            Usos = [new FinalidadeDoEnderecoDto { Id = Guid.NewGuid(), FinalidadeId = Lone.Domain.Enderecos.FinalidadesEnderecoIniciais.Id(Lone.Domain.Enderecos.FinalidadesEnderecoIniciais.Fiscal), Principal = true }],
            Cep = "01310100",
            Logradouro = "Avenida Paulista",
            Numero = "1000",
            MunicipioId = 3550308,
            Cidade = "São Paulo",
            Uf = "SP"
        };

        return new PessoaDto
        {
            Natureza = NaturezaPessoa.Juridica,
            Nome = "Empresa Exemplo Ltda",
            Enderecos = [endereco],
            Estabelecimentos =
            [
                new EstabelecimentoDto { Cnpj = "11222333000181", Principal = true, EnderecoFiscalId = endereco.Id },
                new EstabelecimentoDto { Cnpj = "11222333000262" }
            ],
            Papeis = [new PapelDto { Papel = TipoPapel.Cliente, InicioEm = new DateOnly(2026, 1, 1) }],
            ContasCliente = [new ContaClienteDto { LimiteCredito = 5_000m }]
        };
    }

    [Fact]
    public void Ids_vazios_recebem_id_novo_e_os_filhos_apontam_para_a_pessoa()
    {
        var entidade = PessoaMapeamento.ParaEntidade(EmpresaComFilial());

        Assert.NotEqual(Guid.Empty, entidade.Id);
        Assert.All(entidade.Estabelecimentos, e =>
        {
            Assert.NotEqual(Guid.Empty, e.Id);
            Assert.Equal(entidade.Id, e.PessoaId);
        });
        Assert.Equal(entidade.Id, entidade.ContasCliente[0].PessoaId);
    }

    [Fact]
    public void Ida_e_volta_preserva_os_dados()
    {
        var original = EmpresaComFilial();

        var volta = PessoaMapeamento.ParaDto(PessoaMapeamento.ParaEntidade(original));

        Assert.Equal(original.Nome, volta.Nome);
        Assert.Equal(2, volta.Estabelecimentos.Count);
        Assert.Equal("11222333000181", volta.Estabelecimentos[0].Cnpj); // principal primeiro
        Assert.Equal(original.Enderecos[0].Id, volta.Estabelecimentos[0].EnderecoFiscalId);
        Assert.Equal(5_000m, volta.ContasCliente[0].LimiteCredito);
        Assert.True(volta.TemPapel(TipoPapel.Cliente));
    }

    [Fact]
    public void Empresa_valida_nao_tem_erros()
    {
        var pessoa = PessoaMapeamento.ParaEntidade(EmpresaComFilial());
        PessoaNormalizador.Normalizar(pessoa);

        Assert.Empty(PessoaValidador.Validar(pessoa));
        Assert.Equal("11222333", pessoa.DocumentoPrincipal); // raiz do CNPJ principal
    }

    [Fact]
    public void Endereco_fiscal_de_fora_do_cadastro_e_recusado()
    {
        var dto = EmpresaComFilial();
        dto.Estabelecimentos[1].EnderecoFiscalId = Guid.NewGuid();

        var pessoa = PessoaMapeamento.ParaEntidade(dto);
        PessoaNormalizador.Normalizar(pessoa);

        Assert.Contains(PessoaValidador.Validar(pessoa), e => e.Contains("endereço fiscal"));
    }

    [Fact]
    public void Filial_de_outra_raiz_e_recusada()
    {
        var dto = EmpresaComFilial();
        dto.Estabelecimentos[1].Cnpj = "11444777000161";

        var pessoa = PessoaMapeamento.ParaEntidade(dto);
        PessoaNormalizador.Normalizar(pessoa);

        Assert.Contains(PessoaValidador.Validar(pessoa), e => e.Contains("raiz diferente"));
    }
}
