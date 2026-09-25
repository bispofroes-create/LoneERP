using Lone.Domain.Documentos;

namespace Lone.Tests.Dominio;

public class AnexosTests
{
    private static readonly byte[] Pdf = [.. "%PDF-1.7\n"u8.ToArray(), 1, 2, 3];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
    private static readonly byte[] Exe = [.. "MZ"u8.ToArray(), 0x90, 0];

    [Fact]
    public void Formato_e_identificado_pelo_conteudo()
    {
        Assert.Equal("application/pdf", RegrasAnexo.IdentificarTipo(Pdf));
        Assert.Equal("image/png", RegrasAnexo.IdentificarTipo(Png));
        Assert.Equal("image/jpeg", RegrasAnexo.IdentificarTipo(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.Null(RegrasAnexo.IdentificarTipo(Exe));
    }

    [Fact]
    public void Executavel_renomeado_e_extensao_trocada_sao_recusados()
    {
        Assert.Single(RegrasAnexo.Validar("cnh.pdf", Exe, 10, out _));
        Assert.Single(RegrasAnexo.Validar("cnh.pdf", Png, 10, out _));
        Assert.Empty(RegrasAnexo.Validar("CNH.PNG", Png, 10, out var tipo));
        Assert.Equal("image/png", tipo);
    }

    [Fact]
    public void Vazio_e_acima_do_limite_sao_recusados()
    {
        Assert.Equal(new[] { "O arquivo está vazio." }, RegrasAnexo.Validar("a.pdf", [], 10, out _));
        var grande = new byte[1024 * 1024 + 1];
        Pdf.CopyTo(grande, 0);
        Assert.Contains("O arquivo passa do limite de 1 MB.", RegrasAnexo.Validar("a.pdf", grande, 1, out _));
    }

    [Theory]
    [InlineData(@"C:\Users\ana\Documentos\cnh.pdf", "cnh.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("  ", "arquivo")]
    public void Nome_fica_sem_pastas(string enviado, string esperado) => Assert.Equal(esperado, RegrasAnexo.NomeSeguro(enviado));

    [Fact]
    public void Nome_longo_mantem_a_extensao()
    {
        var nome = RegrasAnexo.NomeSeguro(new string('a', 300) + ".pdf");
        Assert.Equal(Lone.Domain.Entidades.AnexoDocumento.TamanhoMaximoNome, nome.Length);
        Assert.EndsWith(".pdf", nome);
    }
}
