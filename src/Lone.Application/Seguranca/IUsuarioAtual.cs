namespace Lone.Application.Seguranca;

/// <summary>Quem está fazendo a operação. Na API: o usuário do token da requisição.</summary>
public interface IUsuarioAtual
{
    /// <summary>Nulo em requisições sem login (ex.: primeiro acesso, login).</summary>
    Guid? Id { get; }

    /// <summary>Nome gravado na auditoria ("sistema" sem login).</summary>
    string Nome { get; }
}

/// <summary>Empresa e estabelecimento em que o usuário está trabalhando nesta requisição.</summary>
public interface IEmpresaAtual
{
    /// <summary>Nulo enquanto nenhuma empresa foi escolhida (ou nenhuma está cadastrada).</summary>
    Guid? EmpresaId { get; }

    Guid? EstabelecimentoId { get; }
}
