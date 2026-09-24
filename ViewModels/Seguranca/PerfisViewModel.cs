using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Aplicacao.Seguranca;
using Microsoft.UI.Xaml.Controls;

namespace Lone.ViewModels.Seguranca
{
    public partial class PerfisViewModel : TelaViewModelBase
    {
        private readonly IPerfilAppService _perfis;
        private bool _ignorarSelecao;

        public PerfisViewModel(IPerfilAppService perfis)
        {
            _perfis = perfis;
        }

        public ObservableCollection<PerfilResumo> Itens { get; } = new();

        [ObservableProperty] private PerfilResumo? _selecionado;
        [ObservableProperty] private PerfilFormulario _formulario = PerfilFormulario.Novo();

        public async Task CarregarAsync(int? selecionarId = null)
        {
            try
            {
                var lista = await _perfis.ListarAsync();

                _ignorarSelecao = true;
                try
                {
                    Itens.Clear();
                    foreach (var p in lista) Itens.Add(p);
                    Selecionado = selecionarId is int id ? Itens.FirstOrDefault(p => p.Id == id) : null;
                }
                finally { _ignorarSelecao = false; }

                if (selecionarId is int abrir) await AbrirAsync(abrir);
            }
            catch (Exception ex)
            {
                MostrarErro("carregar os perfis", ex);
            }
        }

        partial void OnSelecionadoChanged(PerfilResumo? value)
        {
            if (value is null || _ignorarSelecao) return;
            OcultarMensagem();
            _ = AbrirAsync(value.Id);
        }

        private async Task AbrirAsync(int id)
        {
            try
            {
                var perfil = await _perfis.ObterAsync(id);
                Formulario = perfil is null ? PerfilFormulario.Novo() : PerfilFormulario.De(perfil);
            }
            catch (Exception ex)
            {
                MostrarErro("abrir o perfil", ex);
            }
        }

        [RelayCommand]
        private void Novo()
        {
            Selecionado = null;
            OcultarMensagem();
            Formulario = PerfilFormulario.Novo();
        }

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
            Ocupado = true;
            OcultarMensagem();
            try
            {
                var salvo = await _perfis.SalvarAsync(Formulario.ParaEntidade());
                await CarregarAsync(salvo.Id);
                Mostrar("Perfil salvo. Usuários já logados recebem as mudanças no próximo login.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                MostrarErro("salvar o perfil", ex);
            }
            finally
            {
                Ocupado = false;
            }
        }
    }
}
