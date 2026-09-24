namespace Lone.Aplicacao.Seguranca;

/// <summary>Verifica o que o usuário atual pode fazer. Usada pelos serviços de aplicação e pelos ViewModels.</summary>
public interface IAutorizacao
{
    bool Possui(string permissao);

    /// <summary>Lança AcessoNegadoException se o usuário não tiver a permissão.</summary>
    void Exigir(string permissao);
}
