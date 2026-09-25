using Lone.Domain.Contatos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

public class MeiosContatoTests
{
    private static Pessoa ComMeios(params MeioContato[] meios)
    {
        var p = new Pessoa { Nome = "Ana" };
        p.MeiosContato.AddRange(meios);
        return p;
    }

    [Fact]
    public void Tipo_WhatsApp_antigo_vira_celular_com_a_marcacao()
    {
        var p = ComMeios(new MeioContato { Id = Guid.NewGuid(), Tipo = TipoContato.WhatsApp, Valor = "(31) 98765-4321" });

        PessoaNormalizador.Normalizar(p, new DateOnly(2026, 9, 25));

        Assert.Equal(TipoContato.Celular, p.MeiosContato[0].Tipo);
        Assert.True(p.MeiosContato[0].WhatsApp);
        Assert.Equal("31987654321", p.MeiosContato[0].Valor);
    }

    [Fact]
    public void Campos_que_nao_servem_para_o_tipo_sao_limpos()
    {
        var email = new MeioContato { Id = Guid.NewGuid(), Tipo = TipoContato.Email, Valor = "a@b.com.br", WhatsApp = true, Ramal = "12", Finalidades = FinalidadeEmail.NFe };
        var celular = new MeioContato { Id = Guid.NewGuid(), Tipo = TipoContato.Celular, Valor = "31987654321", Ramal = "12", Finalidades = FinalidadeEmail.Marketing };
        var fixo = new MeioContato { Id = Guid.NewGuid(), Tipo = TipoContato.Telefone, Valor = "3133334444", Ramal = " 2-15 " };

        PessoaNormalizador.Normalizar(ComMeios(email, celular, fixo), new DateOnly(2026, 9, 25));

        Assert.False(email.WhatsApp);
        Assert.Null(email.Ramal);
        Assert.Equal(FinalidadeEmail.NFe, email.Finalidades);
        Assert.Null(celular.Ramal);
        Assert.Equal(FinalidadeEmail.Nenhuma, celular.Finalidades);
        Assert.Equal("215", fixo.Ramal);
    }

    [Fact]
    public void Inativo_nunca_e_o_principal()
    {
        var antigo = new MeioContato { Id = Guid.NewGuid(), Tipo = TipoContato.Celular, Valor = "31987654321", Principal = true, Ativo = false };
        var novo = new MeioContato { Id = Guid.NewGuid(), Tipo = TipoContato.Celular, Valor = "31999998888" };

        PessoaNormalizador.Normalizar(ComMeios(antigo, novo), new DateOnly(2026, 9, 25));

        Assert.False(antigo.Principal);
        Assert.True(novo.Principal);
    }

    [Fact]
    public void Classificacao_precisa_ser_da_mesma_categoria_e_ativa_para_escolha_nova()
    {
        var comercialEmail = new TipoMeioContato { Id = Guid.NewGuid(), Nome = "Comercial", Categoria = CategoriaMeioContato.Email };
        var antigoTelefone = new TipoMeioContato { Id = Guid.NewGuid(), Nome = "Recado", Categoria = CategoriaMeioContato.Telefone, Ativo = false };
        var cadastro = new[] { comercialEmail, antigoTelefone }.ToDictionary(t => t.Id);
        var celular = new MeioContato { Id = Guid.NewGuid(), Tipo = TipoContato.Celular, TipoMeioContatoId = comercialEmail.Id };
        var recado = new MeioContato { Id = Guid.NewGuid(), Tipo = TipoContato.Telefone, TipoMeioContatoId = antigoTelefone.Id };

        var erros = RegrasMeioContato.ValidarTipos([celular, recado], new Dictionary<Guid, Guid?>(), cadastro);
        var jaTinha = RegrasMeioContato.ValidarTipos([recado], new Dictionary<Guid, Guid?> { [recado.Id] = antigoTelefone.Id }, cadastro);

        Assert.Equal(2, erros.Count);
        Assert.Empty(jaTinha);
    }
}
