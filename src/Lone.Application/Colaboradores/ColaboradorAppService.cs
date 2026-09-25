using Lone.Application.Empresas;
using Lone.Application.Seguranca;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Seguranca;

namespace Lone.Application.Colaboradores;

/// <summary>Consultas de apoio aos dados de colaborador.</summary>
public interface IColaboradorConsultas
{
    /// <summary>Pessoas físicas ativas com vínculo ativo na data (candidatas a gestor), em ordem de nome.</summary>
    Task<List<PessoaOpcaoDto>> ListarGestoresAsync(DateOnly hoje, CancellationToken ct);

    /// <summary>Nome de exibição de cada pessoa (empresas e gestores das lotações).</summary>
    Task<Dictionary<Guid, string>> NomesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Dos Ids informados, os que são pessoas físicas ativas (podem ser gestores).</summary>
    Task<HashSet<Guid>> PessoasFisicasAtivasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}

public interface IColaboradorAppService
{
    Task<ColaboradorOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default);
}

/// <summary>Opções da aba "Colaborador" numa chamada só: empresas do grupo, gestores e a estrutura organizacional.</summary>
public sealed class ColaboradorAppService : IColaboradorAppService
{
    private readonly IAutorizacao _autorizacao;
    private readonly IEmpresaConsultas _empresas;
    private readonly IColaboradorConsultas _consultas;
    private readonly ICargoAppService _cargos;
    private readonly IDepartamentoAppService _departamentos;
    private readonly ISetorAppService _setores;
    private readonly ICentroCustoAppService _centrosCusto;
    private readonly TimeProvider _relogio;

    public ColaboradorAppService(IAutorizacao autorizacao, IEmpresaConsultas empresas, IColaboradorConsultas consultas,
                                 ICargoAppService cargos, IDepartamentoAppService departamentos, ISetorAppService setores,
                                 ICentroCustoAppService centrosCusto, TimeProvider relogio)
    {
        _autorizacao = autorizacao;
        _empresas = empresas;
        _consultas = consultas;
        _cargos = cargos;
        _departamentos = departamentos;
        _setores = setores;
        _centrosCusto = centrosCusto;
        _relogio = relogio;
    }

    public async Task<ColaboradorOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Colaborador);
        return new ColaboradorOpcoesDto
        {
            Empresas = await _empresas.ListarEmpresasAsync(ct),
            Gestores = await _consultas.ListarGestoresAsync(DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime), ct),
            Cargos = await _cargos.ListarAsync(incluirInativos: true, ct),
            Departamentos = await _departamentos.ListarAsync(incluirInativos: true, ct),
            Setores = await _setores.ListarAsync(incluirInativos: true, ct),
            CentrosCusto = await _centrosCusto.ListarAsync(incluirInativos: true, ct)
        };
    }
}
