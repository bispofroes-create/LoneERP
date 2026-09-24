using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Aplicacao.Empresas;
using Lone.Aplicacao.Seguranca;
using Microsoft.UI.Xaml.Controls;

namespace Lone.ViewModels.Seguranca
{
    public partial class UsuariosViewModel : TelaViewModelBase
    {
        private readonly IUsuarioAppService _usuarios;
        private List<PerfilResumo> _perfisDisponiveis = new();
        private List<EmpresaResumo> _empresasDoGrupo = new();
        private bool _ignorarSelecao;

        public UsuariosViewModel(IUsuarioAppService usuarios)
        {
            _usuarios = usuarios;
        }

        public ObservableCollection<UsuarioResumo> Itens { get; } = new();

        [ObservableProperty] private UsuarioResumo? _selecionado;
        [ObservableProperty] private UsuarioFormulario _formulario = new();

        public async Task CarregarAsync(int? selecionarId = null)
        {
            try
            {
                _perfisDisponiveis = await _usuarios.ListarPerfisAsync();
                _empresasDoGrupo = await _usuarios.ListarEmpresasAsync();
                var lista = await _usuarios.ListarAsync();

                _ignorarSelecao = true;
                try
                {
                    Itens.Clear();
                    foreach (var u in lista) Itens.Add(u);
                    Selecionado = selecionarId is int id ? Itens.FirstOrDefault(u => u.Id == id) : null;
                }
                finally { _ignorarSelecao = false; }

                if (selecionarId is int abrir) await AbrirAsync(abrir);
                else Formulario = UsuarioFormulario.Novo(_perfisDisponiveis, _empresasDoGrupo);
            }
            catch (Exception ex)
            {
                MostrarErro("carregar os usuários", ex);
            }
        }

        partial void OnSelecionadoChanged(UsuarioResumo? value)
        {
            if (value is null || _ignorarSelecao) return;
            OcultarMensagem();
            _ = AbrirAsync(value.Id);
        }

        private async Task AbrirAsync(int id)
        {
            try
            {
                var usuario = await _usuarios.ObterAsync(id);
                Formulario = usuario is null
                    ? UsuarioFormulario.Novo(_perfisDisponiveis, _empresasDoGrupo)
                    : UsuarioFormulario.De(usuario, _perfisDisponiveis, _empresasDoGrupo);
            }
            catch (Exception ex)
            {
                MostrarErro("abrir o usuário", ex);
            }
        }

        [RelayCommand]
        private void Novo()
        {
            Selecionado = null;
            OcultarMensagem();
            Formulario = UsuarioFormulario.Novo(_perfisDisponiveis, _empresasDoGrupo);
        }

        [RelayCommand]
        private void AdicionarPerfil() => Formulario.AdicionarPerfil();

        public void RemoverPerfil(AtribuicaoPerfil linha) => Formulario.RemoverPerfil(linha);

        [RelayCommand]
        private async Task DescartarAsync()
        {
            OcultarMensagem();
            if (Formulario.EhNovo) Novo();
            else await AbrirAsync(Formulario.Id);
        }

        [RelayCommand]
        private async Task SalvarAsync()
        {
            if (Formulario.TemLinhaIncompleta)
            {
                Mostrar("Escolha o perfil em todas as linhas (ou remova as linhas vazias).", InfoBarSeverity.Warning);
                return;
            }

            Ocupado = true;
            OcultarMensagem();
            try
            {
                var novaSenha = string.IsNullOrEmpty(Formulario.NovaSenha) ? null : Formulario.NovaSenha;
                var salvo = await _usuarios.SalvarAsync(Formulario.ParaEntidade(), novaSenha);
                await CarregarAsync(salvo.Id);
                Mostrar(novaSenha is null
                        ? "Usuário salvo."
                        : "Usuário salvo. Ele deverá trocar a senha no próximo acesso.",
                    InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                MostrarErro("salvar o usuário", ex);
            }
            finally
            {
                Ocupado = false;
            }
        }

        [RelayCommand]
        private async Task DesbloquearAsync()
        {
            if (Formulario.EhNovo) return;
            try
            {
                await _usuarios.DesbloquearAsync(Formulario.Id);
                await CarregarAsync(Formulario.Id);
                Mostrar("Acesso desbloqueado.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                MostrarErro("desbloquear", ex);
            }
        }
    }
}
