using Lone.Domain.Entidades;
using Lone.Domain.Enderecos.ConferenciaCep;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Cache postal persistente (F3). Chave = CEP ou UF + cidade + logradouro normalizados (a mesma da consulta externa e do
/// cache em memória); nenhuma coluna de pessoa.
/// </summary>
public class CacheCepConfiguration : IEntityTypeConfiguration<CacheCep>
{
    public const int TamanhoChave = 300;

    public void Configure(EntityTypeBuilder<CacheCep> b)
    {
        b.ToTable("CacheCep");
        b.HasKey(c => c.Chave);
        b.Property(c => c.Chave).HasMaxLength(TamanhoChave);
        b.Property(c => c.Cep).HasMaxLength(8).IsUnicode(false);
        b.Property(c => c.Registros).IsRequired();
    }
}

/// <summary>
/// Histórico técnico das consultas de CEP (F3). Índice por data para a retenção (limpeza futura; recomendação 90 dias).
/// Sem pessoa, sem número, sem bairro, sem complemento.
/// </summary>
public class ConsultaCepConfiguration : IEntityTypeConfiguration<ConsultaCep>
{
    public void Configure(EntityTypeBuilder<ConsultaCep> b)
    {
        b.ToTable("ConsultasCep");
        b.HasKey(c => c.Id);
        b.Property(c => c.Chave).IsRequired().HasMaxLength(CacheCepConfiguration.TamanhoChave);
        b.Property(c => c.Cep).HasMaxLength(8).IsUnicode(false);
        b.HasIndex(c => c.OcorridoEm);
    }
}
