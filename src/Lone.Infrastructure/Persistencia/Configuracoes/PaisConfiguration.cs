using Lone.Domain.Entidades;
using Lone.Domain.Enderecos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Catálogo oficial de países da NF-e (V2-1). Os dados nascem com a base (HasData) a partir do artefato gerado da fonte
/// oficial, sem internet. O sucessor não tem FK: é uma referência informativa, conferida nos testes do catálogo.
/// </summary>
public class PaisConfiguration : IEntityTypeConfiguration<Pais>
{
    public const string CheckCodigo = "CK_Paises_CodigoPaisNFe";
    public const string CheckSucessor = "CK_Paises_CodigoSucessor";
    public const string CheckVigencia = "CK_Paises_Vigencia";

    public void Configure(EntityTypeBuilder<Pais> b)
    {
        b.ToTable("Paises", t =>
        {
            // BIN2: o intervalo [0-9] fica só nos dígitos ASCII (em collation CI_AS ele pode aceitar ¹, ², ³...).
            t.HasCheckConstraint(CheckCodigo, "[CodigoPaisNFe] COLLATE Latin1_General_BIN2 LIKE '[0-9][0-9][0-9][0-9]'");
            t.HasCheckConstraint(CheckSucessor,
                "[CodigoSucessor] IS NULL OR ([CodigoSucessor] COLLATE Latin1_General_BIN2 LIKE '[0-9][0-9][0-9][0-9]' AND [CodigoSucessor] <> [CodigoPaisNFe])");
            t.HasCheckConstraint(CheckVigencia, "[VigenciaFim] IS NULL OR [VigenciaFim] >= [VigenciaInicio]");
        });
        b.HasKey(p => p.CodigoPaisNFe);
        b.Property(p => p.CodigoPaisNFe).HasMaxLength(Pais.TamanhoCodigo).IsFixedLength().IsUnicode(false).ValueGeneratedNever();
        b.Property(p => p.NomeFiscal).IsRequired().HasMaxLength(Pais.TamanhoMaximoNome);
        b.Property(p => p.SituacaoFonte).HasMaxLength(Pais.TamanhoMaximoSituacao);
        b.Property(p => p.CodigoSucessor).HasMaxLength(Pais.TamanhoCodigo).IsFixedLength().IsUnicode(false);
        b.Property(p => p.Fonte).IsRequired().HasMaxLength(Pais.TamanhoMaximoFonte);
        b.Property(p => p.VersaoFonte).IsRequired().HasMaxLength(Pais.TamanhoMaximoVersao).IsUnicode(false);
        b.Property(p => p.HashFonte).IsRequired().HasMaxLength(Pais.TamanhoHash).IsFixedLength().IsUnicode(false);

        b.HasIndex(p => p.NomeFiscal); // pesquisa por nome

        b.HasData(PaisesNFeOficiais.Todos.Select(p => new Pais
        {
            CodigoPaisNFe = p.CodigoPaisNFe,
            NomeFiscal = p.NomeFiscal,
            SituacaoFonte = p.SituacaoFonte,
            VigenciaInicio = p.VigenciaInicio,
            VigenciaFim = p.VigenciaFim,
            CodigoSucessor = p.CodigoSucessor,
            Fonte = PaisesNFeOficiais.Fonte,
            VersaoFonte = PaisesNFeOficiais.VersaoFonte,
            HashFonte = PaisesNFeOficiais.HashFonte
        }).ToArray());
    }
}
