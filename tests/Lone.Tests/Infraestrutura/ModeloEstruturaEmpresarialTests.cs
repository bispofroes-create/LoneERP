using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Estrutura empresarial no MODELO do EF (o que a migration gera), sem banco: grupo empresarial só para pessoa jurídica,
/// grupo econômico preservado, relacionamento aberto único, condição do fornecedor ligada ao cadastro e tipos novos.
/// </summary>
public class ModeloEstruturaEmpresarialTests
{
    private static readonly IModel Modelo = CriarModelo();

    private static IModel CriarModelo()
    {
        using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>()
            .UseSqlServer("Server=(sem-conexao);Database=Lone;Trusted_Connection=True").Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entidade<T>() => Modelo.FindEntityType(typeof(T))!;

    [Fact]
    public void Grupo_empresarial_e_tabela_propria_e_o_grupo_economico_fica_so_no_banco()
    {
        var pessoa = Entidade<Pessoa>();
        var grupo = pessoa.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(GrupoEmpresarial));
        var economico = pessoa.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(GrupoEconomico));

        Assert.Equal(nameof(Pessoa.GrupoEmpresarialId), grupo.Properties.Single().Name);
        Assert.False(grupo.IsRequired); // opcional: empresa sem grupo é válida
        Assert.Equal(DeleteBehavior.NoAction, grupo.DeleteBehavior);
        // MC-4: a coluna antiga continua no banco (mesma FK), mas fora da classe Pessoa (propriedade de sombra).
        Assert.Equal("GrupoEconomicoId", economico.Properties.Single().Name);
        Assert.True(economico.Properties.Single().IsShadowProperty());
        Assert.Null(typeof(Pessoa).GetProperty("GrupoEconomicoId"));
        Assert.Null(typeof(Lone.Contracts.Pessoas.PessoaDto).GetProperty("GrupoEconomicoId"));
        Assert.Equal("GruposEmpresariais", Entidade<GrupoEmpresarial>().GetTableName());
        Assert.Equal("GruposEconomicos", Entidade<GrupoEconomico>().GetTableName());
    }

    [Fact]
    public void Banco_recusa_pessoa_fisica_em_grupo_empresarial()
    {
        var check = Entidade<Pessoa>().GetCheckConstraints().Single(c => c.ModelName == SqlMigracaoEstruturaEmpresarial.CheckGrupoSoPessoaJuridica);
        Assert.Equal($"[GrupoEmpresarialId] IS NULL OR [Natureza] = {(byte)NaturezaPessoa.Juridica}", check.Sql);
    }

    [Fact]
    public void O_mesmo_relacionamento_em_aberto_e_unico_no_banco()
    {
        var indice = Entidade<PessoaRelacionamento>().GetIndexes()
            .Single(i => i.GetDatabaseName() == SqlMigracaoEstruturaEmpresarial.IndiceRelacionamentoAberto);

        Assert.True(indice.IsUnique);
        Assert.Equal(new[] { "PessoaId", "PessoaDestinoId", "TipoRelacionamentoId" }, indice.Properties.Select(p => p.Name).ToArray());
        Assert.Equal("[Ativo] = 1 AND [FimEm] IS NULL", indice.GetFilter());
    }

    [Fact]
    public void Tipos_de_sistema_incluem_administrador_e_parceiro()
    {
        var ids = Entidade<TipoRelacionamento>().GetSeedData().Select(d => (Guid)d[nameof(TipoRelacionamento.Id)]!).ToHashSet();

        Assert.Contains(TiposRelacionamentoSistema.SocioDe, ids);
        Assert.Contains(TiposRelacionamentoSistema.AdministradorDe, ids);
        Assert.Contains(TiposRelacionamentoSistema.ParceiroDe, ids);
        Assert.Contains(TiposRelacionamentoSistema.ContatoDe, ids);
        Assert.Contains(TiposRelacionamentoSistema.ResponsavelPor, ids);
        Assert.Contains(TiposRelacionamentoSistema.RepresentanteDe, ids);
    }

    [Fact]
    public void Condicao_do_fornecedor_referencia_o_cadastro_de_condicoes()
    {
        var fk = Entidade<ContaFornecedor>().GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(CondicaoPagamento));
        Assert.Equal(nameof(ContaFornecedor.CondicaoPagamentoId), fk.Properties.Single().Name);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.NotNull(Entidade<ContaFornecedor>().FindProperty(nameof(ContaFornecedor.CondicaoPagamento))); // o texto anterior continua
    }

    [Theory]
    [InlineData(NaturezaPessoa.Juridica, "ABC Comércio e Participações Ltda", null, "ABC Comércio")]
    [InlineData(NaturezaPessoa.Juridica, "ABC Comércio e Participações Ltda", "ABC", "ABC Comércio")]
    [InlineData(NaturezaPessoa.Juridica, "ABC Comércio e Participações Ltda", null, null)]
    [InlineData(NaturezaPessoa.Fisica, "João da Silva", null, "ignorado")]
    [InlineData(NaturezaPessoa.Fisica, "João da Silva", "João", null)]
    public void Nome_da_lista_no_banco_segue_a_mesma_regra_do_dominio(NaturezaPessoa natureza, string nome, string? exibicao, string? fantasia)
    {
        var p = new Pessoa { Natureza = natureza, Nome = nome, NomeExibicao = exibicao, NomeSocial = natureza == NaturezaPessoa.Fisica ? "Social" : null };
        p.Estabelecimentos.Add(new Estabelecimento { Principal = true, NomeFantasia = fantasia });
        p.Estabelecimentos.Add(new Estabelecimento { Principal = false, NomeFantasia = "Filial" });

        var noBanco = PessoaRepositorio.NomeParaExibirNoBanco.Compile()(p);

        Assert.Equal(NomePessoa.ParaExibir(p.Natureza, p.Nome, p.NomeExibicao, p.NomeSocial, p.EstabelecimentoPrincipal()?.NomeFantasia), noBanco);
    }
}
