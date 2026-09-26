using Lone.Domain.Privacidade;

namespace Lone.Infrastructure.Persistencia;

/// <summary>
/// Fase 3 (privacidade): SQL da migração e nomes das proteções usados pelo modelo do EF e pela tradução de erros.
///
/// <see cref="ConsentimentosAnteriores"/> (dados): colocado pela ferramenta Ferramentas/inserir-sql-privacidade.py na
/// migração da Fase 3, DEPOIS de criar FinalidadesTratamento com os dados iniciais e a coluna
/// PessoaConsentimentos.FinalidadeId, e ANTES da FK para FinalidadesTratamento (a coluna nasce com Guid vazio).
/// Regra: todos os consentimentos que existiam (um por canal, sem finalidade) passam para a finalidade de sistema
/// "Registro anterior" (somente histórico). Nada mais muda: canal, datas, origem e "Concedido" ficam como estavam;
/// quem concedeu, motivo e versão do termo ficam NULL (não existiam). Nenhum consentimento é criado; Marketing do e-mail e
/// "Aceita comunicações" NÃO viram consentimento. Conferência no fim: se sobrar registro sem finalidade, a migração é
/// desfeita (THROW dentro da transação).
/// </summary>
public static class SqlMigracaoPrivacidade
{
    public const string IndiceEmVigor = "IX_PessoaConsentimentos_EmVigor";
    public const string CheckSistemaAtiva = "CK_FinalidadesTratamento_SistemaAtiva";
    public const string CheckRevogadoForaDeVigor = "CK_PessoaConsentimentos_RevogadoForaDeVigor";
    public const int ErroConsentimentoSemFinalidade = 50050;

    public static readonly string RegistroAnteriorId =
        FinalidadesTratamentoIniciais.Id(FinalidadesTratamentoIniciais.RegistroAnterior).ToString().ToUpperInvariant();

    public static readonly string ConsentimentosAnteriores = $"""
        UPDATE PessoaConsentimentos
           SET FinalidadeId = '{RegistroAnteriorId}'
         WHERE FinalidadeId = '00000000-0000-0000-0000-000000000000';

        IF EXISTS (SELECT 1 FROM PessoaConsentimentos WHERE FinalidadeId = '00000000-0000-0000-0000-000000000000')
            THROW {ErroConsentimentoSemFinalidade}, 'Privacidade: sobrou consentimento sem finalidade depois da migração.', 1;
        """;
}
