using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.Api;
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

    protected void Mostrar(string texto, TipoMensagem tipo)
    {
        TipoMensagem = tipo;
        Mensagem = texto;
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
