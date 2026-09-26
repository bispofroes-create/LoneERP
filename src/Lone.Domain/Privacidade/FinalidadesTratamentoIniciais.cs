using Lone.Domain.Enums;

namespace Lone.Domain.Privacidade;

/// <summary>
/// Finalidades de tratamento que nascem com a base (Ids estáveis: a migração dos consentimentos antigos depende deles).
/// O resto do sistema usa o Id vindo do cadastro; regra que precisa de uma específica usa o Código. Só estas duas:
/// as demais (contrato, cobrança, newsletter...) serão criadas quando tiverem regra de negócio.
/// </summary>
public static class FinalidadesTratamentoIniciais
{
    public const string Marketing = "MARKETING";

    /// <summary>Guarda os consentimentos de antes da Fase 3 (por canal, sem finalidade). Somente histórico.</summary>
    public const string RegistroAnterior = "REGISTRO_ANTERIOR";

    public static IReadOnlyList<Inicial> Todas { get; } =
    [
        new(new Guid("7a9e1c08-0000-0000-0000-000000000001"), Marketing, "Marketing",
            "Envio de ofertas, novidades e campanhas.", BaseLegal.Consentimento, ClassificacaoCanal.Marketing,
            SomenteHistorico: false, Ordem: 1),
        new(new Guid("7a9e1c08-0000-0000-0000-000000000099"), RegistroAnterior, "Registro anterior",
            "Consentimentos registrados antes do cadastro de finalidades (por canal, sem finalidade). Somente histórico: não autoriza nenhuma comunicação.",
            BaseLegal.NaoDefinida, ClassificacaoCanal.Nenhuma, SomenteHistorico: true, Ordem: 99)
    ];

    public static bool EhCodigoDeSistema(string? codigo) => Todas.Any(t => t.Codigo == codigo);

    public static Guid Id(string codigo) => Todas.First(t => t.Codigo == codigo).Id;

    public sealed record Inicial(Guid Id, string Codigo, string Nome, string Descricao, BaseLegal BaseLegal,
                                 ClassificacaoCanal ClassificacaoExigida, bool SomenteHistorico, int Ordem);
}
