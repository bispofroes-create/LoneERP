using System.Net;
using Lone.Cliente.Mensagens;
using Lone.Cliente.Navegacao;
using Lone.Contracts.Pessoas;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Cliente;

/// <summary>
/// Salvar só com o que gravar (base de todos os cadastros): sem alteração, Salvar e Descartar ficam desabilitados e a
/// barra diz "Sem alterações"; qualquer alteração — em qualquer nível da ficha — habilita na hora; voltar ao valor
/// original desabilita de novo. Menu lateral recolhido em janela estreita.
/// </summary>
public class SalvarSoComAlteracoesTests
{
    private static async Task<(Lone.Cliente.ViewModels.Pessoas.PessoasViewModel Tela, AmbienteCliente Ambiente)> PessoaGravadaAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        tela.Mensagens = new ServicoMensagens(new FakeTimeProvider());

        await tela.NovoCommand.ExecuteAsync(null);
        var f = tela.Formulario!;
        // Pessoa nova: Salvar sempre disponível (a gravação mostra o que falta); nada a descartar ainda.
        Assert.True(tela.PodeSalvarAgora);
        Assert.False(tela.PodeDescartar);
        Assert.Equal("Novo cadastro, ainda não salvo", tela.EstadoFicha);

        f.Nome = "Carlos";
        Assert.True(tela.PodeDescartar);
        Assert.Equal("Alterações não salvas", tela.EstadoFicha);

        f.Enderecos[0].Municipio.Definir(3550308, "São Paulo", "SP");
        var gravada = f.ParaDto();
        gravada.Codigo = 7;
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, new ResultadoSalvarPessoa { Pessoa = gravada, Avisos = [] })
            .Responder(HttpStatusCode.OK, new PaginaListaPessoas());
        await tela.SalvarCommand.ExecuteAsync(null);
        return (tela, ambiente);
    }

    [Fact]
    public async Task Depois_de_gravar_sem_alteracao_Salvar_e_Descartar_ficam_desabilitados()
    {
        var (tela, _) = await PessoaGravadaAsync();

        Assert.False(tela.Formulario!.Nova);
        Assert.False(tela.PodeSalvarAgora);
        Assert.False(tela.PodeDescartar);
        Assert.False(tela.DescartarCommand.CanExecute(null));
        Assert.Equal("Sem alterações", tela.EstadoFicha);
    }

    [Fact]
    public async Task Alterar_habilita_na_hora_e_voltar_ao_valor_gravado_desabilita()
    {
        var (tela, _) = await PessoaGravadaAsync();
        var f = tela.Formulario!;
        var avisos = new List<string?>();
        tela.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        f.Nome = "Carlos Souza";
        Assert.True(tela.PodeSalvarAgora);
        Assert.True(tela.DescartarCommand.CanExecute(null));
        Assert.Equal("Alterações não salvas", tela.EstadoFicha);
        Assert.Contains(nameof(tela.PodeSalvarAgora), avisos); // a tela recebe o aviso (o botão acende sozinho)

        f.Nome = "Carlos"; // desfez à mão: não há o que gravar
        Assert.False(tela.PodeSalvarAgora);
        Assert.Equal("Sem alterações", tela.EstadoFicha);
    }

    [Fact]
    public async Task Alteracao_em_nivel_interno_e_item_novo_em_lista_tambem_contam()
    {
        var (tela, _) = await PessoaGravadaAsync();
        var f = tela.Formulario!;

        f.Enderecos[0].Logradouro = "Rua Nova"; // dentro do endereço
        Assert.True(tela.PodeSalvarAgora);
        f.Enderecos[0].Logradouro = string.Empty;
        Assert.False(tela.PodeSalvarAgora);

        tela.AdicionarEmailCommand.Execute(null); // item novo numa lista...
        var email = f.MeiosContato.Last(m => m.NaListaEmails);
        email.Valor = "carlos@exemplo.com";       // ...e o campo do item novo (a estrutura nova é acompanhada)
        Assert.True(tela.PodeSalvarAgora);
        Assert.Equal("Alterações não salvas", tela.EstadoFicha);
    }

    [Fact]
    public async Task Ficha_fechada_para_de_ser_acompanhada()
    {
        var (tela, _) = await PessoaGravadaAsync();
        var fechada = tela.Formulario!;

        await tela.FecharFichaCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, tela.EstadoFicha);
        Assert.False(tela.PodeSalvarAgora);

        var avisos = 0;
        tela.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(tela.EstadoFicha)) avisos++; };
        fechada.Nome = "Outro"; // a ficha que saiu da tela não mexe mais nela (sem inscrição presa)
        Assert.Equal(0, avisos);
    }

    [Theory]
    [InlineData(1920, false)]
    [InlineData(1008, false)] // no limite: fixo
    [InlineData(1007, true)]
    [InlineData(360, true)]   // largura mínima da janela
    [InlineData(0, false)]    // antes da primeira medida: não mexe
    public void Menu_lateral_recolhe_abaixo_de_1008(double largura, bool recolhido) =>
        Assert.Equal(recolhido, MenuLateral.Recolhido(largura));
}
