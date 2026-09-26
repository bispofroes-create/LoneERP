using Lone.Domain.Enums;

namespace Lone.Domain.Enderecos;

/// <summary>
/// As finalidades iniciais (de sistema): nascem com a base, com Ids estáveis (a migração dos dados antigos depende
/// deles). O resto do sistema usa o Id vindo do cadastro; regra que precisa de uma finalidade específica usa o Código.
/// O bit legado liga cada uma à coluna antiga PessoaEnderecos.Finalidades (só compatibilidade e migração).
/// </summary>
public static class FinalidadesEnderecoIniciais
{
    public const string Comercial = "COMERCIAL";
    public const string Residencial = "RESIDENCIAL";
    public const string Fiscal = "FISCAL";
    public const string Entrega = "ENTREGA";
    public const string Cobranca = "COBRANCA";
    public const string Correspondencia = "CORRESPONDENCIA";

    public static IReadOnlyList<(Guid Id, string Codigo, string Nome, int Ordem, FinalidadeEndereco BitLegado)> Todas { get; } =
    [
        (new Guid("7a9e1c07-0000-0000-0000-000000000001"), Comercial, "Comercial", 1, FinalidadeEndereco.Comercial),
        (new Guid("7a9e1c07-0000-0000-0000-000000000002"), Residencial, "Residencial", 2, FinalidadeEndereco.Residencial),
        (new Guid("7a9e1c07-0000-0000-0000-000000000003"), Fiscal, "Fiscal", 3, FinalidadeEndereco.Fiscal),
        (new Guid("7a9e1c07-0000-0000-0000-000000000004"), Entrega, "Entrega", 4, FinalidadeEndereco.Entrega),
        (new Guid("7a9e1c07-0000-0000-0000-000000000005"), Cobranca, "Cobrança", 5, FinalidadeEndereco.Cobranca),
        (new Guid("7a9e1c07-0000-0000-0000-000000000006"), Correspondencia, "Correspondência", 6, FinalidadeEndereco.Correspondencia)
    ];

    /// <summary>Código de uma finalidade de sistema (imutável; ela também não pode ser desativada nem excluída).</summary>
    public static bool EhCodigoDeSistema(string? codigo) => Todas.Any(t => t.Codigo == codigo);

    /// <summary>Id estável de uma finalidade de sistema pelo código.</summary>
    public static Guid Id(string codigo) => Todas.First(t => t.Codigo == codigo).Id;

    /// <summary>Bit da coluna legada para a finalidade (Nenhuma para as criadas pelo usuário).</summary>
    public static FinalidadeEndereco BitLegado(Guid finalidadeId) =>
        Todas.Where(t => t.Id == finalidadeId).Select(t => t.BitLegado).FirstOrDefault();
}
