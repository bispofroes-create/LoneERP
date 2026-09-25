using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Documentos;
using Lone.Contracts.Pessoas;
using Lone.Domain.Documentos;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class DocumentosFormularioTests
{
    private static readonly TipoDocumentoDto Cnh = new()
    {
        Id = TiposDocumentoSistema.Id(TipoDocumento.Cnh), Nome = "CNH", Ordem = 2, TipoSistema = TipoDocumento.Cnh, ExigeValidade = true, DiasAvisoVencimento = 30
    };

    private static readonly TipoDocumentoDto Alvara = new() { Id = Guid.NewGuid(), Nome = "Alvará", Ordem = 10, DiasAvisoVencimento = 60 };
    private static readonly TipoDocumentoDto Antigo = new() { Id = Guid.NewGuid(), Nome = "Carteira antiga", Ordem = 11, Ativo = false };

    private static PessoaDto Com(params DocumentoDto[] documentos) => new() { Id = Guid.NewGuid(), Nome = "Ana", Documentos = [.. documentos] };

    [Fact]
    public void Remover_documento_gravado_desativa_e_ele_volta_no_envio()
    {
        var f = PessoaFormulario.De(Com(new DocumentoDto { Id = Guid.NewGuid(), TipoDocumentoId = Alvara.Id, Numero = "A1" }), tiposDocumento: [Cnh, Alvara]);
        var documento = Assert.Single(f.Documentos);

        documento.RemoverCommand.Execute(null);

        Assert.False(documento.Visivel);
        Assert.True(f.TemDocumentosInativos);
        Assert.False(Assert.Single(f.ParaDto().Documentos).Ativo);

        f.MostrarDocumentosInativos = true;
        Assert.True(documento.Visivel);
        documento.ReativarCommand.Execute(null);
        Assert.True(f.ParaDto().Documentos[0].Ativo);
    }

    [Fact]
    public void Remover_documento_novo_tira_da_lista()
    {
        var f = PessoaFormulario.NovaPessoa(tiposDocumento: [Cnh]);
        var documento = new DocumentoFormulario();
        f.AdicionarDocumento(documento);

        documento.RemoverCommand.Execute(null);

        Assert.Empty(f.Documentos);
    }

    [Fact]
    public void Aviso_de_vencimento_usa_a_antecedencia_do_tipo()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var f = PessoaFormulario.De(Com(
            new DocumentoDto { Id = Guid.NewGuid(), TipoDocumentoId = Alvara.Id, Numero = "A1", ValidoAte = hoje.AddDays(45) },
            new DocumentoDto { Id = Guid.NewGuid(), TipoDocumentoId = Cnh.Id, Numero = "C1", ValidoAte = hoje.AddDays(-2) }), tiposDocumento: [Cnh, Alvara]);

        Assert.Equal("Vence em 45 dias", f.Documentos[0].AvisoValidade); // alvará avisa com 60 dias
        Assert.True(f.Documentos[1].Vencido);
        Assert.Equal("1 documento vencido, 1 vence em breve.", f.AvisoDocumentos);

        f.Documentos[1].RemoverCommand.Execute(null); // inativo não conta
        Assert.Equal("1 vence em breve.", f.AvisoDocumentos);
    }

    [Fact]
    public void Validade_obrigatoria_do_tipo_e_conferida_na_tela()
    {
        var f = PessoaFormulario.NovaPessoa(tiposDocumento: [Cnh, Alvara]);
        var documento = new DocumentoFormulario { Numero = "123" };
        f.AdicionarDocumento(documento);
        documento.Tipo = documento.Tipos.First(o => o.Valor == Cnh.Id);

        Assert.Contains(f.ValidarLocalmente(), e => e == "CNH 123: informe a validade.");
        Assert.Equal("Válido até (obrigatório)", documento.RotuloValidade);
    }

    [Fact]
    public void Tipo_desativado_aparece_so_para_quem_ja_tinha_e_novo_nao_o_oferece()
    {
        var f = PessoaFormulario.De(Com(new DocumentoDto { Id = Guid.NewGuid(), TipoDocumentoId = Antigo.Id, Numero = "1" }), tiposDocumento: [Cnh, Alvara, Antigo]);

        Assert.Equal("Carteira antiga (desativado)", f.Documentos[0].Tipo.Texto);
        Assert.Equal(Antigo.Id, f.ParaDto().Documentos[0].TipoDocumentoId);

        var novo = new DocumentoFormulario();
        f.AdicionarDocumento(novo);
        Assert.DoesNotContain(novo.Tipos, o => o.Valor == Antigo.Id);
    }

    [Fact]
    public void Sem_a_lista_de_tipos_o_tipo_gravado_volta_intacto_e_o_legado_vira_tipo_de_sistema()
    {
        var f = PessoaFormulario.De(Com(
            new DocumentoDto { Id = Guid.NewGuid(), TipoDocumentoId = Alvara.Id, Numero = "1" },
            new DocumentoDto { Id = Guid.NewGuid(), Tipo = TipoDocumento.Passaporte, Numero = "2" }));

        var enviados = f.ParaDto().Documentos;
        Assert.Equal(Alvara.Id, enviados[0].TipoDocumentoId);
        Assert.Equal(TipoDocumento.Outro, enviados[0].Tipo);
        Assert.Equal(TiposDocumentoSistema.Id(TipoDocumento.Passaporte), enviados[1].TipoDocumentoId);
        Assert.Equal(TipoDocumento.Passaporte, enviados[1].Tipo);
    }
}
