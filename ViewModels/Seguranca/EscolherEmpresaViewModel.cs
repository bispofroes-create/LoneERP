using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Aplicacao.Empresas;
using Lone.Aplicacao.Seguranca;
using Lone.Servicos;
using Microsoft.UI.Xaml.Controls;

namespace Lone.ViewModels.Seguranca
{
    /// <summary>
    /// Escolha da empresa (estabelecimento) em que o usuário vai trabalhar.
    /// Aparece depois do login, quando há mais de uma disponível, e pelo "Trocar empresa" do menu.
    /// </summary>
    public partial class EscolherEmpresaViewModel : TelaViewModelBase
    {
        private readonly ISessao _sessao;
        private readonly IAutenticacaoService _autenticacao;
        private readonly INavegacao _navegacao;

        /// <summary>Guardado na abertura: diz se "Voltar" retorna ao sistema ou sai para o login.</summary>
        private readonly bool _jaTinhaEmpresa;

        public EscolherEmpresaViewModel(ISessao sessao, IAutenticacaoService autenticacao, INavegacao navegacao)
        {
            _sessao = sessao;
            _autenticacao = autenticacao;
            _navegacao = navegacao;
            _jaTinhaEmpresa = sessao.EmpresaAtiva is not null;
        }

        public ObservableCollection<EmpresaAtiva> Empresas { get; } = new();

        public string NomeUsuario => _sessao.Nome;

        public string TextoVoltar => _jaTinhaEmpresa ? "Voltar" : "Sair";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(EntrarCommand))]
        private EmpresaAtiva? _selecionada;

        [ObservableProperty] private bool _semEmpresas;

        /// <summary>Relê as empresas no banco (alguma pode ter sido cadastrada ou inativada) e marca a atual.</summary>
        public async Task CarregarAsync()
        {
            Ocupado = true;
            OcultarMensagem();
            try
            {
                var atual = _sessao.EmpresaAtiva?.EstabelecimentoId;
                await _autenticacao.AtualizarEmpresasAsync();

                Empresas.Clear();
                foreach (var empresa in _sessao.EmpresasDisponiveis)
                    Empresas.Add(empresa);

                Selecionada = Empresas.FirstOrDefault(e => e.EstabelecimentoId == atual)
                              ?? (Empresas.Count == 1 ? Empresas[0] : null);
                SemEmpresas = Empresas.Count == 0;

                if (SemEmpresas)
                    Mostrar("Nenhuma empresa disponível para o seu usuário. Cadastre a empresa em Pessoas com o papel "
                            + "\"Empresa do grupo\" ou peça ao administrador para liberar o acesso.", InfoBarSeverity.Warning);
            }
            catch (Exception ex)
            {
                MostrarErro("carregar as empresas", ex);
            }
            finally
            {
                Ocupado = false;
            }
        }

        private bool PodeEntrar() => Selecionada is not null;

        [RelayCommand(CanExecute = nameof(PodeEntrar))]
        private void Entrar()
        {
            if (Selecionada is null) return;
            try
            {
                _sessao.SelecionarEmpresa(Selecionada);
                _navegacao.IrParaSistema();
            }
            catch (Exception ex)
            {
                MostrarErro("selecionar a empresa", ex);
            }
        }

        /// <summary>Sem empresa cadastrada: entra assim mesmo (o administrador precisa cadastrá-la).</summary>
        [RelayCommand]
        private void ContinuarSemEmpresa() => _navegacao.IrParaSistema();

        [RelayCommand]
        private void Voltar()
        {
            if (_jaTinhaEmpresa && _sessao.EmpresaAtiva is not null)
            {
                _navegacao.IrParaSistema();
                return;
            }

            _autenticacao.Sair();
            _navegacao.IrParaLogin();
        }
    }
}
