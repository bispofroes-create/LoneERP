using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.Api;
using Lone.Cliente.Mensagens;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels;

public enum TipoMensagem
{
    Informacao,
    Sucesso,
    Aviso,
    Erro
}

/// <summary>Base das telas: indicador de ocupado e mensagem ao usuário, com tradução dos erros da API.</summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Livre))]
    private bool _ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string _mensagem = string.Empty;

    [ObservableProperty] private TipoMensagem _tipoMensagem = TipoMensagem.Informacao;

    public bool Livre => !Ocupado;
    public bool TemMensagem => Mensagem.Length > 0;

    /// <summary>
    /// Toda mensagem mostrada na barra da tela, mesmo repetida (a mesma frase duas vezes não muda <see cref="Mensagem"/>): a
    /// tela usa para levar a vista até onde a mensagem aparece (anotação C3). Paliativo mantido até a fase da faixa de
    /// erro/aviso; o sucesso não passa mais por aqui (vai para a camada global, visível em qualquer ponto da rolagem).
    /// </summary>
    public event EventHandler? MensagemMostrada;

    /// <summary>
    /// Serviço global de mensagens (toast). O padrão é o do aplicativo; os testes trocam por um próprio, com relógio controlado.
    /// </summary>
    public ServicoMensagens Mensagens { get; set; } = ServicoMensagens.Padrao;

    /// <summary>
    /// Ponto único das mensagens das telas. O serviço global classifica: o que ele assume (Fase 1: sucesso) sai da barra da
    /// tela e aparece na camada global, fora da rolagem; o resto (erro, aviso, informação) continua na barra, como sempre.
    /// </summary>
    protected void Mostrar(string texto, TipoMensagem tipo) => Mostrar(texto, tipo, acao: null);

    /// <summary>
    /// Mesma coisa, com a única ação útil do toast (ex.: "Abrir") e o contexto que separa mensagens iguais de origens
    /// diferentes. Ação só vale para o que vira toast.
    /// </summary>
    protected void Mostrar(string texto, TipoMensagem tipo, AcaoMensagem? acao, string? contexto = null)
    {
        if (texto.Length > 0 && ServicoMensagens.Classificar(tipo) is not null)
        {
            // Antes o sucesso substituía o texto da barra; continua assim: um erro antigo não fica ao lado da confirmação.
            LimparMensagem();
            Mensagens.Publicar(texto, tipo, acao: acao, contexto: contexto);
            return;
        }
        if (acao is not null)
            throw new InvalidOperationException("Só as mensagens da camada global (toast) têm ação.");

        TipoMensagem = tipo;
        Mensagem = texto;
        if (texto.Length > 0) MensagemMostrada?.Invoke(this, EventArgs.Empty);
    }

    protected void LimparMensagem() => Mensagem = string.Empty;

    /// <summary>Regra, permissão, conflito e falha de rede viram mensagens claras; o resto mostra a causa.</summary>
    protected void MostrarErro(Exception erro)
    {
        switch (erro)
        {
            case ValidacaoException v:
                Mostrar(string.Join(Environment.NewLine, v.Erros), TipoMensagem.Erro);
                break;
            case ConflitoDeEdicaoException or AcessoNegadoException or LoginRecusadoException
                or ServidorIndisponivelException or SessaoExpiradaException or TrocaDeSenhaObrigatoriaException
                or ErroDaApiException:
                Mostrar(erro.Message, TipoMensagem.Erro);
                break;
            default:
                Mostrar($"Erro inesperado: {erro.GetBaseException().Message}", TipoMensagem.Erro);
                break;
        }
    }

    /// <summary>Executa uma ação com o indicador de ocupado e tratamento de erro. Falso se deu erro.</summary>
    protected async Task<bool> ExecutarAsync(Func<Task> acao)
    {
        if (Ocupado) return false;
        Ocupado = true;
        LimparMensagem();
        try
        {
            await acao();
            return true;
        }
        catch (Exception ex)
        {
            MostrarErro(ex);
            return false;
        }
        finally
        {
            Ocupado = false;
        }
    }
}
