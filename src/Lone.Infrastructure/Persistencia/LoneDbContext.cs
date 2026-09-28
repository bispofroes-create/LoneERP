using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Auditoria;
using Lone.Infrastructure.Persistencia.Configuracoes;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia;

public class LoneDbContext : DbContext
{
    public LoneDbContext(DbContextOptions<LoneDbContext> options) : base(options) { }

    public DbSet<Pessoa> Pessoas => Set<Pessoa>();
    public DbSet<Estabelecimento> Estabelecimentos => Set<Estabelecimento>();
    public DbSet<PessoaEndereco> PessoaEnderecos => Set<PessoaEndereco>();
    public DbSet<PessoaEnderecoFinalidade> PessoaEnderecoFinalidades => Set<PessoaEnderecoFinalidade>();
    public DbSet<FinalidadeEnderecoCadastro> FinalidadesEndereco => Set<FinalidadeEnderecoCadastro>();
    public DbSet<FinalidadeTratamento> FinalidadesTratamento => Set<FinalidadeTratamento>();
    public DbSet<MeioContato> MeiosContato => Set<MeioContato>();
    public DbSet<Contato> Contatos => Set<Contato>();
    public DbSet<PessoaDocumento> PessoaDocumentos => Set<PessoaDocumento>();
    public DbSet<PessoaPapel> PessoaPapeis => Set<PessoaPapel>();
    public DbSet<ContaCliente> ContasCliente => Set<ContaCliente>();
    public DbSet<ContaFornecedor> ContasFornecedor => Set<ContaFornecedor>();
    public DbSet<Bloqueio> Bloqueios => Set<Bloqueio>();
    public DbSet<PessoaSocio> PessoaSocios => Set<PessoaSocio>();
    public DbSet<PessoaConsentimento> PessoaConsentimentos => Set<PessoaConsentimento>();
    public DbSet<PessoaEtiqueta> PessoaEtiquetas => Set<PessoaEtiqueta>();
    public DbSet<Etiqueta> Etiquetas => Set<Etiqueta>();
    public DbSet<Profissao> Profissoes => Set<Profissao>();
    public DbSet<Papel> Papeis => Set<Papel>();
    public DbSet<TipoMeioContato> TiposMeioContato => Set<TipoMeioContato>();
    public DbSet<TipoEndereco> TiposEndereco => Set<TipoEndereco>();
    public DbSet<TipoDocumentoCadastro> TiposDocumento => Set<TipoDocumentoCadastro>();
    public DbSet<AnexoDocumento> AnexosDocumento => Set<AnexoDocumento>();
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Setor> Setores => Set<Setor>();
    public DbSet<CentroCusto> CentrosCusto => Set<CentroCusto>();
    public DbSet<VinculoColaborador> VinculosColaborador => Set<VinculoColaborador>();
    public DbSet<LotacaoColaborador> LotacoesColaborador => Set<LotacaoColaborador>();
    public DbSet<CondicaoPagamento> CondicoesPagamento => Set<CondicaoPagamento>();
    public DbSet<PerfilComercial> PerfisComerciais => Set<PerfilComercial>();
    public DbSet<TipoCarteira> TiposCarteira => Set<TipoCarteira>();
    public DbSet<ExcecaoComercial> ExcecoesComerciais => Set<ExcecaoComercial>();
    public DbSet<CarteiraCliente> CarteiraClientes => Set<CarteiraCliente>();
    public DbSet<Cnae> Cnaes => Set<Cnae>();
    public DbSet<EstabelecimentoCnae> EstabelecimentoCnaes => Set<EstabelecimentoCnae>();
    public DbSet<HistoricoFiscal> HistoricoFiscal => Set<HistoricoFiscal>();
    public DbSet<Interacao> Interacoes => Set<Interacao>();
    public DbSet<ParametrosRelacionamento> ParametrosRelacionamento => Set<ParametrosRelacionamento>();
    public DbSet<OcupacaoCbo> OcupacoesCbo => Set<OcupacaoCbo>();
    public DbSet<Equipe> Equipes => Set<Equipe>();
    public DbSet<MembroEquipe> MembrosEquipe => Set<MembroEquipe>();
    public DbSet<Indicador> Indicadores => Set<Indicador>();
    public DbSet<Meta> Metas => Set<Meta>();
    public DbSet<MetaItem> MetaItens => Set<MetaItem>();
    public DbSet<MetaFaixa> MetaFaixas => Set<MetaFaixa>();
    public DbSet<MetaParticipante> MetaParticipantes => Set<MetaParticipante>();
    public DbSet<MetaAlvo> MetaAlvos => Set<MetaAlvo>();
    public DbSet<FiltroSalvo> FiltrosSalvos => Set<FiltroSalvo>();
    public DbSet<PreferenciaMenu> PreferenciasMenu => Set<PreferenciaMenu>();
    public DbSet<PreferenciaTela> PreferenciasTela => Set<PreferenciaTela>();
    public DbSet<PessoaValorPersonalizado> PessoaValoresPersonalizados => Set<PessoaValorPersonalizado>();
    public DbSet<DocumentoValorPersonalizado> PessoaDocumentoValoresPersonalizados => Set<DocumentoValorPersonalizado>();
    public DbSet<CampoPersonalizado> CamposPersonalizados => Set<CampoPersonalizado>();
    public DbSet<CampoPersonalizadoOpcao> CampoPersonalizadoOpcoes => Set<CampoPersonalizadoOpcao>();
    public DbSet<Municipio> Municipios => Set<Municipio>();
    public DbSet<PendenciaMunicipio> PendenciasMunicipio => Set<PendenciaMunicipio>();
    public DbSet<GrupoEconomico> GruposEconomicos => Set<GrupoEconomico>();
    public DbSet<GrupoEmpresarial> GruposEmpresariais => Set<GrupoEmpresarial>();
    public DbSet<TipoRelacionamento> TiposRelacionamento => Set<TipoRelacionamento>();
    public DbSet<PessoaRelacionamento> PessoaRelacionamentos => Set<PessoaRelacionamento>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<TokenRenovacao> TokensRenovacao => Set<TokenRenovacao>();

    /// <summary>Usuário gravado na auditoria. Definido pelos repositórios ao abrir o contexto.</summary>
    public string Usuario { get; set; } = "sistema";

    /// <summary>Origem das alterações desta gravação (usuário, consulta externa, importação...).</summary>
    public OrigemAlteracao Origem { get; set; } = OrigemAlteracao.Usuario;

    /// <summary>Motivo informado para esta gravação (vai em todas as linhas de auditoria dela).</summary>
    public string? Motivo { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        SequenciasConfiguration.Configurar(modelBuilder);

        // Carrega todas as classes IEntityTypeConfiguration da pasta Configuracoes.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoneDbContext).Assembly);

        foreach (var tipo in modelBuilder.Model.GetEntityTypes().ToList())
        {
            // Toda raiz de agregado tem controle de concorrência (rowversion).
            if (typeof(AgregadoRaiz).IsAssignableFrom(tipo.ClrType))
            {
                modelBuilder.Entity(tipo.ClrType).Property(nameof(AgregadoRaiz.Versao)).IsRowVersion();
                modelBuilder.Entity(tipo.ClrType).Ignore(nameof(AgregadoRaiz.EventosPendentes)); // vão para a auditoria
            }

            // Chaves Guid vêm prontas (IdSequencial), do aparelho ou do servidor: o banco nunca gera.
            // Sem isto, o EF trataria um filho novo com Id preenchido como "alterado" em vez de "incluído".
            if (tipo.FindPrimaryKey() is { Properties: [{ ClrType: var tipoChave } chave] } && tipoChave == typeof(Guid))
                chave.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync: a auditoria só é gravada no modo assíncrono.");

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AplicarDatasDeControle();

        var coletor = new ColetorAuditoria(ChangeTracker, Usuario, Origem, Motivo);
        coletor.Coletar();

        if (!coletor.TemRegistros)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        // Dados e auditoria na mesma transação: ou grava tudo, ou nada.
        await using var transacao = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;

        // Aceita as mudanças já aqui, senão a segunda gravação (auditoria) repetiria a primeira.
        var alterados = await base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

        // Depois de gravar: a auditoria recebe os valores finais (ex.: código gerado pelo banco).
        Auditoria.AddRange(coletor.Finalizar());
        await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (transacao is not null)
            await transacao.CommitAsync(cancellationToken);

        return alterados;
    }

    /// <summary>Datas de controle (UTC) e Ids ainda vazios (ex.: estabelecimento criado pelo normalizador).</summary>
    private void AplicarDatasDeControle()
    {
        var agora = DateTime.UtcNow;
        foreach (var entrada in ChangeTracker.Entries<EntidadeBase>())
        {
            if (entrada.State == EntityState.Added)
            {
                if (entrada.Entity.Id == Guid.Empty)
                    entrada.Entity.Id = IdSequencial.Novo();
                entrada.Entity.CriadoEm = agora;
            }
            else if (entrada.State == EntityState.Modified)
            {
                entrada.Entity.AtualizadoEm = agora;
                entrada.Property(e => e.CriadoEm).IsModified = false;
            }
        }

        foreach (var entrada in ChangeTracker.Entries<UsuarioPerfil>().Where(e => e.State == EntityState.Added))
        {
            if (entrada.Entity.Id == Guid.Empty)
                entrada.Entity.Id = IdSequencial.Novo();
        }
    }
}
