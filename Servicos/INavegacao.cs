using System;
using System.Threading.Tasks;
using Lone.Aplicacao.Seguranca;
using Lone.Views;
using Lone.Views.Seguranca;
using Microsoft.UI.Xaml.Controls;

namespace Lone.Servicos
{
    /// <summary>Troca a tela principal da janela (primeiro acesso, login, escolha de empresa, sistema). Usada pelos ViewModels.</summary>
    public interface INavegacao
    {
        void Registrar(Frame principal);

        /// <summary>Primeiro acesso se o banco não tem usuários; senão, login.</summary>
        Task IniciarAsync();

        void IrParaLogin();

        /// <summary>
        /// Depois de entrar: se o usuário tem várias empresas e nenhuma foi escolhida, vai para a escolha;
        /// senão, direto para o sistema.
        /// </summary>
        void EntrarNoSistema();

        void IrParaEscolherEmpresa();
        void IrParaSistema();
    }

    public sealed class Navegacao : INavegacao
    {
        private readonly IAutenticacaoService _autenticacao;
        private readonly ISessao _sessao;
        private Frame? _principal;

        public Navegacao(IAutenticacaoService autenticacao, ISessao sessao)
        {
            _autenticacao = autenticacao;
            _sessao = sessao;
        }

        public void Registrar(Frame principal) => _principal = principal;

        public async Task IniciarAsync()
        {
            if (await _autenticacao.ExisteUsuarioAsync())
                IrParaLogin();
            else
                Ir(typeof(PrimeiroAcessoPage));
        }

        public void IrParaLogin() => Ir(typeof(LoginPage));

        public void EntrarNoSistema()
        {
            if (_sessao.EmpresaAtiva is null && _sessao.EmpresasDisponiveis.Count > 1)
                IrParaEscolherEmpresa();
            else
                IrParaSistema();
        }

        public void IrParaEscolherEmpresa() => Ir(typeof(EscolherEmpresaPage));

        public void IrParaSistema() => Ir(typeof(ShellPage));

        private void Ir(Type pagina)
        {
            if (_principal is null)
                throw new InvalidOperationException("A navegação principal não foi registrada.");

            _principal.Navigate(pagina);
            _principal.BackStack.Clear(); // não existe "voltar" entre login e sistema
        }
    }
}
