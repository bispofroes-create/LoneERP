namespace Lone.Aplicacao.Seguranca;

/// <summary>Quem está usando o sistema.</summary>
public interface IUsuarioAtual
{
    /// <summary>Nulo antes do login.</summary>
    int? Id { get; }

    /// <summary>Nome gravado na auditoria ("sistema" antes do login).</summary>
    string Nome { get; }
}
