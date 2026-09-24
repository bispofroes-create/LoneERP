using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Aplicacao.Seguranca;
using Lone.Core.Entidades;

namespace Lone.ViewModels.Seguranca
{
    /// <summary>Permissões de um módulo, agrupadas na tela.</summary>
    public sealed class GrupoPermissoes
    {
        public string Modulo { get; init; } = string.Empty;
        public ObservableCollection<OpcaoMarcavel> Itens { get; } = new();
    }

    public partial class PerfilFormulario : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Titulo), nameof(EhNovo))]
        private int _id;

        [ObservableProperty] private string _nome = string.Empty;
        [ObservableProperty] private string _descricao = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PermissoesVisiveis))]
        private bool _administrador;

        [ObservableProperty] private bool _ativo = true;

        public byte[]? Versao { get; private set; }
        public ObservableCollection<GrupoPermissoes> Grupos { get; } = new();

        public bool EhNovo => Id == 0;
        public string Titulo => EhNovo ? "Novo perfil" : "Editar perfil";
        public bool PermissoesVisiveis => !Administrador;

        public static PerfilFormulario Novo() => Montar(new PerfilFormulario(), new HashSet<string>());

        public static PerfilFormulario De(Perfil p) => Montar(new PerfilFormulario
        {
            Id = p.Id,
            Versao = p.Versao,
            Nome = p.Nome,
            Descricao = p.Descricao ?? string.Empty,
            Administrador = p.Administrador,
            Ativo = p.Ativo
        }, p.Permissoes.Select(x => x.Codigo).ToHashSet());

        private static PerfilFormulario Montar(PerfilFormulario f, HashSet<string> marcadas)
        {
            foreach (var modulo in Permissoes.Todas.GroupBy(p => p.Modulo))
            {
                var grupo = new GrupoPermissoes { Modulo = modulo.Key };
                foreach (var p in modulo)
                    grupo.Itens.Add(new OpcaoMarcavel
                    {
                        Codigo = p.Codigo,
                        Texto = p.Descricao,
                        Detalhe = p.Codigo,
                        Marcado = marcadas.Contains(p.Codigo)
                    });
                f.Grupos.Add(grupo);
            }
            return f;
        }

        public Perfil ParaEntidade() => new()
        {
            Id = Id,
            Versao = Versao,
            Nome = Nome,
            Descricao = Descricao,
            Administrador = Administrador,
            Ativo = Ativo,
            Permissoes = Grupos.SelectMany(g => g.Itens).Where(i => i.EstaMarcado)
                .Select(i => new PerfilPermissao { PerfilId = Id, Codigo = i.Codigo }).ToList()
        };
    }
}
