using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;

namespace Lone.Tests.Dominio;

/// <summary>
/// Consolidação decidida pelo servidor (ConsolidacaoEndereco): o aplicativo só manda "consolidar A em B"; a regra
/// confere e decide a partir do estado carregado do banco. A transação (tudo ou nada) é da gravação do repositório.
/// </summary>
public class ConsolidacaoEnderecoTests
{
    private static readonly Guid Entrega = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Entrega);
    private static readonly Guid Cobranca = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Cobranca);
    private static readonly Guid Fiscal = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Fiscal);

    private static PessoaEndereco Endereco(string numero = "100", bool ativo = true) => new()
    {
        Id = Guid.NewGuid(), Logradouro = "Rua A", Numero = numero, Bairro = "Centro", Cep = "35790000", Cidade = "Curvelo", Uf = "MG",
        MunicipioId = 3120904, Ativo = ativo
    };

    private static PessoaEnderecoFinalidade Uso(PessoaEndereco e, Guid finalidade, bool principal = false, bool ativo = true) => new()
    {
        Id = Guid.NewGuid(), PessoaEnderecoId = e.Id, FinalidadeId = finalidade, Principal = principal, Ativo = ativo
    };

    [Fact]
    public void Enderecos_precisam_ser_da_mesma_pessoa()
    {
        var mantido = Endereco();
        var deOutraPessoa = Endereco();
        var p = new Pessoa { Id = Guid.NewGuid(), Enderecos = [mantido] };

        var erros = ConsolidacaoEndereco.Aplicar(p, deOutraPessoa.Id, mantido.Id);

        Assert.Contains(erros, e => e.Contains("não existe nesta pessoa"));
        Assert.True(mantido.Ativo);
    }

    [Fact]
    public void Origem_e_destino_precisam_estar_ativos_e_ser_diferentes()
    {
        var ativo = Endereco();
        var inativo = Endereco(ativo: false);
        var p = new Pessoa { Id = Guid.NewGuid(), Enderecos = [ativo, inativo] };

        Assert.NotEmpty(ConsolidacaoEndereco.Aplicar(p, inativo.Id, ativo.Id)); // origem inativa
        Assert.NotEmpty(ConsolidacaoEndereco.Aplicar(p, ativo.Id, inativo.Id)); // destino inativo
        Assert.NotEmpty(ConsolidacaoEndereco.Aplicar(p, ativo.Id, ativo.Id));   // o mesmo
        Assert.True(ativo.Ativo);
        Assert.Null(inativo.MescladoEmId);
    }

    [Fact]
    public void Enderecos_fisicamente_diferentes_nao_sao_consolidados()
    {
        var a = Endereco("100");
        var b = Endereco("200");
        var p = new Pessoa { Id = Guid.NewGuid(), Enderecos = [a, b] };

        Assert.Contains(ConsolidacaoEndereco.Aplicar(p, a.Id, b.Id), e => e.Contains("não são o mesmo endereço físico"));
        Assert.True(a.Ativo);
    }

    [Fact]
    public void Finalidades_sao_unidas_sem_repetir_o_principal_passa_e_o_historico_e_reaproveitado()
    {
        var mantido = Endereco();
        var duplicado = Endereco();
        var entregaNoMantido = Uso(mantido, Entrega);
        var cobrancaRetiradaNoMantido = Uso(mantido, Cobranca, ativo: false);
        var p = new Pessoa
        {
            Id = Guid.NewGuid(),
            Enderecos = [mantido, duplicado],
            FinalidadesEnderecos =
            [
                entregaNoMantido, cobrancaRetiradaNoMantido,
                Uso(duplicado, Entrega, principal: true), Uso(duplicado, Cobranca), Uso(duplicado, Fiscal)
            ]
        };

        Assert.Empty(ConsolidacaoEndereco.Aplicar(p, duplicado.Id, mantido.Id));

        var doMantido = p.FinalidadesEnderecos.Where(u => u.PessoaEnderecoId == mantido.Id).ToList();
        Assert.Equal(3, doMantido.Count);                                   // Entrega, Cobrança (reativada) e Fiscal: sem repetir
        Assert.True(entregaNoMantido.Principal);                            // o principal passou para o mesmo lugar físico
        Assert.True(cobrancaRetiradaNoMantido.Ativo);                       // linha histórica reaproveitada
        Assert.False(cobrancaRetiradaNoMantido.Principal);
        Assert.All(doMantido, u => Assert.True(u.Ativo));
        Assert.Single(p.FinalidadesEnderecos, u => u.Principal && u.FinalidadeId == Entrega);
        Assert.All(p.FinalidadesEnderecos.Where(u => u.PessoaEnderecoId == duplicado.Id), u => Assert.False(u.Principal));
        Assert.Equal(5 + 1, p.FinalidadesEnderecos.Count);                   // só a Fiscal é linha nova; nada apagado
    }

    [Fact]
    public void Conflito_de_principal_nao_e_decidido_em_silencio()
    {
        var mantido = Endereco();
        var duplicado = Endereco();
        var p = new Pessoa
        {
            Id = Guid.NewGuid(),
            Enderecos = [mantido, duplicado],
            FinalidadesEnderecos = [Uso(mantido, Entrega, principal: true), Uso(duplicado, Entrega, principal: true)] // estado inválido
        };

        Assert.Contains(ConsolidacaoEndereco.Aplicar(p, duplicado.Id, mantido.Id), e => e.Contains("principal da mesma finalidade"));
        Assert.True(duplicado.Ativo);
        Assert.All(p.FinalidadesEnderecos, u => Assert.True(u.Principal)); // nada mudou
    }

    [Fact]
    public void Consolidado_fica_inativo_com_MescladoEmId_e_as_filiais_sao_redirecionadas()
    {
        var mantido = Endereco();
        var duplicado = Endereco();
        var filial = new Estabelecimento { Id = Guid.NewGuid(), EnderecoFiscalId = duplicado.Id };
        var p = new Pessoa { Id = Guid.NewGuid(), Enderecos = [mantido, duplicado], Estabelecimentos = [filial] };

        Assert.Empty(ConsolidacaoEndereco.Aplicar(p, duplicado.Id, mantido.Id));

        Assert.False(duplicado.Ativo);
        Assert.Equal(mantido.Id, duplicado.MescladoEmId);
        Assert.Equal(mantido.Id, filial.EnderecoFiscalId);
        Assert.Equal(2, p.Enderecos.Count); // nada apagado
        Assert.Empty(RegrasFinalidadeEndereco.ValidarConsolidacao(p));

        Assert.NotEmpty(ConsolidacaoEndereco.Aplicar(p, duplicado.Id, mantido.Id)); // de novo: já consolidado
    }
}
