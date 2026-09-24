using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Aplicacao.Seguranca;
using Lone.Servicos;

namespace Lone.ViewModels
{
    /// <summary>
    /// Menu principal: o que o usuário logado pode ver, a empresa ativa, trocar de empresa e sair.
    /// As permissões são lidas ao abrir o sistema; trocar de empresa recria o menu.
    /// </summary>
    public partial class ShellViewModel : ObservableObject
    {
        private readonly ISessao _sessao;
        private readonly IAutenticacaoService _autenticacao;
        private readonly INavegacao _navegacao;

        public ShellViewModel(ISessao sessao, IAutorizacao autorizacao, IAutenticacaoService autenticacao, INavegacao navegacao)
        {
            _sessao = sessao;
            _autenticacao = autenticacao;
            _navegacao = navegacao;

            PodeVerPessoas = autorizacao.Possui(Permissoes.Pessoas.Visualizar);
            PodeGerenciarUsuarios = autorizacao.Possui(Permissoes.Seguranca.GerenciarUsuarios);
            PodeGerenciarPerfis = autorizacao.Possui(Permissoes.Seguranca.GerenciarPerfis);

            _sessao.Alterada += Sessao_Alterada;
        }

        public string NomeUsuario => _sessao.Nome;
        public string LoginUsuario => _sessao.Login;
        public bool DeveTrocarSenha => _sessao.DeveTrocarSenha;

        public string EmpresaAtiva => _sessao.EmpresaAtiva?.Nome ?? "Nenhuma empresa selecionada";
        public string EmpresaAtivaDetalhe => _sessao.EmpresaAtiva is { } e
            ? (e.EhMatriz ? $"{e.CnpjFormatado} · matriz" : $"{e.CnpjFormatado} · filial")
            : string.Empty;
        public bool PodeTrocarEmpresa => _sessao.EmpresasDisponiveis.Count > 1;

        public bool PodeVerPessoas { get; }
        public bool PodeGerenciarUsuarios { get; }
        public bool PodeGerenciarPerfis { get; }
        public bool PodeVerSeguranca => PodeGerenciarUsuarios || PodeGerenciarPerfis;

        /// <summary>A sessão é única no aplicativo; o menu deixa de ouvi-la quando a tela fecha.</summary>
        public void Desligar() => _sessao.Alterada -= Sessao_Alterada;

        // Ex.: a primeira empresa do grupo foi cadastrada e ficou ativa sozinha.
        private void Sessao_Alterada(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(EmpresaAtiva));
            OnPropertyChanged(nameof(EmpresaAtivaDetalhe));
            OnPropertyChanged(nameof(PodeTrocarEmpresa));
        }

        [RelayCommand]
        private void TrocarEmpresa() => _navegacao.IrParaEscolherEmpresa();

        [RelayCommand]
        private void Sair()
        {
            _autenticacao.Sair();
            _navegacao.IrParaLogin();
        }
    }
}
