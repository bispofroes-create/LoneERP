using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Violações CONHECIDAS das proteções da pessoa no banco (endereço × finalidade e, desde a Etapa 4, a carteira de
/// vendedores), traduzidas em mensagem para o usuário (a
/// gravação responde 409 com o erro original anexado). Só estes casos: qualquer outra violação (outro índice, FK,
/// CHECK...) não é reconhecida aqui e continua como erro inesperado.
/// </summary>
public static class ConflitosEnderecoFinalidade
{
    public const string PrincipalRepetido =
        "Não foi possível salvar porque outro endereço já está definido como principal para esta finalidade. " +
        "Atualize os dados e tente novamente.";
    public const string FinalidadeRepetida =
        "Não foi possível salvar porque este endereço já possui esta finalidade. Atualize os dados e tente novamente.";
    public const string EnderecoInativoPrincipal =
        "Não foi possível salvar porque um endereço inativo ficaria como principal de uma finalidade. " +
        "Atualize os dados e tente novamente.";
    public const string CarteiraSobreposta =
        "Não foi possível salvar porque esta pessoa ficaria com dois vínculos ativos do mesmo papel de um por vez (ou " +
        "exclusivo) no mesmo período. Atualize os dados e tente novamente.";
    public const string CarteiraAcimaDoLimite =
        "Não foi possível salvar porque esta pessoa ficaria com mais vínculos ativos de um papel ao mesmo tempo do que o " +
        "limite do papel. Atualize os dados e tente novamente.";

    /// <summary>Mensagem do conflito, ou nulo se o erro não é um dos casos conhecidos.</summary>
    public static string? Mensagem(Exception erro) => erro switch
    {
        DbUpdateException { InnerException: SqlException sql } => Mensagem(sql.Number, sql.Message),
        SqlException sql => Mensagem(sql.Number, sql.Message),
        _ => null
    };

    /// <summary>2601/2627 = chave duplicada em índice único (distinguido pelo NOME do índice); 5004x = gatilhos.</summary>
    public static string? Mensagem(int numero, string texto) => numero switch
    {
        2601 or 2627 when texto.Contains(SqlMigracaoFinalidadesEndereco.IndicePrincipal, StringComparison.Ordinal) => PrincipalRepetido,
        2601 or 2627 when texto.Contains(SqlMigracaoFinalidadesEndereco.IndiceFinalidadeAtiva, StringComparison.Ordinal) => FinalidadeRepetida,
        SqlMigracaoFinalidadesEndereco.ErroPrincipalEmEnderecoInativo or
        SqlMigracaoFinalidadesEndereco.ErroEnderecoInativoComPrincipal => EnderecoInativoPrincipal,
        SqlMigracaoCarteira.ErroSobreposicao => CarteiraSobreposta, // gatilho da carteira (Etapa 4)
        SqlMigracaoCarteira.ErroAcimaDoLimite => CarteiraAcimaDoLimite, // gatilho da carteira (Motor Comercial, Fase 1a)
        _ => null
    };
}
