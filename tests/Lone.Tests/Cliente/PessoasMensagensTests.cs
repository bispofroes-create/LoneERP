using System.Net;
using Lone.Cliente.Mensagens;
using Lone.Cliente.ViewModels;
using Lone.Contracts.Pessoas;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Cliente;

/// <summary>
/// Piloto da Fase 1 da camada global de mensagens em Pessoas: a confirmação da gravação vai para o toast (visível em qualquer
/// ponto da rolagem da ficha), sem ocupar a barra da ficha e sem levar a tela ao topo; aviso (duplicidade) continua na barra.
/// </summary>
public class PessoasMensagensTests
{
    [Fact]
    public async Task Pessoa_nova_gravada_confirma_no_toast_sem_barra_e_sem_voltar_ao_topo()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        tela.Mensagens = new ServicoMensagens(new FakeTimeProvider());
        var levadasAoTopo = 0;
        tela.MensagemMostrada += (_, _) => levadasAoTopo++;

        await tela.NovoCommand.ExecuteAsync(null);
        var f = tela.Formulario!;
        f.Nome = "Carlos";
        f.Enderecos[0].Municipio.Definir(3550308, "São Paulo", "SP");
        var gravada = f.ParaDto();
        gravada.Codigo = 7;
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, new ResultadoSalvarPessoa { Pessoa = gravada, Avisos = [] })
            .Responder(HttpStatusCode.OK, new PaginaListaPessoas());

        await tela.SalvarCommand.ExecuteAsync(null);

        var toast = Assert.Single(tela.Mensagens.Visiveis);
        Assert.Equal("Pessoa criada: Carlos", toast.Texto); // com o nome do cabeçalho da ficha
        Assert.Equal($"pessoa:{f.Id}", toast.Contexto);      // outra pessoa salva em seguida não se junta a esta
        Assert.Equal(TipoMensagem.Sucesso, toast.Tipo);
        Assert.Null(toast.Acao);                       // a ficha já está aberta: não há próxima ação a oferecer
        Assert.Equal(TimeSpan.FromSeconds(4), toast.Duracao);
        Assert.False(tela.TemMensagem);                // nada na barra do topo da ficha
        Assert.Equal(0, levadasAoTopo);                // quem estava no fim do formulário continua lá
        Assert.False(tela.Formulario!.Nova);
    }
}
