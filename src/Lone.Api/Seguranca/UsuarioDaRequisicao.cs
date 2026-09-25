using Lone.Application.Seguranca;
using Lone.Contracts.Seguranca;

namespace Lone.Api.Seguranca;

/// <summary>
/// Quem está fazendo esta requisição, na empresa do token, e o que pode fazer.
/// Um por requisição (scoped); preenchido por <see cref="CarregarUsuarioMiddleware"/> logo após a autenticação.
/// Sem login (ex.: tela de login) fica vazio: Nome "sistema" e nenhuma permissão.
/// </summary>
public sealed class UsuarioDaRequisicao : IUsuarioAtual, IEmpresaAtual, IAutorizacao, IMotivoDaOperacao
{
    /// <summary>Motivo da operação desta requisição (auditoria); definido pelo serviço que o recebe.</summary>
    public string? Motivo { get; set; }

    private AcessoEfetivo? _acesso;

    public Guid? Id { get; private set; }
    public string Nome { get; private set; } = "sistema";
    public Guid? EmpresaId { get; private set; }
    public Guid? EstabelecimentoId { get; private set; }
    public bool DeveTrocarSenha { get; private set; }

    public bool Autenticado => Id is not null;

    /// <summary>Id do usuário logado; só chame em endpoints que exigem login.</summary>
    public Guid IdObrigatorio => Id ?? throw new SessaoInvalidaException();

    internal void Definir(AcessoDoUsuario acesso, Guid? empresaId, Guid? estabelecimentoId)
    {
        Id = acesso.UsuarioId;
        Nome = acesso.Nome;
        DeveTrocarSenha = acesso.DeveTrocarSenha;
        EmpresaId = empresaId;
        EstabelecimentoId = estabelecimentoId;
        _acesso = acesso.Acesso;
    }

    public bool Possui(string permissao) => _acesso?.Possui(permissao) == true;

    public void Exigir(string permissao)
    {
        if (!Possui(permissao))
            throw new AcessoNegadoException(permissao);
    }
}
