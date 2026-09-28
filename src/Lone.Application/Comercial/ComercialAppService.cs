using Lone.Application.Empresas;
using Lone.Application.Papeis;
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
    /// <summary>
    /// Pessoas ativas com alguma das classificações informadas ativa, em ordem de nome, cada uma com as dessas
    /// classificações que tem.
    /// </summary>
    Task<List<AtendenteOpcaoDto>> ListarAtendentesAsync(IReadOnlyCollection<Guid> classificacoes, CancellationToken ct);

    /// <summary>Dos Ids informados, as pessoas ativas, com nome e classificações ativas (para conferir a carteira).</summary>
    Task<Dictionary<Guid, PessoaElegivel>> PessoasElegiveisAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

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
    private readonly IPapelRepositorio _classificacoes;
    private readonly IParametrosComerciaisRepositorio _parametros;
    private readonly ICoberturaAppService _coberturas;

    public ComercialAppService(IAutorizacao autorizacao, IPerfilComercialAppService perfis, ICondicaoPagamentoAppService condicoes,
                               ITipoCarteiraAppService tipos, IComercialConsultas consultas, IEmpresaConsultas empresas,
                               IPapelRepositorio classificacoes, IParametrosComerciaisRepositorio parametros, ICoberturaAppService coberturas)
    {
        _parametros = parametros;
        _coberturas = coberturas;
        _autorizacao = autorizacao;
        _perfis = perfis;
        _condicoes = condicoes;
        _tipos = tipos;
        _consultas = consultas;
        _empresas = empresas;
        _classificacoes = classificacoes;
    }

    public async Task<ComercialOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var tipos = await _tipos.ListarAsync(incluirInativos: true, ct);
        return new ComercialOpcoesDto
        {
            Perfis = await _perfis.ListarAsync(incluirInativos: true, ct),
            Condicoes = await _condicoes.ListarAsync(incluirInativos: true, ct),
            TiposCarteira = tipos,
            Atendentes = await _consultas.ListarAtendentesAsync([.. tipos.Where(t => t.Ativo).SelectMany(t => t.Classificacoes).Distinct()], ct),
            Classificacoes = [.. (await _classificacoes.ListarAsync(incluirInativos: true, ct)).Select(p => new ClassificacaoOpcaoDto(p.Id, p.Nome, p.Ativo))],
            Empresas = await _empresas.ListarEmpresasAsync(ct),
            DiasAvisoFimVinculo = (await _parametros.ObterAsync(ct)).DiasAvisoFimVinculo,
            Coberturas = await _coberturas.AvisosAsync(ct)
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
    private readonly IPapelRepositorio _classificacoes;

    public ReferenciasComercial(IPerfilComercialRepositorio perfis, ICondicaoPagamentoRepositorio condicoes,
                                ITipoCarteiraRepositorio tipos, IComercialConsultas consultas, IPapelRepositorio classificacoes)
    {
        _perfis = perfis;
        _condicoes = condicoes;
        _tipos = tipos;
        _consultas = consultas;
        _classificacoes = classificacoes;
    }

    public async Task<Dictionary<Guid, TipoCarteira>> TiposAsync(CancellationToken ct) =>
        (await _tipos.ListarAsync(ct)).ToDictionary(t => t.Id);

    public async Task<List<string>> ValidarAsync(Pessoa dados, Pessoa? anterior, IReadOnlyDictionary<Guid, TipoCarteira> tipos, CancellationToken ct)
    {
        var condicoes = dados.ContasCliente.Select(c => c.CondicaoPagamentoId)
            .Concat(dados.ExcecoesComerciais.Select(e => e.CondicaoPagamentoId))
            .Concat(dados.ContasFornecedor.Select(f => f.CondicaoPagamentoId)).OfType<Guid>().Distinct().ToList();
        var referencias = new ComercialParaConferir(
            await _perfis.ObterVariosAsync(dados.ContasCliente.Select(c => c.PerfilComercialId).OfType<Guid>().Distinct().ToList(), ct),
            await _condicoes.ObterVariosAsync(condicoes, ct),
            tipos,
            await _consultas.PessoasElegiveisAsync(dados.Carteira.Select(c => c.VendedorId).Where(id => id != Guid.Empty).Distinct().ToList(), ct),
            (await _classificacoes.ListarAsync(incluirInativos: true, ct)).ToDictionary(p => p.Id, p => p.Nome));
        return RegrasComercial.ValidarReferencias(dados, anterior, referencias);
    }

    /// <summary>
    /// As classificações ativas da pessoa, se ela está ativa (nulo = não existe ou inativa): quais papéis comerciais ela pode
    /// ocupar ("Quem pode ser"). Usado no F4 da Fase 2a-2.
    /// </summary>
    public async Task<IReadOnlySet<Guid>?> ClassificacoesAsync(Guid pessoaId, CancellationToken ct) =>
        (await _consultas.PessoasElegiveisAsync([pessoaId], ct)).GetValueOrDefault(pessoaId)?.Classificacoes;

    /// <summary>Nomes dos vendedores (para as frases do histórico da carteira).</summary>
    public Task<Dictionary<Guid, string>> NomesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => _consultas.NomesAsync(ids, ct);

    public async Task PreencherNomesAsync(IReadOnlyList<CarteiraDto> carteira, CancellationToken ct)
    {
        if (carteira.Count == 0) return;
        var nomes = await _consultas.NomesAsync(carteira.Select(c => c.VendedorId).Distinct().ToList(), ct);
        foreach (var c in carteira) c.Vendedor = nomes.GetValueOrDefault(c.VendedorId);
    }
}
