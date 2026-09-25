using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Documentos;
using Lone.Contracts.Pessoas;
using Lone.Domain.Documentos;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class CamposDeDocumentosFormularioTests
{
    private static readonly Guid Cnh = TiposDocumentoSistema.Id(TipoDocumento.Cnh);
    private static readonly Guid Rg = TiposDocumentoSistema.Id(TipoDocumento.Rg);

    private static readonly CampoPersonalizadoDto Categoria = new()
    {
        Id = Guid.NewGuid(), Nome = "Categoria", Tipo = TipoCampoPersonalizado.Texto, Entidade = EntidadePersonalizavel.Documento,
        TipoDocumentoId = Cnh, Obrigatorio = true
    };

    private static readonly CampoPersonalizadoDto Oculto = new()
    {
        Id = Guid.NewGuid(), Nome = "Código RENACH", Tipo = TipoCampoPersonalizado.Texto, Entidade = EntidadePersonalizavel.Documento,
        TipoDocumentoId = Cnh, Visivel = false
    };

    [Fact]
    public void Campos_acompanham_o_tipo_do_documento_e_vao_no_envio()
    {
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Ana",
            Documentos = [new DocumentoDto { Id = Guid.NewGuid(), TipoDocumentoId = Cnh, Numero = "1", ValidoAte = new DateOnly(2030, 1, 1),
                ValoresPersonalizados = [new ValorPersonalizadoDto { CampoId = Categoria.Id, Texto = "AB" }] }]
        }, camposDocumento: [Categoria, Oculto]);
        var documento = f.Documentos[0];

        var campo = Assert.Single(documento.CamposPersonalizados); // o oculto não aparece
        Assert.Equal("AB", campo.Texto);
        Assert.Equal("AB", Assert.Single(f.ParaDto().Documentos[0].ValoresPersonalizados).Texto);

        documento.Tipo = documento.Tipos.First(o => o.Valor == Rg);
        Assert.Empty(documento.CamposPersonalizados);

        documento.Tipo = documento.Tipos.First(o => o.Valor == Cnh); // voltou: o valor continua
        Assert.Equal("AB", documento.CamposPersonalizados[0].Texto);
    }

    [Fact]
    public void Obrigatorio_do_documento_e_conferido_na_tela()
    {
        var f = PessoaFormulario.NovaPessoa(camposDocumento: [Categoria]);
        var documento = new DocumentoFormulario { Numero = "1", ValidoAte = "01/01/2030" };
        f.AdicionarDocumento(documento);
        documento.Tipo = documento.Tipos.First(o => o.Valor == Cnh);

        Assert.Contains("Informe \"Categoria\" (documento CNH).", f.ValidarLocalmente());
    }

    [Fact]
    public void Sem_a_lista_de_campos_os_valores_gravados_voltam_intactos()
    {
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Ana",
            Documentos = [new DocumentoDto { Id = Guid.NewGuid(), TipoDocumentoId = Cnh, Numero = "1",
                ValoresPersonalizados = [new ValorPersonalizadoDto { CampoId = Categoria.Id, Texto = "AB" }] }]
        });

        Assert.Equal("AB", Assert.Single(f.ParaDto().Documentos[0].ValoresPersonalizados).Texto);
    }

    [Fact]
    public void Ficha_do_campo_de_documento_leva_tipo_visivel_e_pesquisavel()
    {
        var tipo = new TipoDocumentoDto { Id = Cnh, Nome = "CNH", Ativo = true };
        var edicao = CampoPersonalizadoEdicao.Novo(EntidadePersonalizavel.Documento);
        edicao.DefinirTiposDocumento([tipo]);
        edicao.Nome = "Categoria";
        edicao.Tipo = CampoPersonalizadoEdicao.Tipos.First(t => t.Valor == TipoCampoPersonalizado.Cpf);
        edicao.Pesquisavel = true;

        Assert.Contains("Escolha o tipo de documento.", edicao.ValidarLocalmente());
        edicao.TipoDocumento = edicao.TiposDocumento.First(o => o.Valor == Cnh);
        var dto = edicao.ParaDto();

        Assert.Equal(EntidadePersonalizavel.Documento, dto.Entidade);
        Assert.Equal(Cnh, dto.TipoDocumentoId);
        Assert.True(dto.Pesquisavel);
        Assert.Empty(edicao.ValidarLocalmente());
    }
}
