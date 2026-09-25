using Lone.Domain.Entidades;

namespace Lone.Domain.CamposPersonalizados;

/// <summary>
/// Confere os valores personalizados (da pessoa ou de um documento) contra as definições dos campos. Não acessa banco.
/// Regras: campo desconhecido é recusado; campo desativado ou oculto não é editável (mantém o gravado); obrigatório só
/// vale para campo ativo e visível; opção desativada só é aceita se já era a gravada; vazio = sem linha.
/// </summary>
public static class ValidadorValoresPersonalizados
{
    /// <param name="valores">Enviados pelo cadastro (serão normalizados e filtrados aqui).</param>
    /// <param name="campos">Definições que valem para estes valores (ativas e inativas), com as opções.</param>
    /// <param name="gravados">Valores que já estavam gravados (vazio se for novo).</param>
    /// <param name="onde">Onde o valor aparece, para as mensagens (ex.: "informações adicionais", "Documento 2 (CNH)").</param>
    public static List<string> Aplicar<T>(
        List<T> valores,
        IReadOnlyCollection<CampoPersonalizado> campos,
        IReadOnlyCollection<T> gravados,
        string onde = "informações adicionais") where T : ValorPersonalizado
    {
        var erros = new List<string>();
        var porId = campos.ToDictionary(c => c.Id);
        var gravadoPorCampo = gravados.GroupBy(g => g.CampoId).ToDictionary(g => g.Key, g => g.First());

        // Um valor por campo; desconhecidos saem com erro.
        foreach (var repetido in valores.GroupBy(v => v.CampoId).SelectMany(g => g.Skip(1)).ToList())
            valores.Remove(repetido);
        foreach (var desconhecido in valores.Where(v => !porId.ContainsKey(v.CampoId)).ToList())
        {
            erros.Add($"{Maiuscula(onde)}: campo que não existe mais (ou não vale para este tipo). Reabra o cadastro e tente de novo.");
            valores.Remove(desconhecido);
        }

        // Campo desativado ou oculto: vale o que estava gravado, seja o que for que o aparelho mandou.
        valores.RemoveAll(v => !Editavel(porId[v.CampoId]));
        foreach (var gravado in gravados.Where(g => porId.TryGetValue(g.CampoId, out var c) && !Editavel(c)))
            valores.Add((T)gravado.Clonar());

        foreach (var valor in valores.ToList())
        {
            var campo = porId[valor.CampoId];
            if (!Editavel(campo)) continue;

            LimparOutrasColunas(valor, campo.Definicao.Coluna);
            if (campo.Definicao.Normalizar(campo, valor) is { } problema)
                erros.Add($"{campo.Nome}: {problema}.");
            else if (valor.OpcaoId is { } opcaoId && campo.Opcoes.FirstOrDefault(o => o.Id == opcaoId) is { Ativa: false } opcao &&
                     gravadoPorCampo.GetValueOrDefault(campo.Id)?.OpcaoId != opcaoId)
                erros.Add($"{campo.Nome}: a opção \"{opcao.Texto}\" foi desativada. Escolha outra.");

            if (valor.Vazio) valores.Remove(valor);
        }

        foreach (var campo in campos.Where(c => Editavel(c) && c.Obrigatorio).OrderBy(c => c.Ordem))
            if (valores.All(v => v.CampoId != campo.Id))
                erros.Add($"Informe \"{campo.Nome}\" ({onde}).");

        return erros;
    }

    /// <summary>Documento removido (inativo): os valores ficam exatamente como estavam gravados.</summary>
    public static void ManterGravados<T>(List<T> valores, IEnumerable<T> gravados) where T : ValorPersonalizado
    {
        valores.Clear();
        valores.AddRange(gravados.Select(g => (T)g.Clonar()));
    }

    private static bool Editavel(CampoPersonalizado c) => c.Ativo && c.Visivel;

    private static string Maiuscula(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static void LimparOutrasColunas(ValorPersonalizado v, ColunaValor coluna)
    {
        if (coluna != ColunaValor.Texto) v.ValorTexto = null;
        if (coluna != ColunaValor.Numero) v.ValorNumero = null;
        if (coluna != ColunaValor.Data) v.ValorData = null;
        if (coluna != ColunaValor.Logico) v.ValorLogico = null;
        if (coluna != ColunaValor.Opcao) v.OpcaoId = null;
    }
}
