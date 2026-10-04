namespace Lone.Cliente.ViewModels.Comum;

/// <summary>
/// O que a lista de cadastro vazia diz (04/10/2026; referência: "No data" × "No matching items" do SAP Fiori):
/// <list type="bullet">
/// <item>nada cadastrado: a tela mostra o seu próprio texto (<see cref="PorFiltro"/> falso);</item>
/// <item>nada para a pesquisa: "Nenhum resultado para "x"" e "Limpar pesquisa" — e, se a pesquisa acha registros em
/// outra situação, diz quantos;</item>
/// <item>nada na situação escolhida (sem pesquisa): "Nenhum cadastro em "Ativos"" e "Mostrar todos".</item>
/// </list>
/// Nunca repete o botão "+ Novo" (um só por tela, no alto — decisão de 03/10/2026).
/// </summary>
public sealed record EstadoListaVazia(bool PorFiltro, string Titulo, string Texto, string Acao)
{
    public static readonly EstadoListaVazia Nenhum = new(false, string.Empty, string.Empty, string.Empty);

    /// <param name="visiveis">Itens na lista agora.</param>
    /// <param name="termo">Texto da pesquisa (vazio = sem pesquisa).</param>
    /// <param name="naSituacao">Itens na situação escolhida, sem a pesquisa.</param>
    /// <param name="comPesquisa">Itens que a pesquisa acha, em qualquer situação.</param>
    /// <param name="total">Itens lidos, em qualquer situação.</param>
    /// <param name="situacao">Situação escolhida ("Ativos"...); nulo quando a tela não tem o filtro.</param>
    public static EstadoListaVazia Calcular(int visiveis, string termo, int naSituacao, int comPesquisa, int total, string? situacao)
    {
        if (visiveis > 0 || total == 0) return Nenhum;
        if (termo.Length > 0)
        {
            var texto = comPesquisa > 0 && situacao is not null
                ? $"{(comPesquisa == 1 ? "1 cadastro com este texto está" : $"{comPesquisa} cadastros com este texto estão")} fora de \"{situacao}\". Mude a Situação para ver."
                : "Confira o texto ou limpe a pesquisa.";
            return new EstadoListaVazia(true, $"Nenhum resultado para \"{termo}\"", texto, "Limpar pesquisa");
        }
        if (situacao is not null && naSituacao == 0)
            return new EstadoListaVazia(true, $"Nenhum cadastro em \"{situacao}\"",
                total == 1 ? "Há 1 cadastro em outra situação." : $"Há {total} cadastros em outra situação.", "Mostrar todos");
        return Nenhum;
    }
}
