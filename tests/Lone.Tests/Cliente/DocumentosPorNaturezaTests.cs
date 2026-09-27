using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Documentos;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Documentos;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Documentos por natureza (RG só na PF, documento novo sem tipo) e ajustes do Fiscal da PJ.</summary>
public class DocumentosPorNaturezaTests
{
    private static TipoDocumentoDto Sistema(TipoDocumento tipo, string nome, int ordem) =>
        new() { Id = TiposDocumentoSistema.Id(tipo), Nome = nome, Ordem = ordem, TipoSistema = tipo, Ativo = true };

    private static readonly TipoDocumentoDto Rg = Sistema(TipoDocumento.Rg, "RG", 1);
    private static readonly TipoDocumentoDto Cnh = Sistema(TipoDocumento.Cnh, "CNH", 2);
    private static readonly TipoDocumentoDto Passaporte = Sistema(TipoDocumento.Passaporte, "Passaporte", 3);
    private static readonly TipoDocumentoDto Estrangeiro = Sistema(TipoDocumento.DocumentoEstrangeiro, "Documento estrangeiro", 4);
    private static readonly TipoDocumentoDto Alvara = new() { Id = Guid.NewGuid(), Nome = "Alvará", Ordem = 10, Ativo = true };
    private static readonly TipoDocumentoDto[] Todos = [Rg, Cnh, Passaporte, Estrangeiro, Alvara];

    private static PessoaFormulario Nova(NaturezaPessoa natureza)
    {
        var f = PessoaFormulario.NovaPessoa(tiposDocumento: Todos);
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, natureza);
        return f;
    }

    [Fact]
    public void Documento_novo_comeca_sem_tipo_e_nao_grava_sem_escolher()
    {
        var f = Nova(NaturezaPessoa.Fisica);
        var documento = new DocumentoFormulario { Numero = "123" };
        f.AdicionarDocumento(documento);

        Assert.Equal(Guid.Empty, documento.Tipo.Valor);
        Assert.Contains(f.ValidarLocalmente(), e => e == "Documento 123: escolha o tipo.");

        documento.Tipo = documento.Tipos.First(o => o.Valor == Rg.Id);
        Assert.DoesNotContain(f.ValidarLocalmente(), e => e.Contains("escolha o tipo"));
    }

    [Fact]
    public void Cada_natureza_ve_so_os_tipos_que_se_aplicam()
    {
        static Guid[] Tipos(PessoaFormulario f)
        {
            var d = new DocumentoFormulario();
            f.AdicionarDocumento(d);
            return d.Tipos.Select(o => o.Valor).Where(v => v != Guid.Empty).ToArray();
        }

        Assert.Equal(new[] { Rg.Id, Cnh.Id, Passaporte.Id, Alvara.Id }, Tipos(Nova(NaturezaPessoa.Fisica)));
        Assert.Equal(new[] { Alvara.Id }, Tipos(Nova(NaturezaPessoa.Juridica)));
        Assert.Equal(new[] { Passaporte.Id, Estrangeiro.Id, Alvara.Id }, Tipos(Nova(NaturezaPessoa.Estrangeiro)));
    }

    [Fact]
    public void Trocar_a_natureza_tira_o_tipo_que_nao_se_aplica_do_documento_novo()
    {
        var f = Nova(NaturezaPessoa.Fisica);
        var documento = new DocumentoFormulario();
        f.AdicionarDocumento(documento);
        documento.Tipo = documento.Tipos.First(o => o.Valor == Rg.Id);

        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);

        Assert.Equal(Guid.Empty, documento.Tipo.Valor);
        Assert.DoesNotContain(documento.Tipos, o => o.Valor == Rg.Id);
    }

    [Fact]
    public void Rg_gravado_numa_empresa_continua_como_esta()
    {
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Natureza = NaturezaPessoa.Juridica, Nome = "ABC Ltda",
            Estabelecimentos = [new EstabelecimentoDto { Id = Guid.NewGuid(), Principal = true, Cnpj = "11222333000181" }],
            Documentos = [new DocumentoDto { Id = Guid.NewGuid(), TipoDocumentoId = Rg.Id, Numero = "MG123", OrgaoEmissor = "SSP" }]
        }, tiposDocumento: Todos);

        var documento = Assert.Single(f.Documentos);
        Assert.Equal(Rg.Id, documento.Tipo.Valor);
        Assert.Equal(Rg.Id, f.ParaDto().Documentos[0].TipoDocumentoId);
        Assert.True(documento.MostrarOrgaoEmissor); // já preenchido: continua à vista
    }

    [Fact]
    public void Orgao_emissor_e_uf_so_nos_documentos_pessoais()
    {
        var f = Nova(NaturezaPessoa.Fisica);
        var documento = new DocumentoFormulario();
        f.AdicionarDocumento(documento);

        documento.Tipo = documento.Tipos.First(o => o.Valor == Rg.Id);
        Assert.True(documento.MostrarOrgaoEmissor);
        Assert.True(documento.MostrarUf);

        documento.Tipo = documento.Tipos.First(o => o.Valor == Passaporte.Id);
        Assert.True(documento.MostrarOrgaoEmissor);
        Assert.False(documento.MostrarUf);

        documento.Tipo = documento.Tipos.First(o => o.Valor == Alvara.Id);
        Assert.False(documento.MostrarOrgaoEmissor);
        Assert.False(documento.MostrarUf);
    }

    [Fact]
    public void Documento_principal_aparece_em_uma_linha()
    {
        var f = Nova(NaturezaPessoa.Juridica);
        Assert.Equal("CNPJ não informado · alterado na aba \"Identificação\"", f.DocumentoPrincipalResumo);
        f.Principal.Cnpj = "11.222.333/0001-81";
        Assert.Equal("CNPJ 11.222.333/0001-81 · alterado na aba \"Identificação\"", f.DocumentoPrincipalResumo);
    }

    // ---- Fiscal da PJ ----

    [Fact]
    public void Natureza_juridica_aparece_com_a_descricao()
    {
        var f = Nova(NaturezaPessoa.Juridica);
        f.Principal.NaturezaJuridica = "2046";
        Assert.Equal("204-6 · Sociedade Anônima Aberta", f.Principal.NaturezaJuridicaTexto);
    }

    [Fact]
    public void Consulta_sem_simples_preenche_regime_normal_sem_trocar_o_escolhido()
    {
        var f = Nova(NaturezaPessoa.Juridica);
        f.Principal.AplicarCnpj(new DadosCnpj { Cnpj = "33041260065290", CnaePrincipal = "4753900" }); // sem registro no Simples
        Assert.Equal(RegimeTributario.RegimeNormal, f.Principal.Regime.Valor);

        var escolhido = Nova(NaturezaPessoa.Juridica);
        escolhido.Principal.Regime = Opcao.De(OpcoesPessoa.Regimes, RegimeTributario.SimplesNacional);
        escolhido.Principal.AplicarCnpj(new DadosCnpj { Cnpj = "33041260065290", CnaePrincipal = "4753900" });
        Assert.Equal(RegimeTributario.SimplesNacional, escolhido.Principal.Regime.Valor);

        escolhido.Principal.AplicarCnpj(new DadosCnpj { Cnpj = "33041260065290", CnaePrincipal = "4753900", OpcaoSimples = false });
        Assert.Equal(RegimeTributario.RegimeNormal, escolhido.Principal.Regime.Valor); // a Receita diz que não é optante
    }
}
