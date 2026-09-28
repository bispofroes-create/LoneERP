using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Metas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class EquipeConfiguration : IEntityTypeConfiguration<Equipe>
{
    public void Configure(EntityTypeBuilder<Equipe> b)
    {
        b.ToTable("Equipes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(Equipe.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.HasIndex(x => x.Nome).IsUnique();
        b.HasOne<Departamento>().WithMany().HasForeignKey(x => x.DepartamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.LiderId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Membros).WithOne().HasForeignKey(x => x.EquipeId).OnDelete(DeleteBehavior.Restrict);

        // Hierarquia (Fase 2a): equipe acima. Nunca apaga em cascata.
        b.HasOne<Equipe>().WithMany().HasForeignKey(x => x.EquipePaiId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class MembroEquipeConfiguration : IEntityTypeConfiguration<MembroEquipe>
{
    public void Configure(EntityTypeBuilder<MembroEquipe> b)
    {
        b.ToTable("MembrosEquipe");
        b.HasKey(x => x.Id);
        b.HasOne<Pessoa>().WithMany().HasForeignKey(x => x.PessoaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EquipeId, x.PessoaId });
        b.HasIndex(x => x.PessoaId); // "de que equipes a pessoa participa" (apuração)
        b.Property(x => x.Papel).HasConversion<byte>(); // 0 = Membro
    }
}

public class IndicadorConfiguration : IEntityTypeConfiguration<Indicador>
{
    public void Configure(EntityTypeBuilder<Indicador> b)
    {
        b.ToTable("Indicadores");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).IsRequired().HasMaxLength(Indicador.TamanhoMaximoCodigo).IsUnicode(false);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(Indicador.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.Property(x => x.Fonte).HasConversion<byte>();
        b.Property(x => x.Unidade).HasConversion<byte>();
        b.Property(x => x.Sentido).HasConversion<byte>();
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasIndex(x => x.Nome).IsUnique();

        // Indicadores de sistema (fontes do cadastro), com Ids fixos.
        var criacao = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(IndicadoresSistema.Todos.Select(i => new Indicador
        {
            Id = i.Id, Codigo = i.Codigo, Nome = i.Nome, Fonte = i.Fonte, Unidade = UnidadeIndicador.Quantidade,
            Sentido = SentidoIndicador.MaiorMelhor, Ativo = true, CriadoEm = criacao
        }).ToArray());
    }
}

public class MetaConfiguration : IEntityTypeConfiguration<Meta>
{
    public void Configure(EntityTypeBuilder<Meta> b)
    {
        b.ToTable("Metas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(Meta.TamanhoMaximoNome);
        b.Property(x => x.Descricao).HasMaxLength(RegrasMeta.TamanhoMaximoDescricao);
        b.Property(x => x.Situacao).HasConversion<byte>();
        b.Property(x => x.LimiteAtingimento).HasPrecision(7, 2);
        b.Property(x => x.FechadaPor).HasMaxLength(100);
        b.HasIndex(x => new { x.InicioEm, x.FimEm });
        b.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.MetaId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Faixas).WithOne().HasForeignKey(x => x.MetaId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Participantes).WithOne().HasForeignKey(x => x.MetaId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Alvos).WithOne().HasForeignKey(x => x.MetaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MetaItemConfiguration : IEntityTypeConfiguration<MetaItem>
{
    public void Configure(EntityTypeBuilder<MetaItem> b)
    {
        b.ToTable("MetaItens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Peso).HasPrecision(7, 4);
        b.HasOne<Indicador>().WithMany().HasForeignKey(x => x.IndicadorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.MetaId, x.IndicadorId }).IsUnique(); // o mesmo indicador uma vez por meta
    }
}

public class MetaFaixaConfiguration : IEntityTypeConfiguration<MetaFaixa>
{
    public void Configure(EntityTypeBuilder<MetaFaixa> b)
    {
        b.ToTable("MetaFaixas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(RegrasMeta.TamanhoMaximoNomeFaixa);
        b.Property(x => x.InicioPercentual).HasPrecision(7, 2);
        b.Property(x => x.PercentualPremio).HasPrecision(7, 2);
    }
}

public class MetaParticipanteConfiguration : IEntityTypeConfiguration<MetaParticipante>
{
    public void Configure(EntityTypeBuilder<MetaParticipante> b)
    {
        b.ToTable("MetaParticipantes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nivel).HasConversion<byte>();
        b.Property(x => x.NotaFinal).HasPrecision(7, 2);
        b.Property(x => x.Faixa).HasMaxLength(60);
        b.Property(x => x.PercentualPremio).HasPrecision(7, 2);
        // Sem FK em ReferenciaId: aponta para cadastros diferentes conforme o nível.
        b.HasIndex(x => new { x.MetaId, x.Nivel, x.ReferenciaId }).IsUnique();
        b.HasIndex(x => new { x.Nivel, x.ReferenciaId }); // "metas de que a pessoa/equipe participa" (comissões)
    }
}

public class MetaAlvoConfiguration : IEntityTypeConfiguration<MetaAlvo>
{
    public void Configure(EntityTypeBuilder<MetaAlvo> b)
    {
        b.ToTable("MetaAlvos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Alvo).HasPrecision(18, 4);
        b.Property(x => x.Realizado).HasPrecision(18, 4);
        b.Property(x => x.OrigemRealizado).HasConversion<byte?>();
        b.Property(x => x.RealizadoPor).HasMaxLength(100);
        b.HasOne<MetaParticipante>().WithMany().HasForeignKey(x => x.ParticipanteId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<MetaItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.MetaId, x.ParticipanteId, x.ItemId }).IsUnique();
    }
}

/// <summary>Filtros salvos da consulta avançada de pessoas (critérios em JSON de tipo fechado).</summary>
public class FiltroSalvoConfiguration : IEntityTypeConfiguration<FiltroSalvo>
{
    public void Configure(EntityTypeBuilder<FiltroSalvo> b)
    {
        b.ToTable("FiltrosSalvos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).IsRequired().HasMaxLength(FiltroSalvo.TamanhoMaximoNome).UseCollation(EtiquetaConfiguration.CollationNome);
        b.Property(x => x.Autor).IsRequired().HasMaxLength(100);
        b.Property(x => x.Criterios).IsRequired().HasMaxLength(FiltroSalvo.TamanhoMaximoCriterios);
        b.HasIndex(x => new { x.UsuarioId, x.Ativo });
        b.HasIndex(x => x.Compartilhado).HasFilter("[Compartilhado] = 1");
    }
}
