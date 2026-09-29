namespace Lone.Domain.Territorios;

/// <summary>
/// Os tipos de território que nascem com a base (padrão pronto para a empresa pequena), com Ids fixos. Só classificam:
/// o usuário pode renomear, reordenar ou desativar; o código não muda.
/// </summary>
public static class TiposTerritorioIniciais
{
    public static IReadOnlyList<(Guid Id, string Codigo, string Nome, string Descricao, int Ordem)> Todos { get; } =
    [
        (new Guid("7a9e1c0b-0000-0000-0000-000000000001"), "GEOGRAFICO", "Geográfico", "País, região, estado, cidade, bairro...", 1),
        (new Guid("7a9e1c0b-0000-0000-0000-000000000002"), "SEGMENTO", "Segmento", "Ramo de atividade, porte, canal...", 2),
        (new Guid("7a9e1c0b-0000-0000-0000-000000000003"), "ESTRATEGICO", "Estratégico", "Grandes contas, contas-chave, contas especiais...", 3)
    ];

    public static bool EhDoSistema(Guid id) => Todos.Any(t => t.Id == id);
}
