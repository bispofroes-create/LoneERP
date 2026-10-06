using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Integracoes.Cep;

/// <summary>
/// Dados da reconferência em lote (F6). Cada operação abre o próprio contexto e é curta: nenhuma chamada externa fica
/// dentro de transação. Gravar mexe só em CepSituacao, CepFonte e CepConferidoEm, numa instrução cuja condição repete os
/// valores lidos: se o endereço mudou no meio (outra pessoa salvou a ficha), nenhuma linha é alterada.
/// </summary>
public sealed class ReconferenciaCepSql : IReconferenciaCepRepositorio, IManutencaoConsultasCep, IRegistroExecucoesReconferenciaCep
{
    private readonly IDbContextFactory<LoneDbContext> _fabrica;

    public ReconferenciaCepSql(IDbContextFactory<LoneDbContext> fabrica) => _fabrica = fabrica;

    /// <summary>Endereços elegíveis: ativos, no Brasil, não consolidados, de cadastro não arquivado.</summary>
    private static IQueryable<PessoaEndereco> Elegiveis(LoneDbContext db) =>
        db.PessoaEnderecos.AsNoTracking()
            .Where(e => e.Ativo && e.CodigoPais == PessoaEndereco.CodigoPaisBrasil && e.MescladoEmId == null)
            // Sem escopo: a reconferência é de manutenção da base inteira; o serviço só a libera para alcance "Tudo".
            .Where(e => db.Pessoas.Any(p => p.Id == e.PessoaId && p.Situacao != SituacaoPessoa.Arquivado));

    public async Task<SelecaoReconferenciaCep> SelecionarAsync(CriterioReconferenciaCep c, DateTime agora, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        var antigo = agora - PoliticaReconferenciaCep.IdadeParaReconferir;
        var q = Elegiveis(db).Where(e =>
            (c.NaoConferidos && e.CepSituacao == CepSituacao.NaoConferido)
            || (c.Divergentes && e.CepSituacao == CepSituacao.Divergente)
            || (c.NaoEncontrados && e.CepSituacao == CepSituacao.NaoEncontrado)
            || (c.ConferidosAntigos && e.CepSituacao == CepSituacao.Conferido && (e.CepConferidoEm == null || e.CepConferidoEm < antigo)));
        if (c.Uf is { } uf) q = q.Where(e => e.Uf == uf);
        if (c.MunicipioId is { } municipio) q = q.Where(e => e.MunicipioId == municipio);

        // Sem CEP de 8 dígitos não há o que conferir: fica de fora e é contado à parte.
        var semCep = await q.CountAsync(e => e.Cep == null || e.Cep.Length != 8, ct);
        var validos = q.Where(e => e.Cep != null && e.Cep.Length == 8);
        var total = await validos.CountAsync(ct);
        // Ordem previsível: nunca conferidos / mais antigos primeiro; desempate pelo Id.
        var ids = await validos.OrderBy(e => e.CepConferidoEm == null ? 0 : 1).ThenBy(e => e.CepConferidoEm).ThenBy(e => e.Id)
            .Take(Math.Max(0, c.Limite)).Select(e => e.Id).ToListAsync(ct);
        return new SelecaoReconferenciaCep(total, ids, semCep);
    }

    public async Task<IReadOnlyList<EnderecoReconferivel>> ObterAsync(IReadOnlyCollection<Guid> enderecos, CancellationToken ct = default)
    {
        if (enderecos.Count == 0) return [];
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        var ids = enderecos.ToList();
        return await db.PessoaEnderecos.AsNoTracking().Where(e => ids.Contains(e.Id))
            // Sem escopo: mesma regra da seleção (só alcance "Tudo" chega aqui); lê só código e nome para o resultado.
            .Join(db.Pessoas.AsNoTracking(), e => e.PessoaId, p => p.Id, (e, p) => new EnderecoReconferivel(
                e.Id, e.PessoaId, p.Codigo, p.Nome, e.Cep, e.Logradouro, e.Numero, e.Bairro, e.Cidade, e.Uf, e.MunicipioId,
                e.CodigoMunicipioIbge, e.Ativo && e.MescladoEmId == null && p.Situacao != SituacaoPessoa.Arquivado,
                e.CodigoPais == PessoaEndereco.CodigoPaisBrasil, e.CepSituacao))
            .ToListAsync(ct);
    }

    public async Task<bool> GravarAsync(EnderecoReconferivel l, ConferenciaCepRealizada c, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        // Uma instrução: a condição repete os dados conferidos como foram lidos. Mudou qualquer um, 0 linhas.
        var alterados = await db.PessoaEnderecos
            .Where(e => e.Id == l.EnderecoId && e.Ativo && e.CodigoPais == PessoaEndereco.CodigoPaisBrasil
                        && e.Cep == l.Cep && e.Logradouro == l.Logradouro && e.Numero == l.Numero && e.Bairro == l.Bairro
                        && e.Cidade == l.Cidade && e.Uf == l.Uf && e.MunicipioId == l.MunicipioId
                        && e.CodigoMunicipioIbge == l.CodigoMunicipioIbge)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.CepSituacao, c.Situacao).SetProperty(e => e.CepFonte, c.Fonte)
                .SetProperty(e => e.CepConferidoEm, c.ConferidoEm), ct);
        return alterados == 1;
    }

    /// <summary>
    /// G: um evento por execução na Auditoria (só inclusão; a Auditoria é protegida contra alteração). Entidade e raiz
    /// "ReconferenciaCep" com o id da execução; repetir a mesma execução não duplica.
    /// </summary>
    public async Task<bool> RegistrarAsync(ExecucaoReconferenciaCep e, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        var id = e.ExecucaoId.ToString("N");
        if (await db.Auditoria.AnyAsync(a => a.Entidade == EventoReconferenciaCep.Entidade && a.RegistroId == id, ct)) return false;
        db.Auditoria.Add(new RegistroAuditoria
        {
            DataHora = e.Em, Usuario = e.Usuario.Length <= 100 ? e.Usuario : e.Usuario[..100], OperacaoId = Guid.NewGuid(),
            Origem = OrigemAlteracao.Usuario, Entidade = EventoReconferenciaCep.Entidade, RegistroId = id,
            RaizEntidade = EventoReconferenciaCep.Entidade, RaizId = e.ExecucaoId, Acao = AcaoAuditoria.Evento,
            Descricao = e.Descricao.Length <= 500 ? e.Descricao : e.Descricao[..500]
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> LimparAsync(DateTime limite, int tamanhoLote, CancellationToken ct = default)
    {
        var removidos = 0;
        while (!ct.IsCancellationRequested)
        {
            await using var db = await _fabrica.CreateDbContextAsync(ct);
            var lote = await db.ConsultasCep.AsNoTracking().Where(h => h.OcorridoEm < limite).OrderBy(h => h.OcorridoEm)
                .Take(Math.Max(1, tamanhoLote)).Select(h => h.Id).ToListAsync(ct);
            if (lote.Count == 0) break;
            removidos += await db.ConsultasCep.Where(h => lote.Contains(h.Id)).ExecuteDeleteAsync(ct);
        }
        return removidos;
    }
}
