using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Aplicacao.Comum;
using Lone.Aplicacao.Seguranca;
using Lone.Core.Validacao;
using Microsoft.UI.Xaml.Controls;

namespace Lone.ViewModels
{
    /// <summary>Base das telas: barra de mensagens, indicador de ocupado e tradução de erros para o usuário.</summary>
    public abstract partial class TelaViewModelBase : ObservableObject
    {
        [ObservableProperty] private bool _ocupado;
        [ObservableProperty] private bool _mensagemAberta;
        [ObservableProperty] private string _mensagem = string.Empty;
        [ObservableProperty] private InfoBarSeverity _mensagemSeveridade = InfoBarSeverity.Informational;

        protected void Mostrar(string texto, InfoBarSeverity severidade)
        {
            Mensagem = texto;
            MensagemSeveridade = severidade;
            MensagemAberta = true;
        }

        protected void OcultarMensagem() => MensagemAberta = false;

        /// <summary>Regra, permissão e conflito viram mensagens claras; o resto mostra a causa técnica.</summary>
        protected void MostrarErro(string acao, Exception ex)
        {
            switch (ex)
            {
                case ValidacaoException v:
                    Mostrar(string.Join("\n", v.Erros), InfoBarSeverity.Error);
                    break;
                case ConflitoDeEdicaoException:
                    Mostrar(ex.Message, InfoBarSeverity.Warning);
                    break;
                case AcessoNegadoException:
                    Mostrar(ex.Message, InfoBarSeverity.Error);
                    break;
                default:
                    Mostrar($"Erro ao {acao}: {ex.GetBaseException().Message}", InfoBarSeverity.Error);
                    break;
            }
        }
    }
}
