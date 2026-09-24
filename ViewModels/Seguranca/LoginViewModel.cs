using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Aplicacao.Seguranca;
using Lone.Servicos;
using Microsoft.UI.Xaml.Controls;

namespace Lone.ViewModels.Seguranca
{
    public partial class LoginViewModel : TelaViewModelBase
    {
        private readonly IAutenticacaoService _autenticacao;
        private readonly INavegacao _navegacao;

        public LoginViewModel(IAutenticacaoService autenticacao, INavegacao navegacao)
        {
            _autenticacao = autenticacao;
            _navegacao = navegacao;
        }

        [ObservableProperty] private string _login = string.Empty;
        [ObservableProperty] private string _senha = string.Empty;

        [RelayCommand]
        private async Task EntrarAsync()
        {
            if (Ocupado) return;
            if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrEmpty(Senha))
            {
                Mostrar("Informe login e senha.", InfoBarSeverity.Warning);
                return;
            }

            Ocupado = true;
            OcultarMensagem();
            try
            {
                var resultado = await _autenticacao.EntrarAsync(Login, Senha);
                if (resultado.Sucesso)
                {
                    Senha = string.Empty;
                    _navegacao.EntrarNoSistema();
                }
                else
                {
                    Senha = string.Empty;
                    Mostrar(resultado.Mensagem, InfoBarSeverity.Error);
                }
            }
            catch (Exception ex)
            {
                MostrarErro("entrar", ex);
            }
            finally
            {
                Ocupado = false;
            }
        }
    }
}
