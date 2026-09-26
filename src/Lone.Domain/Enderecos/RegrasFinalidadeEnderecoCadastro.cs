using Lone.Domain.Entidades;

namespace Lone.Domain.Enderecos;

/// <summary>
/// Proteção das finalidades de sistema (COMERCIAL, RESIDENCIAL, FISCAL, ENTREGA, COBRANCA, CORRESPONDENCIA): o código
/// não muda, a marca "de sistema" não sai e ela não é desativada (nem excluída — nada é excluído). O banco repete a
/// regra com um gatilho em FinalidadesEndereco. Finalidades criadas pelo usuário podem mudar de nome e ser desativadas,
/// mas também não mudam de código (as regras usam o código).
/// </summary>
public static class RegrasFinalidadeEnderecoCadastro
{
    public static List<string> ValidarAlteracao(FinalidadeEnderecoCadastro? anterior, FinalidadeEnderecoCadastro nova)
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(nova.Codigo) || nova.Codigo.Length > FinalidadeEnderecoCadastro.TamanhoMaximoCodigo)
            erros.Add($"Informe o código da finalidade (até {FinalidadeEnderecoCadastro.TamanhoMaximoCodigo} caracteres).");
        if (string.IsNullOrWhiteSpace(nova.Nome) || nova.Nome.Length > FinalidadeEnderecoCadastro.TamanhoMaximoNome)
            erros.Add($"Informe o nome da finalidade (até {FinalidadeEnderecoCadastro.TamanhoMaximoNome} caracteres).");

        if (anterior is null)
        {
            if (nova.DoSistema || FinalidadesEnderecoIniciais.EhCodigoDeSistema(nova.Codigo))
                erros.Add("Este código é de uma finalidade de sistema: escolha outro.");
            return erros;
        }

        if (!string.Equals(anterior.Codigo, nova.Codigo, StringComparison.Ordinal))
            erros.Add("O código de uma finalidade não pode ser alterado (as regras do sistema usam o código).");
        if (anterior.DoSistema)
        {
            if (!nova.DoSistema)
                erros.Add($"A finalidade {anterior.Nome} é de sistema e continua sendo.");
            if (!nova.Ativo)
                erros.Add($"A finalidade {anterior.Nome} é de sistema e não pode ser desativada.");
        }
        else if (nova.DoSistema)
            erros.Add("Uma finalidade criada pelo usuário não pode virar de sistema.");
        return erros;
    }
}
