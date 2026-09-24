using Lone.Aplicacao.Auditoria;
using Lone.Core.Entidades;

namespace Lone.Aplicacao.Pessoas;

/// <summary>Casos de uso do cadastro de pessoas. É o único ponto que a tela usa.</summary>
public interface IPessoaAppService
{
    Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct = default);
    Task<Pessoa?> ObterAsync(int id, CancellationToken ct = default);
    Task<int> ContarClientesAtivosAsync(CancellationToken ct = default);

    /// <summary>
    /// Inclui (Id = 0) ou altera. Lança ValidacaoException (regra), AcessoNegadoException (permissão)
    /// ou ConflitoDeEdicaoException (outro usuário gravou antes).
    /// </summary>
    Task<ResultadoSalvar> SalvarAsync(Pessoa pessoa, CancellationToken ct = default);

    Task<List<RegistroHistorico>> ListarHistoricoAsync(int pessoaId, CancellationToken ct = default);
}
