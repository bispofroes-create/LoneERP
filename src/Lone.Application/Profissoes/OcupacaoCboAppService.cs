using Lone.Application.Seguranca;
using Lone.Contracts.Profissoes;
using Lone.Contracts.Seguranca;
using Lone.Domain.Profissoes;
using Lone.Domain.Validacao;

namespace Lone.Application.Profissoes;

public interface IOcupacaoCboAppService
{
    Task<List<OcupacaoCboDto>> ListarAsync(CancellationToken ct = default);
    Task<SituacaoCbo> ObterSituacaoAsync(CancellationToken ct = default);
    Task<ResultadoImportacaoCbo> ImportarAsync(ImportarCboRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>
/// Tabela oficial da CBO. Não há serviço público estável para baixar (como o do IBGE): o arquivo oficial
/// "CBO2002 - Ocupacao.csv" é baixado no site do Ministério do Trabalho e importado aqui.
/// </summary>
public sealed class OcupacaoCboAppService : IOcupacaoCboAppService
{
    /// <summary>A CBO 2002 tem cerca de 2.600 ocupações: um arquivo bem menor é arquivo errado ou cortado.</summary>
    public const int MinimoOcupacoes = 2000;

    private readonly IOcupacaoCboRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;
    private readonly TimeProvider _relogio;

    public OcupacaoCboAppService(IOcupacaoCboRepositorio repositorio, IAutorizacao autorizacao, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
        _relogio = relogio;
    }

    public async Task<List<OcupacaoCboDto>> ListarAsync(CancellationToken ct = default)
    {
        if (!_autorizacao.Possui(Permissoes.Cadastros.Profissoes)) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return (await _repositorio.ListarAtivasAsync(ct))
            .Select(o => new OcupacaoCboDto { Codigo = o.Id, Titulo = o.Titulo })
            .ToList();
    }

    public async Task<SituacaoCbo> ObterSituacaoAsync(CancellationToken ct = default)
    {
        var (quantidade, atualizadaEm) = await _repositorio.ObterSituacaoAsync(ct);
        return new SituacaoCbo { Quantidade = quantidade, AtualizadaEm = atualizadaEm };
    }

    public async Task<ResultadoImportacaoCbo> ImportarAsync(ImportarCboRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.TabelasOficiais);

        if (requisicao.Arquivo is not { Length: > 0 })
            throw new ValidacaoException(["Escolha o arquivo \"CBO2002 - Ocupacao.csv\"."]);

        var lido = LeitorCbo.Ler(requisicao.Arquivo);
        if (lido.Ocupacoes.Count < MinimoOcupacoes)
            throw new ValidacaoException([
                $"O arquivo tem só {lido.Ocupacoes.Count} ocupações válidas (a CBO tem cerca de 2.600). " +
                "Use o arquivo \"CBO2002 - Ocupacao.csv\" do site do Ministério do Trabalho. Nada foi alterado."]);

        var (incluidas, alteradas, desativadas) =
            await _repositorio.SincronizarAsync(lido.Ocupacoes, _relogio.GetUtcNow().UtcDateTime, ct);
        return new ResultadoImportacaoCbo
        {
            Lidas = lido.Ocupacoes.Count,
            Incluidas = incluidas,
            Alteradas = alteradas,
            Desativadas = desativadas,
            LinhasIgnoradas = lido.LinhasIgnoradas
        };
    }
}
