using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Aplicacao.Seguranca;
using Lone.Servicos;
using Microsoft.UI.Xaml.Controls;

namespace Lone.ViewModels.Seguranca
{
    /// <summary>Criação do primeiro administrador, quando o banco ainda não tem usuários.</summary>
    public partial class PrimeiroAcessoViewModel : TelaViewModelBase
    {
        private readonly IAutenticacaoService _autenticacao;
        private readonly INavegacao _navegacao;

        public PrimeiroAcessoViewModel(IAutenticacaoService autenticacao, INavegacao navegacao)
        {
            _autenticacao = autenticacao;
            _navegacao = navegacao;
        }

        [ObservableProperty] private string _nome = string.Empty;
        [ObservableProperty] private string _login = string.Empty;
        [ObservableProperty] private string _senha = string.Empty;
        [ObservableProperty] private string _confirmacao = string.Empty;

        public string RegraSenha => $"Mínimo de {PoliticaSenha.TamanhoMinimo} caracteres, com letras e números.";

        [RelayCommand]
        private async Task CriarAsync()
        {
            if (Ocupado) return;
            if (Senha != Confirmacao)
            {
                Mostrar("A confirmação não confere com a senha.", InfoBarSeverity.Error);
                return;
            }

            Ocupado = true;
            OcultarMensagem();
            try
            {
                await _autenticacao.CriarPrimeiroAdministradorAsync(Nome, Login, Senha);
                Senha = Confirmacao = string.Empty;
                _navegacao.EntrarNoSistema();
            }
            catch (Exception ex)
            {
                MostrarErro("criar o administrador", ex);
            }
            finally
            {
                Ocupado = false;
            }
        }
    }
}
