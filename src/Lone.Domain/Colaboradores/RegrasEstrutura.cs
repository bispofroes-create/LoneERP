using Lone.Domain.Entidades;

namespace Lone.Domain.Colaboradores;

/// <summary>Regras dos cadastros da estrutura organizacional (cargo, departamento, setor, centro de custo). Não acessa banco.</summary>
public static class RegrasEstrutura
{
    public static string Texto(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? string.Empty : string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static List<string> ValidarNome(string nome, int maximo, string oQue)
    {
        var erros = new List<string>();
        if (nome.Length == 0) erros.Add($"Informe o nome do {oQue}.");
        else if (nome.Length > maximo) erros.Add($"O nome do {oQue} pode ter no máximo {maximo} caracteres.");
        return erros;
    }

    /// <summary>Código do centro de custo: sem espaços, só letras, números, ponto, hífen e barra (ex.: "1.01", "ADM-02").</summary>
    public static string CodigoCentroCusto(string? codigo) =>
        new((codigo ?? string.Empty).Trim().ToUpperInvariant().Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '/').ToArray());

    /// <summary>
    /// Árvore dos centros de custo: o pai existe, é sintético e ativo (para quem está mudando de pai), e não há ciclo
    /// (o centro não pode ficar abaixo de si mesmo). Sintético não pode virar analítico enquanto tiver filhos.
    /// </summary>
    /// <param name="todos">Todos os centros de custo gravados (o editado, se já existir, com os dados antigos).</param>
    public static List<string> ValidarCentroCusto(CentroCusto cc, IReadOnlyCollection<CentroCusto> todos)
    {
        var erros = new List<string>();
        if (cc.Codigo.Length == 0) erros.Add("Informe o código do centro de custo.");
        else if (cc.Codigo.Length > CentroCusto.TamanhoMaximoCodigo)
            erros.Add($"O código pode ter no máximo {CentroCusto.TamanhoMaximoCodigo} caracteres.");
        erros.AddRange(ValidarNome(cc.Nome, CentroCusto.TamanhoMaximoNome, "centro de custo"));

        var porId = todos.ToDictionary(c => c.Id);
        var anterior = porId.GetValueOrDefault(cc.Id);
        if (cc.PaiId is { } paiId)
        {
            if (!porId.TryGetValue(paiId, out var pai))
                erros.Add("O centro de custo pai não existe mais.");
            else
            {
                if (pai.Analitico) erros.Add($"\"{pai.Descricao}\" é analítico: marque-o como sintético para ter centros abaixo dele.");
                if (!pai.Ativo && anterior?.PaiId != paiId) erros.Add($"\"{pai.Descricao}\" está desativado.");

                // Sobe a árvore a partir do novo pai: se passar pelo próprio centro, formaria um ciclo.
                var visitados = new HashSet<Guid>();
                for (Guid? atual = paiId; atual is { } id && visitados.Add(id); atual = porId.GetValueOrDefault(id)?.PaiId)
                    if (id == cc.Id)
                    {
                        erros.Add("Um centro de custo não pode ficar abaixo dele mesmo (nem de um centro que está abaixo dele).");
                        break;
                    }
            }
        }

        if (cc.Analitico && todos.Any(c => c.PaiId == cc.Id))
            erros.Add("Este centro de custo tem centros abaixo dele: ele precisa continuar sintético.");
        return erros;
    }
}
