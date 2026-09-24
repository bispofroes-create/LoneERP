using Lone.Aplicacao.Empresas;

namespace Lone.Aplicacao.Seguranca;

/// <summary>
/// Guarda o usuário logado, a empresa ativa e responde às verificações de permissão.
/// Vale o perfil atribuído "em todas as empresas" e os atribuídos à empresa ativa.
/// Mudanças de perfil feitas por um administrador valem no próximo login.
/// </summary>
public sealed class SessaoUsuario : ISessao, IAutorizacao
{
    private DadosSessao? _dados;
    private IReadOnlyList<EmpresaAtiva> _empresas = [];
    private IReadOnlyList<PerfilDaSessao> _perfisValidos = [];

    public event EventHandler? Alterada;

    public bool Autenticada => _dados is not null;
    public int? Id => _dados?.UsuarioId;
    public string Nome => _dados?.Nome ?? "sistema";
    public string Login => _dados?.Login ?? string.Empty;
    public bool DeveTrocarSenha => _dados?.DeveTrocarSenha ?? false;
    public bool Administrador => _perfisValidos.Any(p => p.Administrador);

    public EmpresaAtiva? EmpresaAtiva { get; private set; }
    public IReadOnlyList<EmpresaAtiva> EmpresasDisponiveis => _empresas;

    public void Iniciar(DadosSessao dados, IReadOnlyList<EmpresaAtiva> estabelecimentosDoGrupo)
    {
        _dados = dados;
        EmpresaAtiva = null;
        DefinirEmpresas(estabelecimentosDoGrupo);
        if (_empresas.Count == 1)
            EmpresaAtiva = _empresas[0];
        RecalcularPerfis();
        Alterada?.Invoke(this, EventArgs.Empty);
    }

    public void AtualizarEmpresas(IReadOnlyList<EmpresaAtiva> estabelecimentosDoGrupo)
    {
        if (_dados is null) return;
        DefinirEmpresas(estabelecimentosDoGrupo);
        if (EmpresaAtiva is not null && _empresas.All(e => e.EstabelecimentoId != EmpresaAtiva.EstabelecimentoId))
            EmpresaAtiva = null;
        if (EmpresaAtiva is null && _empresas.Count == 1)
            EmpresaAtiva = _empresas[0];
        RecalcularPerfis();
        Alterada?.Invoke(this, EventArgs.Empty);
    }

    public void SelecionarEmpresa(EmpresaAtiva empresa)
    {
        if (_empresas.All(e => e.EstabelecimentoId != empresa.EstabelecimentoId))
            throw new AcessoNegadoException(Permissoes.Pessoas.Visualizar);

        EmpresaAtiva = empresa;
        RecalcularPerfis();
        Alterada?.Invoke(this, EventArgs.Empty);
    }

    public void MarcarSenhaTrocada()
    {
        if (_dados is null) return;
        _dados = _dados with { DeveTrocarSenha = false };
        Alterada?.Invoke(this, EventArgs.Empty);
    }

    public void Encerrar()
    {
        _dados = null;
        _empresas = [];
        _perfisValidos = [];
        EmpresaAtiva = null;
        Alterada?.Invoke(this, EventArgs.Empty);
    }

    public bool Possui(string permissao) =>
        _perfisValidos.Any(p => p.Administrador || p.Permissoes.Contains(permissao));

    public void Exigir(string permissao)
    {
        if (!Possui(permissao))
            throw new AcessoNegadoException(permissao);
    }

    /// <summary>O usuário vê as empresas em que tem algum perfil (perfil "em todas" = todas).</summary>
    private void DefinirEmpresas(IReadOnlyList<EmpresaAtiva> estabelecimentos)
    {
        var perfis = _dados?.Perfis ?? [];
        _empresas = perfis.Any(p => p.EmpresaId is null)
            ? estabelecimentos
            : estabelecimentos.Where(e => perfis.Any(p => p.EmpresaId == e.EmpresaId)).ToList();
    }

    private void RecalcularPerfis()
    {
        var perfis = _dados?.Perfis ?? [];
        var empresa = EmpresaAtiva?.EmpresaId;
        _perfisValidos = perfis.Where(p => p.EmpresaId is null || p.EmpresaId == empresa).ToList();
    }
}
