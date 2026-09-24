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

    /// <summary>Pessoa completa (estabelecimentos, endereços, contatos, papéis, contas, bloqueios), sem rastreamento.</summary>
    Task<Pessoa?> ObterAsync(Guid id, CancellationToken ct);

    Task<int> ContarClientesAtivosAsync(CancellationToken ct);

    /// <summary>Datas de nascimento das pessoas físicas ativas (nula = não informada), opcionalmente de um papel.</summary>
    Task<List<DateOnly?>> ListarNascimentosAsync(TipoPapel? papel, CancellationToken ct);

    /// <summary>Etiquetas em uso, em ordem alfabética.</summary>
    Task<List<string>> ListarEtiquetasAsync(CancellationToken ct);

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
}
