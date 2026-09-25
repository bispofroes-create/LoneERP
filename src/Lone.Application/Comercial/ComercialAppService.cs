using Lone.Application.Empresas;
using Lone.Application.Seguranca;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comercial;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Entidades;

namespace Lone.Application.Comercial;

/// <summary>Consultas de apoio aos dados comerciais.</summary>
public interface IComercialConsultas
{
    /// <summary>Pessoas ativas com o papel Vendedor ou Representante ativo, em ordem de nome.</summary>
    Task<List<PessoaOpcaoDto>> ListarVendedoresAsync(CancellationToken ct);

    /// <summary>Dos Ids informados, os que podem ser vendedores (ativos, com o papel Vendedor ou Representante).</summary>
    Task<HashSet<Guid>> VendedoresValidosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task<Dictionary<Guid, string>> NomesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}

public interface IComercialAppService
{
    Task<ComercialOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default);
}

public sealed class ComercialAppService : IComercialAppService
{
    private readonly IAutorizacao _autorizacao;
    private readonly IPerfilComercialAppService _perfis;
    private readonly ICondicaoPagamentoAppService _condicoes;
    private readonly ITipoCarteiraAppService _tipos;
    private readonly IComercialConsultas _consultas;
    private readonly IEmpresaConsultas _empresas;

    public ComercialAppService(IAutorizacao autorizacao, IPerfilComercialAppService perfis, ICondicaoPagamentoAppService condicoes,
                               ITipoCarteiraAppService tipos, IComercialConsultas consultas, IEmpresaConsultas empresas)
    {
        _autorizacao = autorizacao;
        _perfis = perfis;
        _condicoes = condicoes;
        _tipos = tipos;
        _consultas = consultas;
        _empresas = empresas;
    }

    public async Task<ComercialOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return new ComercialOpcoesDto
        {
            Perfis = await _perfis.ListarAsync(incluirInativos: true, ct),
            Condicoes = await _condicoes.ListarAsync(incluirInativos: true, ct),
            TiposCarteira = await _tipos.ListarAsync(incluirInativos: true, ct),
            Vendedores = await _consultas.ListarVendedoresAsync(ct),
            Empresas = await _empresas.ListarEmpresasAsync(ct)
        };
    }
}

/// <summary>Liga os dados comerciais da ficha aos cadastros: confere ao gravar e preenche nomes ao ler.</summary>
public sealed class ReferenciasComercial
{
    private readonly IPerfilComercialRepositorio _perfis;
    private readonly ICondicaoPagamentoRepositorio _condicoes;
    private readonly ITipoCarteiraRepositorio _tipos;
    private readonly IComercialConsultas _consultas;

    public ReferenciasComercial(IPerfilComercialRepositorio perfis, ICondicaoPagamentoRepositorio condicoes,
                                ITipoCarteiraRepositorio tipos, IComercialConsultas consultas)
    {
        _perfis = perfis;
        _condicoes = condicoes;
        _tipos = tipos;
        _consultas = consultas;
    }

    public async Task<Dictionary<Guid, TipoCarteira>> TiposAsync(CancellationToken ct) =>
        (await _tipos.ListarAsync(ct)).ToDictionary(t => t.Id);

    public async Task<List<string>> ValidarAsync(Pessoa dados, Pessoa? anterior, IReadOnlyDictionary<Guid, TipoCarteira> tipos, CancellationToken ct)
    {
        var condicoes = dados.ContasCliente.Select(c => c.CondicaoPagamentoId)
            .Concat(dados.ExcecoesComerciais.Select(e => e.CondicaoPagamentoId)).OfType<Guid>().Distinct().ToList();
        var referencias = new ComercialParaConferir(
            await _perfis.ObterVariosAsync(dados.ContasCliente.Select(c => c.PerfilComercialId).OfType<Guid>().Distinct().ToList(), ct),
            await _condicoes.ObterVariosAsync(condicoes, ct),
            tipos,
            await _consultas.VendedoresValidosAsync(dados.Carteira.Select(c => c.VendedorId).Distinct().ToList(), ct));
        return RegrasComercial.ValidarReferencias(dados, anterior, referencias);
    }

    public async Task PreencherNomesAsync(IReadOnlyList<CarteiraDto> carteira, CancellationToken ct)
    {
        if (carteira.Count == 0) return;
        var nomes = await _consultas.NomesAsync(carteira.Select(c => c.VendedorId).Distinct().ToList(), ct);
        foreach (var c in carteira) c.Vendedor = nomes.GetValueOrDefault(c.VendedorId);
    }
}
