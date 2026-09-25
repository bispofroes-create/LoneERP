using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

public class ConferenciaInscricaoEstadualTests
{
    private static Pessoa Empresa(string ie, string uf)
    {
        var endereco = new PessoaEndereco { Id = Guid.NewGuid(), Uf = uf, Finalidades = FinalidadeEndereco.Principal };
        return new Pessoa
        {
            Natureza = NaturezaPessoa.Juridica,
            Enderecos = [endereco],
            // Principal explícito da finalidade Fiscal (não vem mais da ordem nem da coluna de bits).
            FinalidadesEnderecos =
            [
                new PessoaEnderecoFinalidade
                {
                    Id = Guid.NewGuid(), PessoaEnderecoId = endereco.Id, Principal = true,
                    FinalidadeId = Lone.Domain.Enderecos.FinalidadesEnderecoIniciais.Id(Lone.Domain.Enderecos.FinalidadesEnderecoIniciais.Fiscal)
                }
            ],
            Estabelecimentos = [new Estabelecimento { Id = Guid.NewGuid(), Principal = true, InscricaoEstadual = ie }]
        };
    }

    [Fact]
    public void IE_que_confere_com_a_UF_nao_gera_aviso()
    {
        Assert.Empty(ConferenciaInscricaoEstadual.Avisos(Empresa("110042490114", "SP")));
    }

    [Fact]
    public void IE_com_digito_errado_gera_aviso_com_a_UF()
    {
        var aviso = Assert.Single(ConferenciaInscricaoEstadual.Avisos(Empresa("110042490115", "SP")));
        Assert.Contains("SP", aviso);
    }

    [Fact]
    public void Isento_e_endereco_sem_UF_nao_sao_conferidos()
    {
        Assert.Empty(ConferenciaInscricaoEstadual.Avisos(Empresa("ISENTO", "SP")));
        Assert.Empty(ConferenciaInscricaoEstadual.Avisos(Empresa("110042490115", "")));
    }

    [Fact]
    public void Filial_usa_a_UF_do_proprio_endereco_fiscal()
    {
        var pessoa = Empresa("110042490114", "SP");
        var enderecoRj = new PessoaEndereco { Id = Guid.NewGuid(), Uf = "RJ" };
        pessoa.Enderecos.Add(enderecoRj);
        pessoa.Estabelecimentos.Add(new Estabelecimento { Id = Guid.NewGuid(), InscricaoEstadual = "110042490114", EnderecoFiscalId = enderecoRj.Id });

        Assert.Single(ConferenciaInscricaoEstadual.Avisos(pessoa)); // o número de SP não serve para a filial do RJ
    }
}
