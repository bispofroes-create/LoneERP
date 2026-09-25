using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Papeis;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

public class PapeisTests
{
    private static readonly Papel Cliente = new() { Id = PapeisSistema.Id(TipoPapel.Cliente), Nome = "Cliente", PapelSistema = TipoPapel.Cliente };
    private static readonly Papel Parceiro = new() { Id = Guid.NewGuid(), Nome = "Parceiro" };
    private static readonly Papel Antigo = new() { Id = Guid.NewGuid(), Nome = "Antigo", Ativo = false };

    private static Dictionary<Guid, Papel> Cadastro() => new[] { Cliente, Parceiro, Antigo }.ToDictionary(p => p.Id);

    [Fact]
    public void Codigo_e_gerado_do_nome_sem_acento_e_em_maiusculas()
    {
        var papel = new Papel { Nome = "  Parceiro   de negócios " };

        RegrasPapel.Normalizar(papel);

        Assert.Equal("Parceiro de negócios", papel.Nome);
        Assert.Equal("PARCEIRO_DE_NEGOCIOS", papel.Codigo);
        Assert.Empty(RegrasPapel.Validar(papel));
    }

    [Fact]
    public void Papel_de_sistema_com_regra_nao_pode_ser_desativado()
    {
        var cliente = new Papel { Nome = "Cliente", PapelSistema = TipoPapel.Cliente };
        var vendedor = new Papel { Nome = "Vendedor", PapelSistema = TipoPapel.Vendedor };

        Assert.Throws<ValidacaoException>(cliente.Desativar);
        vendedor.Desativar();
        Assert.False(vendedor.Ativo);
    }

    [Fact]
    public void Papel_da_pessoa_recebe_o_papel_de_sistema_do_cadastro_e_nao_o_do_aplicativo()
    {
        var pessoa = new Pessoa();
        pessoa.Papeis.Add(new PessoaPapel { PapelId = Parceiro.Id, Papel = TipoPapel.EmpresaDoGrupo, Ativo = true }); // tentativa de burlar
        pessoa.Papeis.Add(new PessoaPapel { PapelId = Cliente.Id, Ativo = true });

        var erros = RegrasPapel.Aplicar(pessoa, new HashSet<Guid>(), Cadastro());

        Assert.Empty(erros);
        Assert.Null(pessoa.Papeis[0].Papel);
        Assert.Equal(TipoPapel.Cliente, pessoa.Papeis[1].Papel);
        Assert.False(pessoa.TemPapel(TipoPapel.EmpresaDoGrupo));
    }

    [Fact]
    public void Papel_desativado_so_continua_ativo_em_quem_ja_tinha()
    {
        var pessoa = new Pessoa();
        pessoa.Papeis.Add(new PessoaPapel { PapelId = Antigo.Id, Ativo = true });

        Assert.NotEmpty(RegrasPapel.Aplicar(pessoa, new HashSet<Guid>(), Cadastro()));
        Assert.Empty(RegrasPapel.Aplicar(pessoa, new HashSet<Guid> { Antigo.Id }, Cadastro()));
    }

    [Fact]
    public void Mudancas_de_papel_viram_frases_do_historico()
    {
        var atuais = new[] { new PessoaPapel { PapelId = Parceiro.Id, Ativo = true }, new PessoaPapel { PapelId = Cliente.Id, Ativo = false } };

        var frases = RegrasPapel.Mudancas(new HashSet<Guid> { Cliente.Id }, atuais, Cadastro()).ToList();

        Assert.Equal(new[] { "Papel 'Parceiro' incluído.", "Papel 'Cliente' encerrado." }, frases);
    }

    [Fact]
    public void Varios_periodos_do_mesmo_papel_mas_so_um_ativo()
    {
        var pessoa = new Pessoa { Nome = "Ana" };
        pessoa.Papeis.Add(new PessoaPapel { PapelId = Parceiro.Id, Ativo = false, InicioEm = new DateOnly(2024, 1, 1), FimEm = new DateOnly(2024, 6, 30) });
        pessoa.Papeis.Add(new PessoaPapel { PapelId = Parceiro.Id, Ativo = true });
        pessoa.Papeis.Add(new PessoaPapel { PapelId = Parceiro.Id, Ativo = true }); // repetido

        PessoaNormalizador.Normalizar(pessoa, new DateOnly(2026, 9, 25));

        Assert.Equal(2, pessoa.Papeis.Count);
        Assert.Equal(new DateOnly(2026, 9, 25), pessoa.Papeis.Single(p => p.Ativo).InicioEm);
        Assert.Equal(new DateOnly(2024, 6, 30), pessoa.Papeis.Single(p => !p.Ativo).FimEm);
    }

    [Fact]
    public void Papel_antigo_sem_id_do_cadastro_usa_o_id_do_papel_de_sistema()
    {
        var pessoa = new Pessoa();
        pessoa.Papeis.Add(new PessoaPapel { Papel = TipoPapel.Fornecedor, Ativo = true });

        RegrasPapel.CompletarIds(pessoa);

        Assert.Equal(PapeisSistema.Id(TipoPapel.Fornecedor), pessoa.Papeis[0].PapelId);
    }
}
