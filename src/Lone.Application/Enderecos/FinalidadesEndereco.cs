using Lone.Application.Seguranca;
using Lone.Contracts.Enderecos;
using Lone.Contracts.Seguranca;
using Lone.Domain.Entidades;

namespace Lone.Application.Enderecos;

/// <summary>Cadastro de finalidades de endereço (leitura; o CRUD para o administrador vem num passo seguinte).</summary>
public interface IFinalidadeEnderecoRepositorio
{
    /// <summary>Todas (ativas e desativadas), na ordem do cadastro, sem rastreamento.</summary>
    Task<List<FinalidadeEnderecoCadastro>> ListarAsync(CancellationToken ct);
}

public interface IFinalidadeEnderecoAppService
{
    Task<List<FinalidadeEnderecoDto>> ListarAsync(CancellationToken ct = default);
}

/// <summary>
/// Finalidades para a ficha: vêm todas (a desativada ainda aparece nos endereços que a têm; a ficha só oferece as
/// ativas para associação nova).
/// </summary>
public sealed class FinalidadeEnderecoAppService : IFinalidadeEnderecoAppService
{
    private readonly IFinalidadeEnderecoRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public FinalidadeEnderecoAppService(IFinalidadeEnderecoRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<FinalidadeEnderecoDto>> ListarAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return (await _repositorio.ListarAsync(ct)).OrderBy(f => f.Ordem).ThenBy(f => f.Nome)
            .Select(f => new FinalidadeEnderecoDto { Id = f.Id, Codigo = f.Codigo, Nome = f.Nome, Ordem = f.Ordem, Ativo = f.Ativo })
            .ToList();
    }
}

/// <summary>Rotina que aponta pessoas com endereços físicos duplicados (só leitura; consolidação é na ficha).</summary>
public interface IEnderecosDuplicadosConsulta
{
    /// <summary>Examina até <paramref name="examinar"/> pessoas com 2+ endereços ativos a partir de <paramref name="apos"/>.</summary>
    Task<PaginaEnderecosDuplicados> ListarAsync(Guid? apos, int limite, int examinar, CancellationToken ct);
}

public interface IEnderecosDuplicadosAppService
{
    Task<PaginaEnderecosDuplicados> ListarAsync(Guid? apos, int limite, CancellationToken ct = default);
}

public sealed class EnderecosDuplicadosAppService : IEnderecosDuplicadosAppService
{
    public const int LimiteMaximo = 200;
    public const int PessoasPorChamada = 2_000;

    private readonly IEnderecosDuplicadosConsulta _consulta;
    private readonly IAutorizacao _autorizacao;

    public EnderecosDuplicadosAppService(IEnderecosDuplicadosConsulta consulta, IAutorizacao autorizacao)
    {
        _consulta = consulta;
        _autorizacao = autorizacao;
    }

    public Task<PaginaEnderecosDuplicados> ListarAsync(Guid? apos, int limite, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return _consulta.ListarAsync(apos, Math.Clamp(limite, 1, LimiteMaximo), PessoasPorChamada, ct);
    }
}
