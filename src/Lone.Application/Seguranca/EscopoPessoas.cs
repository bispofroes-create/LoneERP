using Lone.Application.Comercial;
using Lone.Application.Metas;
using Lone.Domain.Comercial;
using Lone.Domain.Enums;

namespace Lone.Application.Seguranca;

/// <summary>Alcance do usuário da requisição em Pessoas e no Comercial (Fase 2a-2). Na API: o usuário do token.</summary>
public interface IAlcanceDoUsuario
{
    /// <summary>O maior alcance entre os perfis que valem na empresa ativa (administrador: Tudo).</summary>
    AlcanceComercial Alcance { get; }

    /// <summary>A pessoa do cadastro ligada ao usuário (nula = não ligado).</summary>
    Guid? PessoaId { get; }
}

/// <summary>
/// Cadastro fora do alcance do usuário. A API responde exatamente como a um cadastro que não existe (404, mesma
/// mensagem), para não revelar que ele existe.
/// </summary>
public sealed class ForaDoEscopoException : Exception
{
    public const string Mensagem = "Este cadastro não existe ou está fora do seu alcance.";

    public ForaDoEscopoException() : base(Mensagem) { }
}

/// <summary>Um cadastro diante do escopo do usuário.</summary>
public enum SituacaoNoEscopo
{
    NaoExiste,
    NoEscopo,
    ForaDoEscopo
}

/// <summary>Conferências do escopo que precisam do banco (implementadas na Infraestrutura, com a mesma regra da lista).</summary>
public interface IPessoasNoEscopo
{
    /// <summary>Se o cadastro existe e se está no escopo (escopo Tudo: nunca "fora").</summary>
    Task<SituacaoNoEscopo> SituacaoAsync(Guid pessoaId, EscopoResolvido escopo, CancellationToken ct);
}

/// <summary>
/// O escopo de Pessoas desta requisição (Fase 2a-2), num ponto só: as consultas de Pessoas aplicam
/// <see cref="ObterAsync"/> e as rotas com o id de um cadastro chamam <see cref="ExigirAsync"/>. Um por requisição
/// (scoped): resolve uma vez e guarda.
/// </summary>
public interface IEscopoPessoas
{
    Task<EscopoResolvido> ObterAsync(CancellationToken ct = default);

    /// <summary>
    /// Lança <see cref="ForaDoEscopoException"/> se o cadastro está fora do alcance. Com alcance restrito, um id que não
    /// existe recebe a mesma recusa (senão a diferença de resposta revelaria quais ids existem), menos quando
    /// <paramref name="podeSerNovo"/> (a gravação de um cadastro novo, cujo id vem do aparelho).
    /// </summary>
    Task ExigirAsync(Guid pessoaId, bool podeSerNovo = false, CancellationToken ct = default);
}

public sealed class EscopoPessoas : IEscopoPessoas
{
    private readonly IAlcanceDoUsuario _alcance;
    private readonly IEmpresaAtual _empresa;
    private readonly IEquipeRepositorio _equipes;
    private readonly ICoberturaRepositorio _coberturas;
    private readonly IPessoasNoEscopo _pessoas;
    private readonly TimeProvider _relogio;
    private EscopoResolvido? _resolvido;

    public EscopoPessoas(IAlcanceDoUsuario alcance, IEmpresaAtual empresa, IEquipeRepositorio equipes, ICoberturaRepositorio coberturas,
                         IPessoasNoEscopo pessoas, TimeProvider relogio)
    {
        _alcance = alcance;
        _empresa = empresa;
        _equipes = equipes;
        _coberturas = coberturas;
        _pessoas = pessoas;
        _relogio = relogio;
    }

    public async Task<EscopoResolvido> ObterAsync(CancellationToken ct = default)
    {
        if (_resolvido is not null) return _resolvido;
        var hoje = DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);
        var alcance = _alcance.Alcance;

        // Tudo e Nenhum não dependem de equipes nem de coberturas: nenhuma consulta a mais para quem vê a base inteira.
        if (alcance is (AlcanceComercial.MinhaCarteira or AlcanceComercial.MinhaEquipe) && _alcance.PessoaId is not null)
        {
            // As equipes entram nos dois níveis: em "Minha carteira" a cobertura pode ter sido dada à equipe da pessoa.
            var equipes = await _equipes.ListarAsync(ct);
            var coberturas = await _coberturas.ListarAsync(hoje, incluirEncerradas: false, ct);
            _resolvido = RegrasEscopo.Resolver(alcance, _alcance.PessoaId, _empresa.EmpresaId, equipes, coberturas, hoje);
        }
        else
            _resolvido = RegrasEscopo.Resolver(alcance, _alcance.PessoaId, _empresa.EmpresaId, [], [], hoje);
        return _resolvido;
    }

    public async Task ExigirAsync(Guid pessoaId, bool podeSerNovo = false, CancellationToken ct = default)
    {
        var escopo = await ObterAsync(ct);
        if (escopo.Tudo) return;
        var situacao = await _pessoas.SituacaoAsync(pessoaId, escopo, ct);
        if (situacao == SituacaoNoEscopo.ForaDoEscopo || (situacao == SituacaoNoEscopo.NaoExiste && !podeSerNovo))
            throw new ForaDoEscopoException();
    }
}
