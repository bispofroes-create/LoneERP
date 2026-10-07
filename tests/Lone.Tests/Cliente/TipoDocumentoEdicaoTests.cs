using Lone.Cliente.ViewModels.Cadastros;
using Lone.Contracts.Documentos;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>
/// P1-8A: a ficha do tipo de documento ainda não mostra as regras novas, mas também não as desfaz — salvar o nome ou a
/// validade devolve à API exatamente a configuração que veio.
/// </summary>
public class TipoDocumentoEdicaoTests
{
    [Fact]
    public void Salvar_pela_tela_atual_devolve_as_regras_como_vieram()
    {
        var gravado = new TipoDocumentoDto
        {
            Id = Guid.NewGuid(), Nome = "RG", Ordem = 1, TipoSistema = TipoDocumento.Rg, ExigeValidade = false, DiasAvisoVencimento = 15,
            AplicaPessoaFisica = true, AplicaPessoaJuridica = false, AplicaEstrangeiro = false,
            UsoOrgaoEmissor = UsoCampoDocumento.Obrigatorio, UsoUf = UsoCampoDocumento.Opcional, UsoEmissao = UsoCampoDocumento.Oculto,
            FormatoNumero = FormatoNumeroDocumento.Alfanumerico, TamanhoMinimoNumero = 5, TamanhoMaximoNumero = 14,
            Unicidade = UnicidadeDocumento.Aviso
        };

        var edicao = TipoDocumentoEdicao.De(gravado);
        edicao.Nome = "RG ";
        var enviado = edicao.ParaDto();

        Assert.Equal("RG", enviado.Nome);
        Assert.Equal(
            (gravado.AplicaPessoaFisica, gravado.AplicaPessoaJuridica, gravado.AplicaEstrangeiro, gravado.UsoOrgaoEmissor, gravado.UsoUf,
             gravado.UsoEmissao, gravado.FormatoNumero, gravado.TamanhoMinimoNumero, gravado.TamanhoMaximoNumero, gravado.Unicidade),
            (enviado.AplicaPessoaFisica, enviado.AplicaPessoaJuridica, enviado.AplicaEstrangeiro, enviado.UsoOrgaoEmissor, enviado.UsoUf,
             enviado.UsoEmissao, enviado.FormatoNumero, enviado.TamanhoMinimoNumero, enviado.TamanhoMaximoNumero, enviado.Unicidade));
    }

    [Fact]
    public void Tipo_novo_vai_com_os_padroes_de_sempre()
    {
        var enviado = TipoDocumentoEdicao.Criar().ParaDto();
        var padrao = new TipoDocumentoDto();
        Assert.Equal((padrao.AplicaPessoaJuridica, padrao.UsoOrgaoEmissor, padrao.UsoEmissao, padrao.Unicidade),
                     (enviado.AplicaPessoaJuridica, enviado.UsoOrgaoEmissor, enviado.UsoEmissao, enviado.Unicidade));
    }
}
