using Lone.Application.Seguranca;
using Lone.Contracts.Municipios;
using Lone.Contracts.Seguranca;
using Lone.Domain.Validacao;

namespace Lone.Application.Municipios;

public interface IMunicipioAppService
{
    Task<List<MunicipioDto>> ListarDaUfAsync(string uf, CancellationToken ct = default);
    Task<SituacaoMunicipios> ObterSituacaoAsync(CancellationToken ct = default);
    Task<ResultadoAtualizacaoMunicipios> AtualizarAsync(CancellationToken ct = default);
}

/// <summary>Consulta da tabela (qualquer usuário autenticado) e atualização pelo IBGE (permissão própria).</summary>
public sealed class MunicipioAppService : IMunicipioAppService
{
    private readonly IMunicipioRepositorio _repositorio;
    private readonly ServicoMunicipios _servico;
    private readonly IAutorizacao _autorizacao;

    public MunicipioAppService(IMunicipioRepositorio repositorio, ServicoMunicipios servico, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _servico = servico;
        _autorizacao = autorizacao;
    }

    public Task<List<MunicipioDto>> ListarDaUfAsync(string uf, CancellationToken ct = default)
    {
        var sigla = (uf ?? string.Empty).Trim().ToUpperInvariant();
        if (!Ufs.Valida(sigla))
            throw new ValidacaoException(["UF inválida."]);
        return _repositorio.ListarDaUfAsync(sigla, ct);
    }

    public Task<SituacaoMunicipios> ObterSituacaoAsync(CancellationToken ct = default) => _repositorio.ObterSituacaoAsync(ct);

    public Task<ResultadoAtualizacaoMunicipios> AtualizarAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.TabelasOficiais);
        return _servico.AtualizarAsync(ct);
    }
}
