using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Documentos;
using Lone.Contracts.Pessoas;
using Lone.Domain.Documentos;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>
/// P1-8B: na ficha da pessoa, quem pode usar o tipo e os campos do documento (órgão, UF, emissão) vêm das regras do tipo
/// (o cadastro), não mais do enum; sem o cadastro, a regra fixa de antes continua como reserva. Valor já gravado nunca some.
/// </summary>
public class DocumentosMetadadosFormularioTests
{
    private static readonly TipoDocumentoDto Conselho = new()
    {
        Id = Guid.NewGuid(), Nome = "Registro no conselho", Ordem = 20, Ativo = true,
        AplicaPessoaFisica = true, AplicaPessoaJuridica = false, AplicaEstrangeiro = false,
        UsoOrgaoEmissor = UsoCampoDocumento.Opcional, UsoUf = UsoCampoDocumento.Obrigatorio, UsoEmissao = UsoCampoDocumento.Oculto
    };

    private static readonly TipoDocumentoDto[] Catalogo = [Conselho];

    private static DocumentoFormulario NovoDocumento(NaturezaPessoa natureza, IReadOnlyList<TipoDocumentoDto> catalogo)
    {
        var f = PessoaFormulario.NovaPessoa(tiposDocumento: catalogo);
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, natureza);
        var d = new DocumentoFormulario();
        f.AdicionarDocumento(d);
        return d;
    }

    [Fact]
    public void Campos_do_documento_seguem_o_uso_definido_no_tipo()
    {
        var d = NovoDocumento(NaturezaPessoa.Fisica, Catalogo);
        d.Tipo = d.Tipos.First(o => o.Valor == Conselho.Id);

        Assert.True(d.MostrarOrgaoEmissor);              // tipo do usuário com órgão: antes nunca aparecia
        Assert.Equal("Órgão emissor", d.RotuloOrgaoEmissor);
        Assert.True(d.MostrarUf);
        Assert.Equal("UF (obrigatória)", d.RotuloUf);
        Assert.False(d.MostrarEmissao);                   // "Não usar"
    }

    [Fact]
    public void Quem_pode_usar_vem_do_tipo()
    {
        Assert.Contains(NovoDocumento(NaturezaPessoa.Fisica, Catalogo).Tipos, o => o.Valor == Conselho.Id);
        Assert.DoesNotContain(NovoDocumento(NaturezaPessoa.Juridica, Catalogo).Tipos, o => o.Valor == Conselho.Id);
        Assert.DoesNotContain(NovoDocumento(NaturezaPessoa.Estrangeiro, Catalogo).Tipos, o => o.Valor == Conselho.Id);
    }

    [Fact]
    public void Emissao_gravada_continua_a_vista_num_tipo_que_nao_a_usa()
    {
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Natureza = NaturezaPessoa.Fisica, Nome = "Ana",
            Documentos = [new DocumentoDto { Id = Guid.NewGuid(), TipoDocumentoId = Conselho.Id, Numero = "123", Uf = "SP", EmitidoEm = new DateOnly(2020, 5, 1) }]
        }, tiposDocumento: Catalogo);

        var d = Assert.Single(f.Documentos);
        Assert.True(d.MostrarEmissao);
        Assert.Equal("01/05/2020", d.EmitidoEm);
    }

    [Fact]
    public void Sem_o_cadastro_de_tipos_vale_a_regra_fixa_de_antes()
    {
        var d = NovoDocumento(NaturezaPessoa.Fisica, []);
        d.Tipo = d.Tipos.First(o => o.Valor == TiposDocumentoSistema.Id(TipoDocumento.Rg));
        Assert.True(d.MostrarOrgaoEmissor);
        Assert.True(d.MostrarUf);
        Assert.True(d.MostrarEmissao);

        d.Tipo = d.Tipos.First(o => o.Valor == TiposDocumentoSistema.Id(TipoDocumento.Outro));
        Assert.False(d.MostrarOrgaoEmissor);
        Assert.False(d.MostrarUf);
    }
}
