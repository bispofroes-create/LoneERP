using Lone.Core.Entidades;
using Lone.Core.Enums;
using Lone.Data.Auditoria;
using Lone.Data.Configuracoes;
using Microsoft.EntityFrameworkCore;

namespace Lone.Data;

public class LoneDbContext : DbContext
{
    public LoneDbContext(DbContextOptions<LoneDbContext> options) : base(options) { }

    public DbSet<Pessoa> Pessoas => Set<Pessoa>();
    public DbSet<Estabelecimento> Estabelecimentos => Set<Estabelecimento>();
    public DbSet<PessoaEndereco> PessoaEnderecos => Set<PessoaEndereco>();
    public DbSet<MeioContato> MeiosContato => Set<MeioContato>();
    public DbSet<Contato> Contatos => Set<Contato>();
    public DbSet<PessoaDocumento> PessoaDocumentos => Set<PessoaDocumento>();
    public DbSet<PessoaPapel> PessoaPapeis => Set<PessoaPapel>();
    public DbSet<ContaCliente> ContasCliente => Set<ContaCliente>();
    public DbSet<ContaFornecedor> ContasFornecedor => Set<ContaFornecedor>();
    public DbSet<Bloqueio> Bloqueios => Set<Bloqueio>();
    public DbSet<GrupoEconomico> GruposEconomicos => Set<GrupoEconomico>();
    public DbSet<TipoRelacionamento> TiposRelacionamento => Set<TipoRelacionamento>();
    public DbSet<PessoaRelacionamento> PessoaRelacionamentos => Set<PessoaRelacionamento>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfis => Set<Perfil>();

    /// <summary>Usuário gravado na auditoria. Definido pelos repositórios ao abrir o contexto.</summary>
    public string Usuario { get; set; } = "sistema";

    /// <summary>Origem das alterações desta gravação (usuário, consulta externa, importação...).</summary>
    public OrigemAlteracao Origem { get; set; } = OrigemAlteracao.Usuario;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        SequenciasConfiguration.Configurar(modelBuilder);

        // Carrega todas as classes IEntityTypeConfiguration da pasta Configuracoes.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoneDbContext).Assembly);

        // Toda raiz de agregado tem controle de concorrência (rowversion).
        foreach (var tipo in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(AgregadoRaiz).IsAssignableFrom(t.ClrType)))
        {
            modelBuilder.Entity(tipo.ClrType).Property(nameof(AgregadoRaiz.Versao)).IsRowVersion();
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync: a auditoria só é gravada no modo assíncrono.");

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AplicarDatasDeControle();

        var coletor = new ColetorAuditoria(ChangeTracker, Usuario, Origem);
        coletor.Coletar();

        if (!coletor.TemRegistros)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        // Dados e auditoria na mesma transação: ou grava tudo, ou nada.
        await using var transacao = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;

        var alterados = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        // Só depois de gravar sabemos os Ids dos registros incluídos.
        Auditoria.AddRange(coletor.Finalizar());
        await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (transacao is not null)
            await transacao.CommitAsync(cancellationToken);

        return alterados;
    }

    private void AplicarDatasDeControle()
    {
        var agora = DateTime.Now;
        foreach (var entrada in ChangeTracker.Entries<EntidadeBase>())
        {
            if (entrada.State == EntityState.Added)
            {
                entrada.Entity.CriadoEm = agora;
            }
            else if (entrada.State == EntityState.Modified)
            {
                entrada.Entity.AtualizadoEm = agora;
                entrada.Property(e => e.CriadoEm).IsModified = false;
            }
        }
    }
}
