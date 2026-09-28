namespace Lone.Contracts.Menu;

/// <summary>
/// Limites do menu do usuário, iguais na API e no aplicativo. Os recentes guardados são mais que os exibidos para que,
/// ao perder a permissão de uma tela, a lista continue cheia com as outras.
/// </summary>
public static class LimitesMenu
{
    public const int MaximoFavoritos = 20;
    public const int RecentesExibidos = 5;
    public const int RecentesDevolvidos = 15;

    /// <summary>A rota é o nome da tela no Shell (ex.: "consulta-pessoas"): minúsculas, números e hífen.</summary>
    public const int TamanhoMaximoRota = 60;

    /// <summary>Preferência de uma tela (colunas da lista, ordenação...): JSON até este tamanho.</summary>
    public const int TamanhoMaximoPreferenciaTela = 4000;
}

/// <summary>Preferência de uma tela do usuário logado (JSON que só a própria tela entende; vazio = nenhuma).</summary>
public sealed class PreferenciaTelaDto
{
    public string Conteudo { get; set; } = string.Empty;
}

/// <summary>Favoritos (na ordem em que foram marcados) e recentes (o mais novo primeiro) do usuário logado, por rota.</summary>
public sealed class PreferenciasMenuDto
{
    public List<string> Favoritos { get; set; } = new();
    public List<string> Recentes { get; set; } = new();
}

/// <summary>Marca ou desmarca uma tela como favorita.</summary>
public sealed class FavoritoMenuRequisicao
{
    public string Rota { get; set; } = string.Empty;
    public bool Favorito { get; set; }
}

/// <summary>Tela aberta pelo usuário (alimenta os recentes).</summary>
public sealed class AcessoMenuRequisicao
{
    public string Rota { get; set; } = string.Empty;
}
