using Lone.Aplicacao.Empresas;

namespace Lone.Aplicacao.Seguranca;

/// <summary>Empresa e estabelecimento em que o usuário está trabalhando.</summary>
public interface IEmpresaAtual
{
    /// <summary>Nulo enquanto nenhuma empresa foi escolhida (ou nenhuma está cadastrada).</summary>
    EmpresaAtiva? EmpresaAtiva { get; }
}

/// <summary>Sessão do usuário logado neste computador. Uma por execução do Lone.</summary>
public interface ISessao : IUsuarioAtual, IEmpresaAtual
{
    bool Autenticada { get; }
    string Login { get; }
    bool Administrador { get; }
    bool DeveTrocarSenha { get; }

    /// <summary>Estabelecimentos em que o usuário pode trabalhar (conforme os perfis por empresa).</summary>
    IReadOnlyList<EmpresaAtiva> EmpresasDisponiveis { get; }

    /// <summary>Disparado ao entrar, sair, trocar a senha ou trocar de empresa.</summary>
    event EventHandler? Alterada;

    /// <summary>Inicia a sessão. Se houver só uma empresa disponível, ela já fica ativa.</summary>
    void Iniciar(DadosSessao dados, IReadOnlyList<EmpresaAtiva> estabelecimentosDoGrupo);

    /// <summary>Atualiza a lista de empresas (ex.: depois de cadastrar uma empresa do grupo).</summary>
    void AtualizarEmpresas(IReadOnlyList<EmpresaAtiva> estabelecimentosDoGrupo);

    /// <summary>Troca a empresa ativa; as permissões passam a ser as dos perfis válidos nela.</summary>
    void SelecionarEmpresa(EmpresaAtiva empresa);

    void MarcarSenhaTrocada();
    void Encerrar();
}

/// <summary>Dados do usuário carregados no login.</summary>
public sealed record DadosSessao(
    int UsuarioId,
    string Nome,
    string Login,
    bool DeveTrocarSenha,
    IReadOnlyList<PerfilDaSessao> Perfis);

/// <summary>Um perfil do usuário, na empresa em que vale (nulo = todas).</summary>
public sealed record PerfilDaSessao(int? EmpresaId, bool Administrador, IReadOnlySet<string> Permissoes);
