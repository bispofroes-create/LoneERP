using Lone.Contracts.Pessoas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Application.Pessoas;

/// <summary>
/// Persistência do agregado Pessoa. Implementado em Lone.Infrastructure.
/// Métodos específicos (nada de repositório genérico): cada um existe porque um caso de uso precisa.
/// </summary>
public interface IPessoaRepositorio
{
    Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct);

    /// <summary>Uma página (começa em 1) com o total que atende aos filtros; mesma ordem da lista.</summary>
    Task<PaginaListaPessoas> ListarPaginaAsync(FiltroPessoas filtro, int pagina, int tamanho, CancellationToken ct);

    /// <summary>Página da lista com as condições do catálogo de filtros (já conferidas pelo serviço).</summary>
    Task<PaginaListaPessoas> ListarPaginaAsync(FiltroPessoas filtro, IReadOnlyList<CondicaoFiltro> condicoes, DateOnly hoje,
                                               int pagina, int tamanho, CancellationToken ct);

    /// <summary>
    /// Página com colunas extras (Ids já conferidos; vêm em PessoaResumo.Valores) e ordenação por coluna (nula = nome).
    /// </summary>
    Task<PaginaListaPessoas> ListarPaginaAsync(FiltroPessoas filtro, IReadOnlyList<CondicaoFiltro> condicoes,
                                               IReadOnlyList<string> colunas, OrdenacaoLista? ordenacao, DateOnly hoje,
                                               int pagina, int tamanho, CancellationToken ct);

    /// <summary>Pessoa completa (estabelecimentos, endereços, contatos, papéis, contas, bloqueios), sem rastreamento.</summary>
    Task<Pessoa?> ObterAsync(Guid id, CancellationToken ct);

    Task<int> ContarClientesAtivosAsync(CancellationToken ct);

    /// <summary>Datas de nascimento das pessoas físicas ativas (nula = não informada), opcionalmente de um papel.</summary>
    Task<List<DateOnly?>> ListarNascimentosAsync(TipoPapel? papel, CancellationToken ct);

    /// <summary>Outra pessoa da mesma natureza com o mesmo documento principal (CPF, ou raiz do CNPJ).</summary>
    Task<PessoaIdentificacao?> BuscarPorDocumentoAsync(NaturezaPessoa natureza, string documento, Guid ignorarId, CancellationToken ct);

    /// <summary>Pessoas com o mesmo nome, ou algum telefone/e-mail igual (possíveis duplicadas).</summary>
    Task<List<PessoaIdentificacao>> BuscarSemelhantesAsync(
        Guid ignorarId, string nome, IReadOnlyCollection<string> contatos, CancellationToken ct);

    /// <summary>Textos de município antigos desta pessoa ainda sem município escolhido.</summary>
    Task<List<PendenciaMunicipio>> ListarPendenciasMunicipioAsync(Guid pessoaId, CancellationToken ct);

    /// <summary>
    /// Inclui (<paramref name="nova"/>) ou altera a pessoa inteira (filhos e contas sincronizados; bloqueios e
    /// relacionamentos não são tocados), com auditoria na mesma transação.
    /// Lança ConflitoDeEdicaoException se outro usuário gravou antes.
    /// </summary>
    Task SalvarAsync(Pessoa pessoa, bool nova, OrigemAlteracao origem, CancellationToken ct);

    /// <summary>
    /// Inclui a pessoa nova e o relacionamento com que ela nasce na mesma transação (Fase 2a-3, E9): ou os dois ficam, ou
    /// nenhum. Sem isso, com alcance restrito, a pessoa poderia ficar gravada sem a relação que a põe no alcance.
    /// </summary>
    Task SalvarNovaComRelacionamentoAsync(Pessoa pessoa, PessoaRelacionamento relacao, OrigemAlteracao origem, CancellationToken ct) =>
        throw new NotSupportedException("Este repositório não grava o relacionamento inicial.");
}
