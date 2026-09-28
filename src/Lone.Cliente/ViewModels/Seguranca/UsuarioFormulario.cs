using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Contracts.Colaboradores;
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

    // ---- Pessoa no cadastro (Fase 2a, decisão F1): quem é o usuário em Pessoas; base de "Minha carteira" e "Minha equipe" ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPessoa), nameof(TextoPessoa))]
    [NotifyCanExecuteChangedFor(nameof(TirarPessoaCommand))]
    private Guid? _pessoaId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoPessoa))]
    private string _pessoaNome = string.Empty;

    public bool TemPessoa => PessoaId is not null;

    public string TextoPessoa => PessoaId is null
        ? "Nenhuma pessoa ligada: com alcance \"Minha carteira\" ou \"Minha equipe\", este usuário não verá nenhum cliente."
        : $"Ligado a: {PessoaNome}";

    /// <summary>Texto digitado para buscar a pessoa (a busca é feita pela tela, que chama a API).</summary>
    [ObservableProperty] private string _buscaPessoa = string.Empty;

    /// <summary>Resultado da busca. Escolher na lista não liga: é preciso confirmar em "Ligar" (evita trocar a lista dentro da seleção).</summary>
    public ObservableCollection<PessoaOpcaoDto> ResultadosPessoa { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LigarPessoaCommand))]
    private PessoaOpcaoDto? _pessoaEscolhida;

    /// <summary>Troca o resultado da busca (chamado pela tela depois da API).</summary>
    public void DefinirResultadosPessoa(IEnumerable<PessoaOpcaoDto> pessoas)
    {
        PessoaEscolhida = null;
        ResultadosPessoa.Clear();
        foreach (var p in pessoas) ResultadosPessoa.Add(p);
    }

    private bool PodeLigarPessoa() => PessoaEscolhida is not null;

    [RelayCommand(CanExecute = nameof(PodeLigarPessoa))]
    private void LigarPessoa()
    {
        if (PessoaEscolhida is not { } escolhida) return;
        PessoaId = escolhida.Id;
        PessoaNome = escolhida.Nome;
        BuscaPessoa = string.Empty;
        DefinirResultadosPessoa([]);
    }

    [RelayCommand(CanExecute = nameof(TemPessoa))]
    private void TirarPessoa()
    {
        PessoaId = null;
        PessoaNome = string.Empty;
    }

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
            PessoaId = dto.PessoaId,
            PessoaNome = dto.Pessoa ?? (dto.PessoaId is null ? string.Empty : "(pessoa não encontrada)"),
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
            PessoaId = PessoaId,
            Perfis = Perfis.Select(p => p.ParaDto()).ToList()
        },
        NovaSenha.Length == 0 ? null : NovaSenha);
}
