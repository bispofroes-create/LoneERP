using Lone.Cliente.ViewModels;

namespace Lone.Tests.Cliente;

/// <summary>
/// Aviso de mensagem mostrada (revisão da tela de operações territoriais, 30/09): a tela leva a vista até a mensagem a
/// cada aviso — inclusive quando a mesma frase aparece duas vezes seguidas, o que não muda a propriedade Mensagem.
/// Vale para a barra (aviso, erro, informação); o sucesso foi para a camada global (MensagensTests).
/// </summary>
public class ViewModelBaseTests
{
    private sealed class Tela : ViewModelBase
    {
        public void Avisar(string texto) => Mostrar(texto, TipoMensagem.Aviso);
        public void Limpar() => LimparMensagem();
    }

    [Fact]
    public void Mensagem_repetida_avisa_de_novo_e_limpar_nao_avisa()
    {
        var tela = new Tela();
        var avisos = 0;
        tela.MensagemMostrada += (_, _) => avisos++;

        tela.Avisar("Mudança incluída.");
        tela.Avisar("Mudança incluída.");
        tela.Limpar();

        Assert.Equal(2, avisos);
        Assert.Equal(string.Empty, tela.Mensagem);
    }
}
