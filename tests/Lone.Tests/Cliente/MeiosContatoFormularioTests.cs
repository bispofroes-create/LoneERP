using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Contatos;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class MeiosContatoFormularioTests
{
    private static readonly TipoMeioContatoDto Comercial = new() { Id = Guid.NewGuid(), Nome = "Comercial", Categoria = CategoriaMeioContato.Telefone, Ativo = true };
    private static readonly TipoMeioContatoDto EmailPessoal = new() { Id = Guid.NewGuid(), Nome = "Pessoal", Categoria = CategoriaMeioContato.Email, Ativo = true };

    private static PessoaDto ComTelefone() => new()
    {
        Id = Guid.NewGuid(), Nome = "Ana",
        MeiosContato = [new MeioContatoDto { Id = Guid.NewGuid(), Tipo = TipoContato.Celular, Valor = "31987654321", TipoMeioContatoId = Comercial.Id, WhatsApp = true }]
    };

    [Fact]
    public void Remover_telefone_gravado_desativa_e_esconde_mas_ele_volta_no_envio()
    {
        var f = PessoaFormulario.De(ComTelefone(), tiposMeio: [Comercial, EmailPessoal]);
        var meio = Assert.Single(f.MeiosContato);

        meio.RemoverCommand.Execute(null);

        Assert.Single(f.MeiosContato);
        Assert.False(meio.Visivel);
        Assert.True(f.TemMeiosInativos);
        var enviado = Assert.Single(f.ParaDto().MeiosContato);
        Assert.False(enviado.Ativo);

        f.MostrarMeiosInativos = true;
        Assert.True(meio.Visivel);
        meio.ReativarCommand.Execute(null);
        Assert.True(f.ParaDto().MeiosContato[0].Ativo);
    }

    [Fact]
    public void Remover_telefone_ainda_nao_gravado_tira_da_lista()
    {
        var f = PessoaFormulario.NovaPessoa();
        var meio = new MeioContatoFormulario { Valor = "31987654321" };
        f.AdicionarMeio(meio);

        meio.RemoverCommand.Execute(null);

        Assert.Empty(f.MeiosContato);
    }

    [Fact]
    public void Classificacao_acompanha_a_categoria_do_tipo()
    {
        var f = PessoaFormulario.De(ComTelefone(), tiposMeio: [Comercial, EmailPessoal]);
        var meio = f.MeiosContato[0];

        Assert.Equal("Comercial", meio.Classificacao.Texto);
        Assert.Equal(Comercial.Id, f.ParaDto().MeiosContato[0].TipoMeioContatoId);
        Assert.True(f.ParaDto().MeiosContato[0].WhatsApp);

        meio.Tipo = Opcao.De(OpcoesPessoa.TiposContato, TipoContato.Email);

        Assert.Equal(new[] { "—", "Pessoal" }, meio.Classificacoes.Select(c => c.Texto));
        var dto = f.ParaDto().MeiosContato[0];
        Assert.Null(dto.TipoMeioContatoId);
        Assert.False(dto.WhatsApp);
    }

    [Fact]
    public void Sem_a_lista_de_tipos_a_classificacao_gravada_volta_intacta()
    {
        var f = PessoaFormulario.De(ComTelefone());

        Assert.Equal(Comercial.Id, f.ParaDto().MeiosContato[0].TipoMeioContatoId);
    }
}
