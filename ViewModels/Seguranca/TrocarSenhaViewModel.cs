using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Aplicacao.Seguranca;
using Microsoft.UI.Xaml.Controls;

namespace Lone.ViewModels.Seguranca
{
    public partial class TrocarSenhaViewModel : TelaViewModelBase
    {
        private readonly IAutenticacaoService _autenticacao;

        public TrocarSenhaViewModel(IAutenticacaoService autenticacao)
        {
            _autenticacao = autenticacao;
        }

        [ObservableProperty] private string _senhaAtual = string.Empty;
        [ObservableProperty] private string _novaSenha = string.Empty;
        [ObservableProperty] private string _confirmacao = string.Empty;

        public string RegraSenha => $"Mínimo de {PoliticaSenha.TamanhoMinimo} caracteres, com letras e números.";

        /// <summary>Devolve true se a senha foi trocada (o diálogo pode fechar).</summary>
        public async Task<bool> TrocarAsync()
        {
            if (NovaSenha != Confirmacao)
            {
                Mostrar("A confirmação não confere com a nova senha.", InfoBarSeverity.Error);
                return false;
            }

            Ocupado = true;
            OcultarMensagem();
            try
            {
                await _autenticacao.TrocarSenhaAsync(SenhaAtual, NovaSenha);
                SenhaAtual = NovaSenha = Confirmacao = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                MostrarErro("trocar a senha", ex);
                return false;
            }
            finally
            {
                Ocupado = false;
            }
        }
    }
}
