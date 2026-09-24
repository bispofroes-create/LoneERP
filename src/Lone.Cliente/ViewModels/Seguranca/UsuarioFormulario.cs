using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;

namespace Lone.Cliente.ViewModels.Seguranca;

/// <summary>Ficha de um usuário em edição: os campos da tela e a conversão de/para o DTO da API.</summary>
public sealed partial class UsuarioFormulario : ObservableObject
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    private UsuarioFormulario(Guid id, byte[]? versao, bool novo)
    {
        Id = id;
        Versao = versao;
        Novo = novo;
    }

    public Guid Id { get; }
    public byte[]? Versao { get; }
    public bool Novo { get; }

    public string Titulo => Novo ? "Novo usuário" : Nome;
    public bool Existente => !Novo;

    /// <summary>Na inclusão a senha inicial é obrigatória; na alteração, só para redefinir.</summary>
    public string RotuloSenha => Novo ? "Senha inicial" : "Redefinir senha (deixe em branco para manter)";

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string _login = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private bool _ativo = true;
    [ObservableProperty] private bool _deveTrocarSenha = true;
    [ObservableProperty] private string _novaSenha = string.Empty;
    [ObservableProperty] private string _confirmacao = string.Empty;

    public ObservableCollection<PerfilAtribuido> Perfis { get; } = new();

    public DateTime? BloqueadoAte { get; private init; }
    public DateTime? UltimoAcessoEm { get; private init; }

    public bool Bloqueado => BloqueadoAte > DateTime.UtcNow;

    public string SituacaoAcesso
    {
        get
        {
            var ultimo = UltimoAcessoEm is { } u ? $"Último acesso em {u.ToLocalTime().ToString("g", Brasil)}." : "Nunca acessou.";
            return Bloqueado
                ? $"Bloqueado por tentativas erradas até {BloqueadoAte!.Value.ToLocalTime().ToString("t", Brasil)}. {ultimo}"
                : ultimo;
        }
    }

    public static UsuarioFormulario NovoUsuario() => new(IdSequencial.Novo(), null, novo: true);

    public static UsuarioFormulario De(UsuarioDto dto, IEnumerable<OpcaoPerfil> perfis, IEnumerable<OpcaoEmpresa> empresas)
    {
        var formulario = new UsuarioFormulario(dto.Id, dto.Versao, novo: false)
        {
            Nome = dto.Nome,
            Login = dto.Login,
            Email = dto.Email ?? string.Empty,
            Ativo = dto.Ativo,
            DeveTrocarSenha = dto.DeveTrocarSenha,
            BloqueadoAte = dto.BloqueadoAte,
            UltimoAcessoEm = dto.UltimoAcessoEm
        };

        var porPerfil = perfis.ToDictionary(p => p.Id);
        var porEmpresa = empresas.Where(e => e.Id is not null).ToDictionary(e => e.Id!.Value);
        foreach (var p in dto.Perfis)
        {
            var perfil = porPerfil.GetValueOrDefault(p.PerfilId) ?? new OpcaoPerfil(p.PerfilId, "(perfil removido)");
            var empresa = p.EmpresaId is { } e
                ? porEmpresa.GetValueOrDefault(e) ?? new OpcaoEmpresa(e, "(empresa removida)")
                : OpcaoEmpresa.Todas;
            formulario.Perfis.Add(new PerfilAtribuido(perfil, empresa));
        }

        return formulario;
    }

    /// <summary>Erros que dá para apontar sem ir ao servidor (o resto a API valida).</summary>
    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (Novo && NovaSenha.Length == 0) erros.Add("Informe a senha inicial do usuário.");
        if (NovaSenha != Confirmacao) erros.Add("A confirmação não confere com a senha.");
        if (Perfis.Count == 0) erros.Add("Escolha ao menos um perfil.");
        return erros;
    }

    public SalvarUsuarioRequisicao ParaRequisicao() => new(
        new UsuarioDto
        {
            Id = Id,
            Versao = Versao,
            Nome = Nome,
            Login = Login,
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email,
            Ativo = Ativo,
            DeveTrocarSenha = DeveTrocarSenha,
            Perfis = Perfis.Select(p => p.ParaDto()).ToList()
        },
        NovaSenha.Length == 0 ? null : NovaSenha);
}
