using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

/// <summary>Regras de endereço × finalidade no domínio (a API aplica estas regras; o banco reforça com índices).</summary>
public class FinalidadesEnderecoTests
{
    private static readonly Guid Entrega = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Entrega);
    private static readonly Guid Cobranca = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Cobranca);
    private static readonly Guid Comercial = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Comercial);

    private static Dictionary<Guid, FinalidadeEnderecoCadastro> Cadastro(bool entregaAtiva = true) =>
        FinalidadesEnderecoIniciais.Todas.Select(f => new FinalidadeEnderecoCadastro
        {
            Id = f.Id, Codigo = f.Codigo, Nome = f.Nome, Ordem = f.Ordem, DoSistema = true,
            Ativo = f.Codigo != FinalidadesEnderecoIniciais.Entrega || entregaAtiva
        }).ToDictionary(f => f.Id);

    private static PessoaEndereco Endereco(string numero, bool ativo = true, string cidade = "Curvelo") => new()
    {
        Id = Guid.NewGuid(), Logradouro = "Rua A", Numero = numero, Bairro = "Centro", Cep = "35790000", Cidade = cidade, Uf = "MG",
        MunicipioId = 3120904, Ativo = ativo
    };

    private static PessoaEnderecoFinalidade Uso(PessoaEndereco e, Guid finalidade, bool principal = false, bool ativo = true) => new()
    {
        Id = Guid.NewGuid(), PessoaEnderecoId = e.Id, FinalidadeId = finalidade, Principal = principal, Ativo = ativo
    };

    [Fact]
    public void Dois_principais_para_a_mesma_finalidade_sao_recusados()
    {
        var a = Endereco("1");
        var b = Endereco("2");
        var p = new Pessoa { Enderecos = [a, b], FinalidadesEnderecos = [Uso(a, Entrega, true), Uso(b, Entrega, true)] };

        Assert.Contains(RegrasFinalidadeEndereco.Validar(p, Cadastro(), []), e => e.Contains("Mais de um endereço principal para Entrega"));
    }

    [Fact]
    public void Mesma_finalidade_ativa_duas_vezes_no_endereco_e_recusada_mas_historico_inativo_nao()
    {
        var a = Endereco("1");
        var repetida = new Pessoa { Enderecos = [a], FinalidadesEnderecos = [Uso(a, Entrega), Uso(a, Entrega)] };
        var comHistorico = new Pessoa { Enderecos = [a], FinalidadesEnderecos = [Uso(a, Entrega), Uso(a, Entrega, ativo: false)] };

        Assert.Contains(RegrasFinalidadeEndereco.Validar(repetida, Cadastro(), []), e => e.Contains("já possui a finalidade Entrega"));
        Assert.Empty(RegrasFinalidadeEndereco.Validar(comHistorico, Cadastro(), []));
    }

    [Fact]
    public void Finalidade_desativada_no_cadastro_nao_entra_em_associacao_nova_mas_a_existente_continua()
    {
        var a = Endereco("1");
        var uso = Uso(a, Entrega);
        var p = new Pessoa { Enderecos = [a], FinalidadesEnderecos = [uso] };

        Assert.Contains(RegrasFinalidadeEndereco.Validar(p, Cadastro(entregaAtiva: false), []), e => e.Contains("desativada"));
        Assert.Empty(RegrasFinalidadeEndereco.Validar(p, Cadastro(entregaAtiva: false), [Uso(a, Entrega)]));
    }

    [Fact]
    public void Relacao_ou_endereco_inativo_nunca_e_principal()
    {
        var inativo = Endereco("1", ativo: false);
        var ativo = Endereco("2");
        var doInativo = Uso(inativo, Entrega, principal: true);
        var retirada = Uso(ativo, Cobranca, principal: true, ativo: false);
        var p = new Pessoa { Enderecos = [inativo, ativo], FinalidadesEnderecos = [doInativo, retirada] };

        RegrasFinalidadeEndereco.Normalizar(p);

        Assert.False(doInativo.Principal);
        Assert.False(retirada.Principal);
        Assert.True(doInativo.Ativo); // a relação fica como histórico
    }

    [Fact]
    public void Referencia_da_listagem_e_o_principal_da_finalidade_de_menor_ordem_e_nao_cria_principal()
    {
        var a = Endereco("1", cidade: "A");
        var b = Endereco("2", cidade: "B");
        var p = new Pessoa { Enderecos = [a, b], FinalidadesEnderecos = [Uso(a, Entrega, true), Uso(b, Comercial, true)] };

        Assert.Same(b, RegrasFinalidadeEndereco.EnderecoReferencia(p, Cadastro())); // Comercial tem ordem 1

        var semPrincipal = new Pessoa { Enderecos = [a, b], FinalidadesEnderecos = [Uso(a, Entrega), Uso(b, Entrega)] };
        Assert.Same(a, RegrasFinalidadeEndereco.EnderecoReferencia(semPrincipal, Cadastro())); // só exibição (1º ativo)
        Assert.All(semPrincipal.FinalidadesEnderecos, u => Assert.False(u.Principal));
    }

    [Fact]
    public void Coluna_legada_e_derivada_das_finalidades_ativas_e_da_referencia()
    {
        var a = Endereco("1");
        var p = new Pessoa { Enderecos = [a], FinalidadesEnderecos = [Uso(a, Entrega, true), Uso(a, Cobranca, ativo: false)] };

        RegrasFinalidadeEndereco.SincronizarLegado(p, a);

        Assert.Equal(FinalidadeEndereco.Entrega | FinalidadeEndereco.Principal, a.Finalidades);
    }

    [Fact]
    public void Pendencias_da_revisao_por_finalidade()
    {
        var a = Endereco("1");
        var b = Endereco("2");
        var c = Endereco("3");
        var p = new Pessoa { Enderecos = [a, b, c], FinalidadesEnderecos = [Uso(a, Entrega), Uso(b, Entrega), Uso(a, Cobranca, true), Uso(b, Cobranca)] };

        var pendencias = RegrasFinalidadeEndereco.PendenciasRevisao(p, id => id == Entrega ? "Entrega" : "Cobrança");

        Assert.Contains("Entrega: existem 2 endereços e nenhum foi definido como principal.", pendencias);
        Assert.DoesNotContain(pendencias, x => x.StartsWith("Cobrança"));
        Assert.Contains(pendencias, x => x.Contains("sem finalidade")); // endereço c
    }

    [Fact]
    public void Duplicata_nova_e_recusada_e_a_antiga_ja_gravada_so_e_apontada()
    {
        var gravadoA = Endereco("100");
        var gravadoB = Endereco("100"); // duplicado antigo
        var novo = Endereco("100");

        Assert.Empty(DuplicidadeEndereco.ErrosDeNovos([gravadoA, gravadoB], [gravadoA, gravadoB]));
        Assert.Single(DuplicidadeEndereco.ErrosDeNovos([gravadoA, novo], [gravadoA]));
        Assert.Single(DuplicidadeEndereco.Pares([gravadoA, gravadoB], incluirPossiveis: false));
    }

    [Theory]
    [InlineData("S/N", "")]
    [InlineData("s/nº", "SN")]
    [InlineData("100 A", "100a")]
    public void Numero_normalizado(string a, string b) =>
        Assert.Equal(DuplicidadeEndereco.Numero(a), DuplicidadeEndereco.Numero(b));

    [Fact]
    public void Consolidado_precisa_ficar_inativo_e_apontar_para_endereco_ativo()
    {
        var mantido = Endereco("1");
        var duplicado = Endereco("1");
        duplicado.MescladoEmId = mantido.Id;
        var p = new Pessoa { Enderecos = [mantido, duplicado] };

        Assert.NotEmpty(RegrasFinalidadeEndereco.ValidarConsolidacao(p)); // ainda ativo
        duplicado.Ativo = false;
        Assert.Empty(RegrasFinalidadeEndereco.ValidarConsolidacao(p));
    }
}
