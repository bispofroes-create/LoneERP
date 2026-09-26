using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Privacidade;

/// <summary>
/// Cadastro de finalidades de tratamento (mesmas proteções das finalidades de endereço): código obrigatório e imutável,
/// finalidade de sistema continua de sistema e não é desativada; "somente histórico" é só das de sistema. A base legal
/// precisa ter regra no Lone (<see cref="BasesLegaisComRegra"/>): hoje, só Consentimento.
/// </summary>
public static class RegrasFinalidadeTratamento
{
    /// <summary>Bases legais que já têm regra de comunicação. As outras ficam modeladas, sem regra inventada.</summary>
    public static IReadOnlyList<BaseLegal> BasesLegaisComRegra { get; } = [BaseLegal.Consentimento];

    public static void Normalizar(FinalidadeTratamento f)
    {
        f.Codigo = (f.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        f.Nome = string.Join(' ', (f.Nome ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        f.Descricao = string.IsNullOrWhiteSpace(f.Descricao) ? null : f.Descricao.Trim();
    }

    public static List<string> ValidarAlteracao(FinalidadeTratamento? anterior, FinalidadeTratamento nova)
    {
        var erros = new List<string>();
        if (nova.Codigo.Length == 0 || nova.Codigo.Length > FinalidadeTratamento.TamanhoMaximoCodigo)
            erros.Add($"Informe o código da finalidade (até {FinalidadeTratamento.TamanhoMaximoCodigo} caracteres).");
        if (nova.Nome.Length == 0 || nova.Nome.Length > FinalidadeTratamento.TamanhoMaximoNome)
            erros.Add($"Informe o nome da finalidade (até {FinalidadeTratamento.TamanhoMaximoNome} caracteres).");
        if (nova.Descricao is { Length: > FinalidadeTratamento.TamanhoMaximoDescricao })
            erros.Add($"A descrição pode ter no máximo {FinalidadeTratamento.TamanhoMaximoDescricao} caracteres.");
        if (!Enum.IsDefined(nova.ClassificacaoExigida))
            erros.Add("Classificação do canal inválida.");

        if (anterior is null)
        {
            if (nova.DoSistema || FinalidadesTratamentoIniciais.EhCodigoDeSistema(nova.Codigo))
                erros.Add("Este código é de uma finalidade de sistema: escolha outro.");
            if (nova.SomenteHistorico)
                erros.Add("Só a finalidade de sistema \"Registro anterior\" é somente histórico.");
            if (!BasesLegaisComRegra.Contains(nova.BaseLegal))
                erros.Add("Escolha a base legal. Nesta versão, só a base legal Consentimento tem regra no Lone.");
            return erros;
        }

        if (!string.Equals(anterior.Codigo, nova.Codigo, StringComparison.Ordinal))
            erros.Add("O código de uma finalidade não pode ser alterado (as regras do sistema usam o código).");
        if (anterior.SomenteHistorico != nova.SomenteHistorico)
            erros.Add("A marca \"somente histórico\" não pode ser alterada.");
        if (anterior.BaseLegal != nova.BaseLegal && !BasesLegaisComRegra.Contains(nova.BaseLegal))
            erros.Add("Nesta versão, só a base legal Consentimento tem regra no Lone.");
        if (anterior.DoSistema)
        {
            if (!nova.DoSistema) erros.Add($"A finalidade {anterior.Nome} é de sistema e continua sendo.");
            if (!nova.Ativo) erros.Add($"A finalidade {anterior.Nome} é de sistema e não pode ser desativada.");
            if (anterior.BaseLegal != nova.BaseLegal) erros.Add($"A base legal da finalidade {anterior.Nome} (de sistema) não muda.");
            if (anterior.ClassificacaoExigida != nova.ClassificacaoExigida)
                erros.Add($"A classificação exigida da finalidade {anterior.Nome} (de sistema) não muda.");
        }
        else if (nova.DoSistema)
            erros.Add("Uma finalidade criada pelo usuário não pode virar de sistema.");
        return erros;
    }
}
