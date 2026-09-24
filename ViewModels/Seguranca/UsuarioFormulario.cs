using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Aplicacao.Empresas;
using Lone.Aplicacao.Seguranca;
using Lone.Core.Entidades;

namespace Lone.ViewModels.Seguranca
{
    public partial class UsuarioFormulario : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Titulo), nameof(EhNovo), nameof(RotuloSenha))]
        private int _id;

        [ObservableProperty] private string _nome = string.Empty;
        [ObservableProperty] private string _login = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private bool _ativo = true;
        [ObservableProperty] private bool _deveTrocarSenha = true;
        [ObservableProperty] private string _novaSenha = string.Empty;

        public byte[]? Versao { get; private set; }
        public bool Bloqueado { get; private set; }
        public string TextoAcesso { get; private set; } = string.Empty;

        /// <summary>Perfis do usuário, cada um valendo em todas as empresas ou numa empresa do grupo.</summary>
        public ObservableCollection<AtribuicaoPerfil> Perfis { get; } = new();

        public IReadOnlyList<OpcaoLista> OpcoesPerfil { get; private set; } = [];
        public IReadOnlyList<OpcaoLista> OpcoesEmpresa { get; private set; } = [];

        /// <summary>Linhas em que o perfil ainda não foi escolhido.</summary>
        public bool TemLinhaIncompleta => Perfis.Any(p => !p.Completa);

        public bool EhNovo => Id == 0;
        public string Titulo => EhNovo ? "Novo usuário" : "Editar usuário";
        public string RotuloSenha => EhNovo ? "Senha inicial *" : "Redefinir senha (deixe em branco para manter)";

        public static UsuarioFormulario Novo(IEnumerable<PerfilResumo> perfis, IEnumerable<EmpresaResumo> empresas)
        {
            var f = new UsuarioFormulario();
            f.PrepararOpcoes(perfis, empresas, []);
            f.AdicionarPerfil();
            return f;
        }

        public static UsuarioFormulario De(Usuario u, IEnumerable<PerfilResumo> perfis, IEnumerable<EmpresaResumo> empresas)
        {
            var agora = DateTime.Now;
            var f = new UsuarioFormulario
            {
                Id = u.Id,
                Versao = u.Versao,
                Nome = u.Nome,
                Login = u.Login,
                Email = u.Email ?? string.Empty,
                Ativo = u.Ativo,
                DeveTrocarSenha = u.DeveTrocarSenha,
                Bloqueado = u.Acesso?.EstaBloqueado(agora) == true,
                TextoAcesso = u.Acesso switch
                {
                    { } a when a.EstaBloqueado(agora) => $"Bloqueado até {a.BloqueadoAte:dd/MM/yyyy HH:mm} por tentativas erradas.",
                    { UltimoAcessoEm: { } ultimo } => $"Último acesso em {ultimo:dd/MM/yyyy HH:mm}.",
                    _ => "Ainda não acessou o sistema."
                }
            };
            f.PrepararOpcoes(perfis, empresas, u.Perfis);
            foreach (var p in u.Perfis)
                f.Perfis.Add(AtribuicaoPerfil.De(p, f.OpcoesPerfil, f.OpcoesEmpresa));
            return f;
        }

        public void AdicionarPerfil() => Perfis.Add(new AtribuicaoPerfil(OpcoesPerfil, OpcoesEmpresa));

        public void RemoverPerfil(AtribuicaoPerfil linha) => Perfis.Remove(linha);

        /// <summary>
        /// Perfis e empresas inativos só aparecem se o usuário já os tiver (para não sumirem da tela sem aviso).
        /// A primeira empresa é "Todas as empresas".
        /// </summary>
        private void PrepararOpcoes(IEnumerable<PerfilResumo> perfis, IEnumerable<EmpresaResumo> empresas, IEnumerable<UsuarioPerfil> atuais)
        {
            var perfisAtuais = atuais.Select(p => p.PerfilId).ToHashSet();
            var empresasAtuais = atuais.Where(p => p.EmpresaId is not null).Select(p => p.EmpresaId!.Value).ToHashSet();

            OpcoesPerfil = perfis
                .Where(p => p.Ativo || perfisAtuais.Contains(p.Id))
                .Select(p => new OpcaoLista(p.Id, p.Nome + (p.Administrador ? " (acesso total)" : p.Ativo ? string.Empty : " (inativo)")))
                .ToList();

            var opcoesEmpresa = new List<OpcaoLista> { new(null, "Todas as empresas") };
            opcoesEmpresa.AddRange(empresas
                .Where(e => e.Ativa || empresasAtuais.Contains(e.Id))
                .Select(e => new OpcaoLista(e.Id, e.Ativa ? e.Nome : e.Nome + " (inativa)")));
            OpcoesEmpresa = opcoesEmpresa;
        }

        public Usuario ParaEntidade() => new()
        {
            Id = Id,
            Versao = Versao,
            Nome = Nome,
            Login = Login,
            Email = Email,
            Ativo = Ativo,
            DeveTrocarSenha = DeveTrocarSenha,
            Perfis = Perfis
                .Where(p => p.Completa)
                .Select(p => new UsuarioPerfil { UsuarioId = Id, PerfilId = p.PerfilId!.Value, EmpresaId = p.EmpresaId })
                .ToList()
        };
    }
}
