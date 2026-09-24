using Lone.Core.Entidades;
using Lone.Core.Enums;

namespace Lone.Aplicacao.Pessoas;

/// <summary>
/// Persistência do agregado Pessoa. Implementado em Lone.Data.
/// Métodos específicos (nada de repositório genérico): cada um existe porque um caso de uso precisa.
/// </summary>
public interface IPessoaRepositorio
{
    Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct);

    /// <summary>Pessoa completa (estabelecimentos, endereços, contatos, papéis, contas, bloqueios), sem rastreamento.</summary>
    Task<Pessoa?> ObterAsync(int id, CancellationToken ct);

    Task<int> ContarClientesAtivosAsync(CancellationToken ct);

    /// <summary>Outra pessoa da mesma natureza com o mesmo documento principal (CPF, ou raiz do CNPJ).</summary>
    Task<PessoaIdentificacao?> BuscarPorDocumentoAsync(NaturezaPessoa natureza, string documento, int ignorarId, CancellationToken ct);

    /// <summary>Pessoas com o mesmo nome, ou algum telefone/e-mail igual (possíveis duplicadas).</summary>
    Task<List<PessoaIdentificacao>> BuscarSemelhantesAsync(
        int ignorarId, string nome, IReadOnlyCollection<string> contatos, CancellationToken ct);

    /// <summary>
    /// Inclui ou altera a pessoa inteira (filhos e contas sincronizados; bloqueios e relacionamentos não são tocados),
    /// com auditoria na mesma transação. Lança ConflitoDeEdicaoException se outro usuário gravou antes.
    /// </summary>
    Task<Pessoa> SalvarAsync(Pessoa pessoa, OrigemAlteracao origem, CancellationToken ct);
}
