namespace Lone.Aplicacao.Comum;

/// <summary>Outro usuário gravou o mesmo registro depois que ele foi aberto; nada foi salvo.</summary>
public class ConflitoDeEdicaoException : Exception
{
    public ConflitoDeEdicaoException(Exception? interna = null)
        : base("Outro usuário alterou este cadastro enquanto você editava. Nada foi salvo: " +
               "clique em \"Descartar alterações\" para carregar a versão atual e refaça suas mudanças.", interna)
    {
    }
}
