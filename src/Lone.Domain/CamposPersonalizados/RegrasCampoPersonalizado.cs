using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.CamposPersonalizados;

/// <summary>Normalização e regras da definição de um campo personalizado. Não acessa banco.</summary>
public static class RegrasCampoPersonalizado
{
    public const int MaximoOpcoes = 100;

    public static void Normalizar(CampoPersonalizado c)
    {
        c.Nome = TiposCampo.Texto(c.Nome) ?? string.Empty;
        c.Dica = TiposCampo.Texto(c.Dica);

        if (c.Tipo != TipoCampoPersonalizado.Decimal) c.CasasDecimais = null;
        if (c.Tipo is not (TipoCampoPersonalizado.Inteiro or TipoCampoPersonalizado.Decimal or TipoCampoPersonalizado.Moeda))
            c.Minimo = c.Maximo = null;

        foreach (var o in c.Opcoes) o.Texto = TiposCampo.Texto(o.Texto) ?? string.Empty;
        if (c.Tipo != TipoCampoPersonalizado.Lista)
        {
            // Opções de um campo que não é lista não servem para nada; as gravadas ficam (o repositório não apaga).
            c.Opcoes.RemoveAll(o => o.Texto.Length == 0);
        }
        for (var i = 0; i < c.Opcoes.Count; i++) c.Opcoes[i].Ordem = i;
    }

    public static List<string> Validar(CampoPersonalizado c)
    {
        var erros = new List<string>();

        if (c.Nome.Length == 0) erros.Add("Informe o nome do campo.");
        else if (c.Nome.Length > CampoPersonalizado.TamanhoMaximoNome)
            erros.Add($"O nome pode ter no máximo {CampoPersonalizado.TamanhoMaximoNome} caracteres.");

        if (!TiposCampo.Existe(c.Tipo)) erros.Add("Tipo de campo inválido.");
        if (c.Dica is { Length: > CampoPersonalizado.TamanhoMaximoDica })
            erros.Add($"A dica pode ter no máximo {CampoPersonalizado.TamanhoMaximoDica} caracteres.");
        if (c.CasasDecimais is > TiposCampo.MaximoCasasDecimais)
            erros.Add($"Use no máximo {TiposCampo.MaximoCasasDecimais} casas decimais.");
        if (c.Minimo is { } minimo && c.Maximo is { } maximo && minimo > maximo)
            erros.Add("O valor mínimo é maior que o máximo.");

        if (c.Tipo == TipoCampoPersonalizado.Lista)
        {
            if (c.Opcoes.Any(o => o.Texto.Length == 0)) erros.Add("Informe o texto de cada opção.");
            if (c.Opcoes.Any(o => o.Texto.Length > CampoPersonalizadoOpcao.TamanhoMaximoTexto))
                erros.Add($"Cada opção pode ter no máximo {CampoPersonalizadoOpcao.TamanhoMaximoTexto} caracteres.");
            if (!c.Opcoes.Any(o => o.Ativa && o.Texto.Length > 0))
                erros.Add("Uma lista de opções precisa de ao menos uma opção ativa.");
            if (c.Opcoes.Count > MaximoOpcoes)
                erros.Add($"Use no máximo {MaximoOpcoes} opções.");
            var repetida = c.Opcoes.Where(o => o.Texto.Length > 0)
                .GroupBy(o => TextoBusca.Normalizar(o.Texto)).FirstOrDefault(g => g.Count() > 1);
            if (repetida is not null)
                erros.Add($"A opção \"{repetida.First().Texto}\" está repetida.");
        }

        return erros;
    }
}
