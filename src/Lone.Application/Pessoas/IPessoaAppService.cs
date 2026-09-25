using Lone.Domain.Enums;
using Lone.Contracts.Auditoria;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Pessoas;

namespace Lone.Application.Pessoas;

/// <summary>Casos de uso do cadastro de pessoas. É o único ponto que a API usa para pessoas.</summary>
public interface IPessoaAppService
{
    Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct = default);
    Task<PessoaDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<int> ContarClientesAtivosAsync(CancellationToken ct = default);

    /// <summary>Pessoas físicas ativas por faixa de idade (todas, ou só as de um papel).</summary>
    Task<List<QuantidadePorFaixaEtaria>> ListarFaixasEtariasAsync(TipoPapel? papel, CancellationToken ct = default);

    /// <summary>
    /// Inclui ou altera (Id desconhecido = inclusão, inclusive quando o aparelho gerou o Id).
    /// Lança ValidacaoException (regra), AcessoNegadoException (permissão) ou ConflitoDeEdicaoException.
    /// </summary>
    Task<ResultadoSalvarPessoa> SalvarAsync(PessoaDto pessoa, CancellationToken ct = default);

    /// <summary>Tira o cadastro de uso (sem apagar nada). Exige a permissão de inativar; registra o evento.</summary>
    Task<PessoaDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);

    /// <summary>Volta a usar um cadastro inativo.</summary>
    Task<PessoaDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);

    Task<List<RegistroHistorico>> ListarHistoricoAsync(Guid pessoaId, CancellationToken ct = default);
}
