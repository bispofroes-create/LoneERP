using Lone.Application.Municipios;
using Lone.Application.Seguranca;
using Lone.Contracts.Municipios;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Tabela de municípios do IBGE e conciliação dos textos antigos de município.</summary>
public class MunicipioRepositorio : ServicoDadosBase, IMunicipioRepositorio
{
    private const string ResolvidaAutomaticamente = "Conciliação automática";

    public MunicipioRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<MunicipioDto>> ListarDaUfAsync(string uf, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Municipios.AsNoTracking()
            .Where(m => m.Uf == uf && m.Ativo)
            .OrderBy(m => m.NomeBusca)
            .Select(m => new MunicipioDto { Id = m.Id, Nome = m.Nome, Uf = m.Uf })
            .ToListAsync(ct);
    }

    public async Task<Dictionary<int, Municipio>> ObterAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.ToList();
        return await db.Municipios.AsNoTracking().Where(m => lista.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
    }

    public async Task<List<Municipio>> ListarTodosAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Municipios.AsNoTracking().Where(m => m.Ativo).ToListAsync(ct);
    }

    public async Task<SituacaoMunicipios> ObterSituacaoAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return new SituacaoMunicipios
        {
            Quantidade = await db.Municipios.CountAsync(m => m.Ativo, ct),
            AtualizadaEm = await db.Municipios.MaxAsync(m => (DateTime?)m.AtualizadoEm, ct),
            PendenciasAbertas = await db.PendenciasMunicipio.CountAsync(p => p.ResolvidaEm == null, ct)
        };
    }

    public async Task<(int Incluidos, int Alterados, int Desativados)> SincronizarAsync(
        IReadOnlyCollection<Municipio> oficiais, DateTime agoraUtc, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Origem = OrigemAlteracao.Sistema;

        var atuais = await db.Municipios.ToDictionaryAsync(m => m.Id, ct);
        var codigosOficiais = oficiais.Select(o => o.Id).ToHashSet();
        int incluidos = 0, alterados = 0, desativados = 0;

        foreach (var oficial in oficiais)
        {
            if (!atuais.TryGetValue(oficial.Id, out var atual))
            {
                db.Municipios.Add(oficial);
                incluidos++;
                continue;
            }

            if (atual.Nome != oficial.Nome || !atual.Ativo || atual.Uf != oficial.Uf)
            {
                atual.Nome = oficial.Nome;
                atual.NomeBusca = oficial.NomeBusca;
                atual.Uf = oficial.Uf;
                atual.CodigoUf = oficial.CodigoUf;
                atual.Ativo = true;
                atual.AtualizadoEm = agoraUtc;
                alterados++;
            }
        }

        // Saiu da lista oficial: desativa (cadastros antigos continuam apontando para ele).
        foreach (var sumido in atuais.Values.Where(m => m.Ativo && !codigosOficiais.Contains(m.Id)))
        {
            sumido.Ativo = false;
            sumido.AtualizadoEm = agoraUtc;
            desativados++;
        }

        await db.SaveChangesAsync(ct);
        return (incluidos, alterados, desativados);
    }

    public async Task<(int Resolvidas, int Abertas)> ConciliarAsync(
        Func<PendenciaMunicipio, ResultadoConciliacao> resolver, DateTime agoraUtc, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Origem = OrigemAlteracao.Sistema;

        await CriarPendenciasDeEnderecosAsync(db, agoraUtc, ct);

        var abertas = await db.PendenciasMunicipio.Where(p => p.ResolvidaEm == null).ToListAsync(ct);
        if (abertas.Count == 0)
        {
            await db.SaveChangesAsync(ct);
            return (0, 0);
        }

        var pessoaIds = abertas.Select(p => p.PessoaId).Distinct().ToList();
        // Sem escopo: rotina do sistema (carga de municípios), sem usuário.
        var pessoas = await db.Pessoas.Include(p => p.Enderecos)
            .Where(p => pessoaIds.Contains(p.Id)).AsSplitQuery().ToDictionaryAsync(p => p.Id, ct);

        var resolvidas = 0;
        foreach (var pendencia in abertas)
        {
            if (!pessoas.TryGetValue(pendencia.PessoaId, out var pessoa)) continue;

            // Alguém já escolheu o município no cadastro (ou o endereço saiu): a pendência acabou.
            if (JaCorrigida(pessoa, pendencia))
            {
                Resolver(pendencia, null, agoraUtc, "Corrigida no cadastro");
                resolvidas++;
                continue;
            }

            var resultado = resolver(pendencia);
            if (resultado.Municipio is not { } municipio)
            {
                pendencia.Observacao = Cortar(resultado.Observacao, 200);
                continue;
            }

            Aplicar(pessoa, pendencia, municipio);
            Resolver(pendencia, municipio.Id, agoraUtc, ResolvidaAutomaticamente);
            resolvidas++;
        }

        await db.SaveChangesAsync(ct);
        return (resolvidas, abertas.Count - resolvidas);
    }

    /// <summary>Endereços no Brasil sem município (gravados antes da tabela do IBGE) viram pendência, uma vez.</summary>
    private static async Task CriarPendenciasDeEnderecosAsync(LoneDbContext db, DateTime agoraUtc, CancellationToken ct)
    {
        var semMunicipio = await db.PessoaEnderecos.AsNoTracking()
            .Where(e => e.MunicipioId == null && e.CodigoPais == PessoaEndereco.CodigoPaisBrasil &&
                        !db.PendenciasMunicipio.Any(p => p.RegistroId == e.Id && p.ResolvidaEm == null))
            .Select(e => new { e.Id, e.PessoaId, e.Cidade, e.Uf, e.CodigoMunicipioIbge })
            .ToListAsync(ct);

        foreach (var e in semMunicipio)
            db.PendenciasMunicipio.Add(new PendenciaMunicipio
            {
                Id = Domain.Comum.IdSequencial.Novo(),
                PessoaId = e.PessoaId,
                Origem = OrigemPendenciaMunicipio.Endereco,
                RegistroId = e.Id,
                TextoOriginal = Cortar(e.Cidade, 100) ?? string.Empty,
                UfOriginal = e.Uf is { Length: 2 } uf ? uf : null,
                CodigoIbgeOriginal = e.CodigoMunicipioIbge is { Length: 7 } codigo ? codigo : null,
                CriadaEm = agoraUtc
            });
    }

    private static bool JaCorrigida(Pessoa pessoa, PendenciaMunicipio pendencia) => pendencia.Origem switch
    {
        OrigemPendenciaMunicipio.Naturalidade => pessoa.NaturalidadeMunicipioId is not null || pessoa.Natureza != NaturezaPessoa.Fisica,
        _ => pessoa.Enderecos.FirstOrDefault(e => e.Id == pendencia.RegistroId) is not { } e || e.MunicipioId is not null || !e.EhBrasil
    };

    /// <summary>Grava o município no cadastro (a auditoria registra, com origem "sistema").</summary>
    private static void Aplicar(Pessoa pessoa, PendenciaMunicipio pendencia, Municipio municipio)
    {
        if (pendencia.Origem == OrigemPendenciaMunicipio.Naturalidade)
        {
            pessoa.NaturalidadeMunicipioId = municipio.Id;
            return;
        }

        var endereco = pessoa.Enderecos.First(e => e.Id == pendencia.RegistroId);
        endereco.MunicipioId = municipio.Id;
        endereco.Cidade = municipio.Nome;
        endereco.Uf = municipio.Uf;
        endereco.CodigoMunicipioIbge = municipio.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Resolver(PendenciaMunicipio pendencia, int? municipioId, DateTime agoraUtc, string por)
    {
        pendencia.MunicipioId = municipioId;
        pendencia.ResolvidaEm = agoraUtc;
        pendencia.ResolvidaPor = por;
        pendencia.Observacao = null;
    }

    private static string? Cortar(string? texto, int maximo) =>
        texto is { Length: var n } && n > maximo ? texto[..maximo] : texto;
}
