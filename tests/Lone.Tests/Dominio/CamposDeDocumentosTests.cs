using Lone.Domain.CamposPersonalizados;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

public class CamposDeDocumentosTests
{
    private static CampoPersonalizado Campo(TipoCampoPersonalizado tipo, bool obrigatorio = false, bool visivel = true) => new()
    {
        Id = Guid.NewGuid(), Nome = tipo.ToString(), Tipo = tipo, Obrigatorio = obrigatorio, Visivel = visivel,
        Entidade = EntidadePersonalizavel.Documento, TipoDocumentoId = Guid.NewGuid()
    };

    [Theory]
    [InlineData("529.982.247-25", "52998224725", null)]
    [InlineData("111.111.111-11", "11111111111", "CPF inválido")]
    public void Cpf_e_guardado_sem_mascara_e_conferido(string digitado, string gravado, string? problema)
    {
        var campo = Campo(TipoCampoPersonalizado.Cpf);
        var valor = new DocumentoValorPersonalizado { CampoId = campo.Id, ValorTexto = digitado };

        Assert.Equal(problema, campo.Definicao.Normalizar(campo, valor));
        Assert.Equal(gravado, valor.ValorTexto);
    }

    [Fact]
    public void Cnpj_invalido_e_recusado()
    {
        var campo = Campo(TipoCampoPersonalizado.Cnpj);
        var erros = ValidadorValoresPersonalizados.Aplicar<DocumentoValorPersonalizado>(
            [new DocumentoValorPersonalizado { CampoId = campo.Id, ValorTexto = "11.111.111/1111-11" }], [campo],
            new List<DocumentoValorPersonalizado>(), "documento 1");

        Assert.Equal(new[] { "Cnpj: CNPJ inválido." }, erros);
    }

    [Fact]
    public void Campo_oculto_mantem_o_gravado_e_obrigatorio_cita_o_documento()
    {
        var oculto = Campo(TipoCampoPersonalizado.Texto, visivel: false);
        var obrigatorio = Campo(TipoCampoPersonalizado.Texto, obrigatorio: true);
        var documentoId = Guid.NewGuid();
        var gravado = new DocumentoValorPersonalizado { Id = Guid.NewGuid(), PessoaDocumentoId = documentoId, CampoId = oculto.Id, ValorTexto = "da integração" };
        var enviados = new List<DocumentoValorPersonalizado> { new() { CampoId = oculto.Id, ValorTexto = "alterado na tela" } };

        var erros = ValidadorValoresPersonalizados.Aplicar(enviados, [oculto, obrigatorio], [gravado], "documento 2");

        var mantido = Assert.Single(enviados);
        Assert.Equal("da integração", mantido.ValorTexto);
        Assert.Equal(documentoId, mantido.PessoaDocumentoId); // a cópia leva o documento junto
        Assert.Equal(new[] { "Informe \"Texto\" (documento 2)." }, erros);
    }

    [Fact]
    public void Regras_do_campo_de_documento()
    {
        var semTipo = new CampoPersonalizado { Nome = "Categoria", Entidade = EntidadePersonalizavel.Documento };
        var ocultoObrigatorio = new CampoPersonalizado { Nome = "Código", Obrigatorio = true, Visivel = false };
        var pessoaComTipo = new CampoPersonalizado { Nome = "Time", TipoDocumentoId = Guid.NewGuid(), Tipo = TipoCampoPersonalizado.Inteiro, Pesquisavel = true };

        RegrasCampoPersonalizado.Normalizar(pessoaComTipo);

        Assert.Contains("Escolha o tipo de documento a que o campo pertence.", RegrasCampoPersonalizado.Validar(semTipo));
        Assert.Contains(RegrasCampoPersonalizado.Validar(ocultoObrigatorio), e => e.StartsWith("Um campo oculto", StringComparison.Ordinal));
        Assert.Null(pessoaComTipo.TipoDocumentoId); // campo de pessoa não tem tipo de documento
        Assert.False(pessoaComTipo.Pesquisavel);     // número não entra na busca por texto
    }
}
