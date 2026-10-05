using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// P0 (D3-a e D6): as cinco ações do histórico (inclusão com foto, alteração, inativação, reativação e exclusão física
/// com foto), lidas do ChangeTracker como na gravação real. Não precisa de banco: o contexto só rastreia as entidades.
/// </summary>
public class ColetorAuditoriaP0Tests
{
    private static LoneDbContext Db() =>
        new(new DbContextOptionsBuilder<LoneDbContext>().UseSqlServer("Server=.;Database=Lone_SemConexao;Trusted_Connection=True").Options);

    private static List<RegistroAuditoria> Coletar(LoneDbContext db)
    {
        var coletor = new ColetorAuditoria(db.ChangeTracker, "teste", OrigemAlteracao.Usuario);
        coletor.Coletar();
        return coletor.Finalizar().ToList();
    }

    private static Contato Maria() => new()
    {
        Id = Guid.NewGuid(), PessoaId = Guid.NewGuid(), Nome = "Maria Souza", Cargo = "Compradora", Telefone = "3837210000",
        Email = "maria@exemplo.com", Principal = true, CelularWhatsApp = false
    };

    [Fact]
    public void Inclusao_no_cadastro_de_pessoas_guarda_a_foto_dos_campos_com_nome_e_valor()
    {
        using var db = Db();
        var c = Maria();
        db.Add(c);

        var r = Coletar(db);

        Assert.Single(r, x => x.Acao == AcaoAuditoria.Inclusao && x.Campo is null);
        var foto = r.Where(x => x.Acao == AcaoAuditoria.Inclusao && x.Campo is not null).ToDictionary(x => x.Campo!, x => x.ValorNovo);
        Assert.Equal("Maria Souza", foto["Nome"]);
        Assert.Equal("Compradora", foto["Cargo"]);
        Assert.Equal("maria@exemplo.com", foto["Email"]);
        Assert.Equal("Sim", foto["Principal"]);
        Assert.False(foto.ContainsKey("CelularWhatsApp"));   // falso: não é significativo
        Assert.False(foto.ContainsKey("Departamento"));      // vazio
        Assert.False(foto.ContainsKey("Id"));                // técnico, sem [DisplayName]
        Assert.False(foto.ContainsKey("PessoaId"));
        Assert.False(foto.ContainsKey("CriadoEm"));
        Assert.All(r, x => Assert.Equal(c.PessoaId, x.RaizId));
    }

    [Fact]
    public void Exclusao_fisica_guarda_a_foto_com_os_valores_anteriores()
    {
        using var db = Db();
        var c = Maria();
        db.Attach(c);
        db.Remove(c);

        var r = Coletar(db);

        Assert.Single(r, x => x.Acao == AcaoAuditoria.Exclusao && x.Campo is null);
        var nome = Assert.Single(r, x => x.Acao == AcaoAuditoria.Exclusao && x.Campo == "Nome");
        Assert.Equal("Maria Souza", nome.ValorAnterior);
        Assert.Null(nome.ValorNovo);
    }

    [Fact]
    public void Dado_sensivel_entra_mascarado_na_foto()
    {
        using var db = Db();
        db.Add(new PessoaSocio { Id = Guid.NewGuid(), PessoaId = Guid.NewGuid(), Nome = "João", Documento = "12345678000190" });

        var documento = Assert.Single(Coletar(db), x => x.Campo == "Documento");

        Assert.Equal("•••••••••••190", documento.ValorNovo);
    }

    [Fact]
    public void Numero_zero_entra_na_foto_da_conta()
    {
        using var db = Db();
        db.Add(new ContaCliente { Id = Guid.NewGuid(), PessoaId = Guid.NewGuid(), LimiteCredito = 0m, ExigeAprovacaoAcimaLimite = true });

        var r = Coletar(db);

        Assert.Contains(r, x => x.Campo == "LimiteCredito" && x.ValorNovo is not null);
        Assert.Contains(r, x => x.Campo == "ExigeAprovacaoAcimaLimite" && x.ValorNovo == "Sim");
        Assert.DoesNotContain(r, x => x.Campo == "EmpresaId"); // nulo (conta padrão)
    }

    [Fact]
    public void Inativar_e_acao_propria_com_o_resumo_e_nao_exclusao()
    {
        using var db = Db();
        var c = Maria();
        db.Attach(c);
        c.Ativo = false;

        var r = Assert.Single(Coletar(db));

        Assert.Equal(AcaoAuditoria.Inativacao, r.Acao);
        Assert.Equal("Ativo", r.Campo);
        Assert.Equal(("Sim", "Não"), (r.ValorAnterior, r.ValorNovo));
        Assert.Equal("Maria Souza (Compradora)", r.Descricao);
    }

    [Fact]
    public void Reativar_e_acao_propria_e_as_outras_mudancas_continuam_como_alteracao()
    {
        using var db = Db();
        var s = new PessoaSocio
        {
            Id = Guid.NewGuid(), PessoaId = Guid.NewGuid(), Nome = "João", Qualificacao = "Sócio", Ativo = false, SaiuEm = new DateOnly(2026, 1, 10)
        };
        db.Attach(s);
        s.Ativo = true;
        s.SaiuEm = null;

        var r = Coletar(db);

        var volta = Assert.Single(r, x => x.Acao == AcaoAuditoria.Reativacao);
        Assert.Equal("João (Sócio)", volta.Descricao);
        var saida = Assert.Single(r, x => x.Acao == AcaoAuditoria.Alteracao);
        Assert.Equal("SaiuEm", saida.Campo);
        Assert.Equal(("10/01/2026", (string?)null), (saida.ValorAnterior, saida.ValorNovo));
    }

    [Fact]
    public void Entidade_com_Ativo_sem_resumo_registra_inativacao_e_reativacao_do_mesmo_jeito()
    {
        using var db = Db();
        var telefone = new MeioContato { Id = Guid.NewGuid(), PessoaId = Guid.NewGuid(), Valor = "38999990000" };
        db.Attach(telefone);
        telefone.Ativo = false;

        var inativacao = Assert.Single(Coletar(db));
        Assert.Equal(AcaoAuditoria.Inativacao, inativacao.Acao);
        Assert.Null(inativacao.Descricao);

        using var db2 = Db();
        var tipo = new TipoMeioContato { Id = Guid.NewGuid(), Nome = "Comercial", Ativo = false };
        db2.Attach(tipo);
        tipo.Ativo = true;
        Assert.Equal(AcaoAuditoria.Reativacao, Assert.Single(Coletar(db2)).Acao); // fora do cadastro de pessoas também (D6)
    }

    [Fact]
    public void Resumo_vazio_nao_impede_a_inativacao()
    {
        using var db = Db();
        var c = new Contato { Id = Guid.NewGuid(), PessoaId = Guid.NewGuid(), Nome = "" };
        db.Attach(c);
        c.Ativo = false;

        var r = Assert.Single(Coletar(db));

        Assert.Equal(AcaoAuditoria.Inativacao, r.Acao);
        Assert.Null(r.Descricao);
    }

    [Fact]
    public void Alteracao_comum_continua_uma_linha_por_campo()
    {
        using var db = Db();
        var c = Maria();
        db.Attach(c);
        c.Cargo = "Gerente";

        var r = Assert.Single(Coletar(db));

        Assert.Equal((AcaoAuditoria.Alteracao, "Cargo", "Compradora", "Gerente"), (r.Acao, r.Campo, r.ValorAnterior, r.ValorNovo));
    }

    [Fact]
    public void Fora_do_cadastro_de_pessoas_a_inclusao_continua_sem_foto()
    {
        using var db = Db();
        db.Add(new TipoMeioContato { Id = Guid.NewGuid(), Nome = "Comercial" });

        var r = Assert.Single(Coletar(db));

        Assert.Equal(AcaoAuditoria.Inclusao, r.Acao);
        Assert.Null(r.Campo);
    }

    [Fact]
    public void Valor_auditavel_continua_igual()
    {
        using var db = Db();
        db.Add(new PessoaEtiqueta { Id = Guid.NewGuid(), PessoaId = Guid.NewGuid(), EtiquetaId = Guid.NewGuid() });

        var r = Assert.Single(Coletar(db));

        Assert.Equal(AcaoAuditoria.Alteracao, r.Acao);
        Assert.Null(r.ValorAnterior);
    }
}
