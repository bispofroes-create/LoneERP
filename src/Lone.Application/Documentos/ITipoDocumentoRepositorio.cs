using Lone.Application.Pessoas;
using Lone.Domain.Entidades;

namespace Lone.Application.Documentos;

/// <summary>Persistência dos tipos de documento. Nada é apagado: tipos são desativados.</summary>
public interface ITipoDocumentoRepositorio
{
    /// <summary>Na ordem do cadastro, sem rastreamento.</summary>
    Task<List<TipoDocumentoCadastro>> ListarAsync(bool incluirInativos, CancellationToken ct);

    Task<TipoDocumentoCadastro?> ObterAsync(Guid id, CancellationToken ct);

    Task<Dictionary<Guid, TipoDocumentoCadastro>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Nome já usado por outro tipo (sem diferenciar maiúsculas nem acentos).</summary>
    Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct);

    /// <summary>Quantos documentos ativos usam cada tipo (ou só o informado).</summary>
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteTipoId, CancellationToken ct);

    Task<int> ProximaOrdemAsync(CancellationToken ct);

    /// <summary>
    /// Grava o tipo. P1-8B: com <paramref name="recalcularChaves"/>, na MESMA transação, refaz a chave de unicidade de todos
    /// os documentos do tipo pelo modo novo — antes, se o modo bloqueia, confere os ativos e recusa (ValidacaoException,
    /// sem gravar nada) quando houver número repetido; o índice único é a garantia final. Nenhum dado é corrigido.
    /// </summary>
    Task SalvarAsync(TipoDocumentoCadastro tipo, bool novo, bool recalcularChaves, CancellationToken ct);

    /// <summary>
    /// P1-8B: documentos ATIVOS de outras pessoas com o mesmo tipo e o mesmo número comparável (para o aviso ou o erro de
    /// repetido). Devolve qualquer pessoa; o serviço decide o que pode ser mostrado (alcance).
    /// </summary>
    Task<List<DocumentoEmOutraPessoa>> BuscarIguaisEmOutrasPessoasAsync(Guid pessoaId, IReadOnlyCollection<Guid> tipos,
                                                                        IReadOnlyCollection<string> numerosComparaveis, CancellationToken ct);
}

/// <summary>Documento ativo de outra pessoa com o mesmo tipo e número comparável.</summary>
public sealed record DocumentoEmOutraPessoa(Guid TipoDocumentoId, string NumeroNormalizado, string? Uf, PessoaIdentificacao Pessoa);
