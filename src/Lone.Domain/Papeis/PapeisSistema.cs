using Lone.Domain.Enums;

namespace Lone.Domain.Papeis;

/// <summary>Um papel de sistema: Id fixo (o mesmo em todas as bases), código, nome inicial e ordem na ficha.</summary>
public sealed record PapelDeSistema(TipoPapel Tipo, Guid Id, string Codigo, string Nome, int Ordem);

/// <summary>
/// Os oito papéis que o código conhece (enum <see cref="TipoPapel"/>). Nascem com a base (dados iniciais da migração)
/// com estes Ids; o nome pode ser mudado pelo usuário, o código e o vínculo com o enum não.
/// </summary>
public static class PapeisSistema
{
    public static IReadOnlyList<PapelDeSistema> Todos { get; } =
    [
        new(TipoPapel.Cliente, new Guid("7a9e1c00-0000-0000-0000-000000000001"), "CLIENTE", "Cliente", 1),
        new(TipoPapel.Fornecedor, new Guid("7a9e1c00-0000-0000-0000-000000000002"), "FORNECEDOR", "Fornecedor", 2),
        new(TipoPapel.Vendedor, new Guid("7a9e1c00-0000-0000-0000-000000000004"), "VENDEDOR", "Vendedor", 3),
        new(TipoPapel.Transportadora, new Guid("7a9e1c00-0000-0000-0000-000000000006"), "TRANSPORTADORA", "Transportadora", 4),
        new(TipoPapel.Representante, new Guid("7a9e1c00-0000-0000-0000-000000000007"), "REPRESENTANTE", "Representante", 5),
        new(TipoPapel.PrestadorServico, new Guid("7a9e1c00-0000-0000-0000-000000000008"), "PRESTADOR_SERVICO", "Prestador de serviço", 6),
        new(TipoPapel.Funcionario, new Guid("7a9e1c00-0000-0000-0000-000000000005"), "FUNCIONARIO", "Funcionário", 7),
        new(TipoPapel.EmpresaDoGrupo, new Guid("7a9e1c00-0000-0000-0000-000000000003"), "EMPRESA_DO_GRUPO", "Empresa do grupo", 8)
    ];

    public static PapelDeSistema De(TipoPapel tipo) => Todos.First(p => p.Tipo == tipo);

    public static Guid Id(TipoPapel tipo) => De(tipo).Id;

    /// <summary>
    /// Papéis com regra no código (conta de cliente/fornecedor, empresas do grupo e acesso, dados sensíveis de
    /// funcionário): não podem ser desativados no cadastro de papéis.
    /// </summary>
    public static bool TemRegra(TipoPapel tipo) =>
        tipo is TipoPapel.Cliente or TipoPapel.Fornecedor or TipoPapel.EmpresaDoGrupo or TipoPapel.Funcionario;
}
