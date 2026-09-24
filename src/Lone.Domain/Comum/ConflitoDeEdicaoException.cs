namespace Lone.Domain.Comum;

/// <summary>Outro usuário gravou o mesmo registro depois que ele foi aberto; nada foi salvo.</summary>
public class ConflitoDeEdicaoException : Exception
{
    public const string MensagemPadrao =
        "Outro usuário alterou este cadastro enquanto você editava. Nada foi salvo: " +
        "descarte as alterações para carregar a versão atual e refaça suas mudanças.";

    public ConflitoDeEdicaoException(Exception? interna = null) : base(MensagemPadrao, interna) { }

    public ConflitoDeEdicaoException(string mensagem) : base(mensagem) { }
}
