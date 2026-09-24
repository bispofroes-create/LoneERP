namespace Lone.Application.Seguranca;

/// <summary>
/// O que o usuário atual pode fazer na empresa ativa. Usada pelos serviços de aplicação:
/// a permissão é conferida aqui, no servidor, mesmo que a tela já tenha escondido a opção.
/// </summary>
public interface IAutorizacao
{
    bool Possui(string permissao);

    /// <summary>Lança AcessoNegadoException se o usuário não tiver a permissão.</summary>
    void Exigir(string permissao);
}
