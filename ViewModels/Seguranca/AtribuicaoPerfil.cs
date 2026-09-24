using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Core.Entidades;

namespace Lone.ViewModels.Seguranca
{
    /// <summary>Uma linha de "perfil em empresa" do usuário (ex.: Vendedor na Filial 2; Administrador em todas).</summary>
    public partial class AtribuicaoPerfil : ObservableObject
    {
        public AtribuicaoPerfil(IReadOnlyList<OpcaoLista> perfis, IReadOnlyList<OpcaoLista> empresas)
        {
            Perfis = perfis;
            Empresas = empresas;
        }

        /// <summary>Opções compartilhadas por todas as linhas do formulário.</summary>
        public IReadOnlyList<OpcaoLista> Perfis { get; }

        /// <summary>A primeira opção é sempre "Todas as empresas" (Id nulo).</summary>
        public IReadOnlyList<OpcaoLista> Empresas { get; }

        /// <summary>-1 = perfil ainda não escolhido.</summary>
        [ObservableProperty] private int _perfilIndice = -1;

        [ObservableProperty] private int _empresaIndice;

        public bool Completa => PerfilIndice >= 0 && PerfilIndice < Perfis.Count;

        public int? PerfilId => Completa ? Perfis[PerfilIndice].Id : null;

        public int? EmpresaId => EmpresaIndice > 0 && EmpresaIndice < Empresas.Count ? Empresas[EmpresaIndice].Id : null;

        public static AtribuicaoPerfil De(UsuarioPerfil p, IReadOnlyList<OpcaoLista> perfis, IReadOnlyList<OpcaoLista> empresas)
        {
            var linha = new AtribuicaoPerfil(perfis, empresas);
            linha.PerfilIndice = Indice(perfis, p.PerfilId);
            linha.EmpresaIndice = p.EmpresaId is null ? 0 : Indice(empresas, p.EmpresaId);
            return linha;
        }

        private static int Indice(IReadOnlyList<OpcaoLista> opcoes, int? id)
        {
            for (var i = 0; i < opcoes.Count; i++)
                if (opcoes[i].Id == id) return i;
            return -1;
        }
    }
}
