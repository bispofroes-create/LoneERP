using Lone.Domain.Entidades;

namespace Lone.Domain.CamposPersonalizados;

/// <summary>
/// Confere os valores personalizados de uma pessoa contra as definições dos campos. Não acessa banco.
/// Regras: campo desconhecido é recusado; campo desativado não é editável (mantém o gravado); obrigatório só
/// vale para campo ativo; opção desativada só é aceita se já era a gravada; vazio = sem linha.
/// </summary>
public static class ValidadorValoresPersonalizados
{
    /// <param name="valores">Enviados pelo cadastro (serão normalizados e filtrados aqui).</param>
    /// <param name="campos">Definições do cadastro (ativas e inativas), com as opções.</param>
    /// <param name="gravados">Valores que a pessoa já tinha (vazio se for nova).</param>
    public static List<string> Aplicar(
        List<PessoaValorPersonalizado> valores,
        IReadOnlyCollection<CampoPersonalizado> campos,
        IReadOnlyCollection<PessoaValorPersonalizado> gravados)
    {
        var erros = new List<string>();
        var porId = campos.ToDictionary(c => c.Id);
        var gravadoPorCampo = gravados.GroupBy(g => g.CampoId).ToDictionary(g => g.Key, g => g.First());

        // Um valor por campo; desconhecidos saem com erro.
        foreach (var repetido in valores.GroupBy(v => v.CampoId).SelectMany(g => g.Skip(1)).ToList())
            valores.Remove(repetido);
        foreach (var desconhecido in valores.Where(v => !porId.ContainsKey(v.CampoId)).ToList())
        {
            erros.Add("Informação adicional de um campo que não existe mais. Reabra o cadastro e tente de novo.");
            valores.Remove(desconhecido);
        }

        // Campo desativado: vale o que estava gravado, seja o que for que o aparelho mandou.
        valores.RemoveAll(v => !porId[v.CampoId].Ativo);
        foreach (var gravado in gravados.Where(g => porId.TryGetValue(g.CampoId, out var c) && !c.Ativo))
            valores.Add(Copia(gravado));

        foreach (var valor in valores.ToList())
        {
            var campo = porId[valor.CampoId];
            if (!campo.Ativo) continue;

            LimparOutrasColunas(valor, campo.Definicao.Coluna);
            if (campo.Definicao.Normalizar(campo, valor) is { } problema)
                erros.Add($"{campo.Nome}: {problema}.");
            else if (valor.OpcaoId is { } opcaoId && campo.Opcoes.FirstOrDefault(o => o.Id == opcaoId) is { Ativa: false } opcao &&
                     gravadoPorCampo.GetValueOrDefault(campo.Id)?.OpcaoId != opcaoId)
                erros.Add($"{campo.Nome}: a opção \"{opcao.Texto}\" foi desativada. Escolha outra.");

            if (valor.Vazio) valores.Remove(valor);
        }

        foreach (var campo in campos.Where(c => c.Ativo && c.Obrigatorio).OrderBy(c => c.Ordem))
            if (valores.All(v => v.CampoId != campo.Id))
                erros.Add($"Informe \"{campo.Nome}\" (informações adicionais).");

        return erros;
    }

    private static void LimparOutrasColunas(PessoaValorPersonalizado v, ColunaValor coluna)
    {
        if (coluna != ColunaValor.Texto) v.ValorTexto = null;
        if (coluna != ColunaValor.Numero) v.ValorNumero = null;
        if (coluna != ColunaValor.Data) v.ValorData = null;
        if (coluna != ColunaValor.Logico) v.ValorLogico = null;
        if (coluna != ColunaValor.Opcao) v.OpcaoId = null;
    }

    private static PessoaValorPersonalizado Copia(PessoaValorPersonalizado g) => new()
    {
        Id = g.Id,
        PessoaId = g.PessoaId,
        CampoId = g.CampoId,
        ValorTexto = g.ValorTexto,
        ValorNumero = g.ValorNumero,
        ValorData = g.ValorData,
        ValorLogico = g.ValorLogico,
        OpcaoId = g.OpcaoId
    };
}
