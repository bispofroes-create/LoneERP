using Lone.Domain.CamposPersonalizados;
using Lone.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

public class CampoPersonalizadoConfiguration : IEntityTypeConfiguration<CampoPersonalizado>
{
    public void Configure(EntityTypeBuilder<CampoPersonalizado> b)
    {
        b.ToTable("CamposPersonalizados");
        b.HasKey(c => c.Id);
        b.Ignore(c => c.Definicao);

        b.Property(c => c.Entidade).HasConversion<byte>();
        b.Property(c => c.Tipo).HasConversion<byte>();
        b.Property(c => c.Nome).IsRequired().HasMaxLength(CampoPersonalizado.TamanhoMaximoNome);
        b.Property(c => c.Dica).HasMaxLength(CampoPersonalizado.TamanhoMaximoDica);
        b.Property(c => c.Minimo).HasPrecision(19, 6);
        b.Property(c => c.Maximo).HasPrecision(19, 6);

        // Nome único por cadastro — e, nos documentos, por tipo de documento ("Categoria" pode existir na CNH e em outro
        // tipo) —, inclusive entre os desativados, para o histórico não ficar ambíguo.
        // Sem filtro: o EF filtraria "TipoDocumentoId IS NOT NULL" e os campos da pessoa (nulo) ficariam sem unicidade.
        b.HasIndex(c => new { c.Entidade, c.TipoDocumentoId, c.Nome }).IsUnique().HasFilter(null);
        b.HasOne<TipoDocumentoCadastro>().WithMany().HasForeignKey(c => c.TipoDocumentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => new { c.Entidade, c.Ativo, c.Ordem });

        b.HasMany(c => c.Opcoes).WithOne().HasForeignKey(o => o.CampoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CampoPersonalizadoOpcaoConfiguration : IEntityTypeConfiguration<CampoPersonalizadoOpcao>
{
    public void Configure(EntityTypeBuilder<CampoPersonalizadoOpcao> b)
    {
        b.ToTable("CampoPersonalizadoOpcoes");
        b.HasKey(o => o.Id);
        b.Property(o => o.Texto).IsRequired().HasMaxLength(CampoPersonalizadoOpcao.TamanhoMaximoTexto);
        b.HasIndex(o => new { o.CampoId, o.Ordem });
    }
}

/// <summary>Colunas e índices comuns às tabelas de valores personalizados (pessoa e documentos).</summary>
internal static class ConfiguracaoValorPersonalizado
{
    /// <summary>Coluna calculada (persistida) com o começo do texto: o índice de busca não pode usar o texto inteiro
    /// (nvarchar(2000) passa do limite de 1.700 bytes da chave de índice do SQL Server).</summary>
    public const string ColunaBusca = "ValorTextoBusca";
    public const int TamanhoBusca = 200;

    public static void Configurar<T>(EntityTypeBuilder<T> b) where T : ValorPersonalizado
    {
        b.HasKey(v => v.Id);
        b.Ignore(v => v.Vazio);

        b.Property(v => v.ValorTexto).HasMaxLength(TiposCampo.TamanhoMaximoTexto);
        b.Property(v => v.ValorNumero).HasPrecision(19, 6);
        b.Property<string?>(ColunaBusca).HasMaxLength(TamanhoBusca)
            .HasComputedColumnSql($"CAST(LEFT([ValorTexto], {TamanhoBusca}) AS nvarchar({TamanhoBusca}))", stored: true);

        // Campos e opções nunca são apagados (só desativados): Restrict protege os valores gravados.
        b.HasOne<CampoPersonalizado>().WithMany().HasForeignKey(v => v.CampoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CampoPersonalizadoOpcao>().WithMany().HasForeignKey(v => v.OpcaoId).OnDelete(DeleteBehavior.Restrict);

        // Filtros e relatórios por valor ("quantidade de filhos > 2", "time = Atlético", "categoria da CNH = D").
        b.HasIndex(v => new { v.CampoId, v.ValorNumero });
        b.HasIndex(v => new { v.CampoId, v.ValorData });
        b.HasIndex(v => new { v.CampoId, v.OpcaoId });
        b.HasIndex("CampoId", ColunaBusca);
    }
}

public class DocumentoValorPersonalizadoConfiguration : IEntityTypeConfiguration<DocumentoValorPersonalizado>
{
    public void Configure(EntityTypeBuilder<DocumentoValorPersonalizado> b)
    {
        b.ToTable("PessoaDocumentoValoresPersonalizados");
        ConfiguracaoValorPersonalizado.Configurar(b);
        // Documento nunca é apagado (desativa); NoAction evita dois caminhos de cascata a partir da pessoa.
        b.HasOne<PessoaDocumento>().WithMany().HasForeignKey(v => v.PessoaDocumentoId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(v => new { v.PessoaDocumentoId, v.CampoId }).IsUnique(); // um valor por campo em cada documento
        b.HasIndex(v => v.PessoaId);
    }
}

public class PessoaValorPersonalizadoConfiguration : IEntityTypeConfiguration<PessoaValorPersonalizado>
{
    public void Configure(EntityTypeBuilder<PessoaValorPersonalizado> b)
    {
        b.ToTable("PessoaValoresPersonalizados");
        ConfiguracaoValorPersonalizado.Configurar(b);

        b.HasIndex(v => new { v.PessoaId, v.CampoId }).IsUnique(); // um valor por campo
    }
}
