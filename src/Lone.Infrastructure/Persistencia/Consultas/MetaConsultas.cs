using Lone.Application.Metas;
using Lone.Application.Seguranca;
using Lone.Contracts.Metas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Papeis;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

/// <summary>Nomes e opções dos participantes das metas, cada nível no seu cadastro.</summary>
public class MetaConsultas : ServicoDadosBase, IMetaConsultas
{
    public MetaConsultas(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<Dictionary<(NivelParticipante, Guid), string>> NomesAsync(
        IEnumerable<(NivelParticipante Nivel, Guid Id)> participantes, CancellationToken ct)
    {
        var porNivel = participantes.GroupBy(p => p.Nivel).ToDictionary(g => g.Key, g => g.Select(p => p.Id).Distinct().ToList());
        var resultado = new Dictionary<(NivelParticipante, Guid), string>();
        if (porNivel.Count == 0) return resultado;
        await using var db = await AbrirAsync(ct);

        List<Guid> Ids(NivelParticipante n) => porNivel.GetValueOrDefault(n) ?? [];

        var pessoas = Ids(NivelParticipante.Empresa).Concat(Ids(NivelParticipante.Colaborador)).Distinct().ToList();
        if (pessoas.Count > 0)
        {
            var nomes = await db.Pessoas.AsNoTracking().Where(p => pessoas.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.NomeExibicao ?? p.Nome, ct);
            foreach (var n in new[] { NivelParticipante.Empresa, NivelParticipante.Colaborador })
                foreach (var id in Ids(n))
                    if (nomes.TryGetValue(id, out var nome)) resultado[(n, id)] = nome;
        }

        var filiais = Ids(NivelParticipante.Filial);
        if (filiais.Count > 0)
            foreach (var f in await Filiais(db, filiais))
                resultado[(NivelParticipante.Filial, f.Id)] = f.Nome;

        var departamentos = Ids(NivelParticipante.Departamento);
        if (departamentos.Count > 0)
            foreach (var d in await db.Departamentos.AsNoTracking().Where(d => departamentos.Contains(d.Id)).Select(d => new { d.Id, d.Nome }).ToListAsync(ct))
                resultado[(NivelParticipante.Departamento, d.Id)] = d.Nome;

        var equipes = Ids(NivelParticipante.Equipe);
        if (equipes.Count > 0)
            foreach (var e in await db.Equipes.AsNoTracking().Where(e => equipes.Contains(e.Id)).Select(e => new { e.Id, e.Nome }).ToListAsync(ct))
                resultado[(NivelParticipante.Equipe, e.Id)] = e.Nome;

        return resultado;
    }

    /// <summary>
    /// Filial = estabelecimento de uma empresa do grupo ("Nome fantasia" ou nome da empresa + final do CNPJ). Filtra
    /// pelos Ids antes de projetar (o EF não filtra sobre um record criado pelo construtor).
    /// </summary>
    private static Task<List<ParticipanteOpcaoDto>> Filiais(LoneDbContext db, List<Guid> ids) =>
        db.Pessoas.AsNoTracking()
            .Where(p => p.Papeis.Any(x => x.Papel == TipoPapel.EmpresaDoGrupo))
            .SelectMany(p => p.Estabelecimentos.Where(e => ids.Contains(e.Id)).Select(e => new ParticipanteOpcaoDto(
                NivelParticipante.Filial, e.Id,
                (e.NomeFantasia ?? p.NomeExibicao ?? p.Nome) + (e.Cnpj != null && e.Cnpj.Length == 14 ? " (" + e.Cnpj.Substring(8, 4) + ")" : ""))))
            .ToListAsync();

    public async Task<List<ParticipanteOpcaoDto>> OpcoesAsync(DateOnly hoje, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var lista = new List<ParticipanteOpcaoDto>();

        lista.AddRange(await db.Pessoas.AsNoTracking()
            .Where(p => p.Situacao == SituacaoPessoa.Ativo && p.Papeis.Any(x => x.Papel == TipoPapel.EmpresaDoGrupo && x.Ativo))
            .Select(p => new ParticipanteOpcaoDto(NivelParticipante.Empresa, p.Id, p.NomeExibicao ?? p.Nome))
            .ToListAsync(ct));

        lista.AddRange(await db.Pessoas.AsNoTracking()
            .Where(p => p.Situacao == SituacaoPessoa.Ativo && p.Papeis.Any(x => x.Papel == TipoPapel.EmpresaDoGrupo && x.Ativo))
            .SelectMany(p => p.Estabelecimentos.Where(e => e.Ativo).Select(e => new ParticipanteOpcaoDto(
                NivelParticipante.Filial, e.Id,
                (e.NomeFantasia ?? p.NomeExibicao ?? p.Nome) + (e.Cnpj != null && e.Cnpj.Length == 14 ? " (" + e.Cnpj.Substring(8, 4) + ")" : ""))))
            .ToListAsync(ct));

        lista.AddRange(await db.Departamentos.AsNoTracking().Where(d => d.Ativo)
            .Select(d => new ParticipanteOpcaoDto(NivelParticipante.Departamento, d.Id, d.Nome)).ToListAsync(ct));

        lista.AddRange(await db.Equipes.AsNoTracking().Where(e => e.Ativo)
            .Select(e => new ParticipanteOpcaoDto(NivelParticipante.Equipe, e.Id, e.Nome)).ToListAsync(ct));

        // Colaborador: vínculo vigente, ou quem pode ocupar um papel comercial que conta para metas ("Quem pode ser" do
        // papel; ex.: representante, que não tem vínculo de colaborador).
        var aceitas = db.TiposCarteiraClassificacoes.AsNoTracking()
            .Where(c => c.Ativo && db.TiposCarteira.Any(t => t.Id == c.TipoCarteiraId && t.Ativo && t.ContaParaMetas))
            .Select(c => c.PapelId);
        lista.AddRange(await db.Pessoas.AsNoTracking()
            .Where(p => p.Situacao == SituacaoPessoa.Ativo &&
                        (db.VinculosColaborador.Any(v => v.PessoaId == p.Id && v.AdmissaoEm <= hoje && (v.DesligamentoEm == null || v.DesligamentoEm >= hoje)) ||
                         p.Papeis.Any(x => x.Ativo && aceitas.Contains(x.PapelId) && (x.FimEm == null || x.FimEm >= hoje))))
            .Select(p => new ParticipanteOpcaoDto(NivelParticipante.Colaborador, p.Id, p.NomeExibicao ?? p.Nome))
            .ToListAsync(ct));

        return lista.OrderBy(o => o.Nivel).ThenBy(o => o.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<Dictionary<Guid, string>> NomesPessoasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();
        await using var db = await AbrirAsync(ct);
        var lista = ids.Distinct().ToList();
        return await db.Pessoas.AsNoTracking().Where(p => lista.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.NomeExibicao ?? p.Nome, ct);
    }
}

/// <summary>
/// Realizado dos indicadores de sistema, contado no banco (sem trazer os clientes para a memória):
/// participante → vendedores do nível no período → clientes da carteira desses vendedores no período, nos papéis que
/// contam para metas → contagem.
/// Filial usa a empresa da filial enquanto a lotação não tiver estabelecimento (ver CONTINUIDADE.md).
/// </summary>
public class FonteIndicadoresCadastro : ServicoDadosBase, IFonteIndicadores
{
    public FonteIndicadoresCadastro(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<Dictionary<Guid, decimal>> CalcularAsync(FonteIndicador fonte, DateOnly inicio, DateOnly fim,
                                                               IReadOnlyList<MetaParticipante> participantes, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, decimal>();
        if (fonte == FonteIndicador.Informado || participantes.Count == 0) return resultado;
        await using var db = await AbrirAsync(ct);
        var papelCliente = PapeisSistema.Id(TipoPapel.Cliente);
        var de = inicio.ToDateTime(TimeOnly.MinValue);
        var ate = fim.AddDays(1).ToDateTime(TimeOnly.MinValue);

        foreach (var p in participantes)
        {
            var (vendedores, empresa) = await VendedoresAsync(db, p, inicio, fim, ct);
            if (vendedores.Count == 0) { resultado[p.Id] = 0; continue; }

            // Só os papéis que contam para metas (Motor Comercial, Fase 1a): um supervisor ou apoio na carteira não soma
            // o cliente ao realizado dele como se fosse o vendedor.
            var clientes = db.CarteiraClientes.AsNoTracking()
                .Where(c => c.Ativo && vendedores.Contains(c.VendedorId) && c.InicioEm <= fim && (c.FimEm == null || c.FimEm >= inicio) &&
                            (empresa == null || c.EmpresaId == null || c.EmpresaId == empresa) &&
                            db.TiposCarteira.Any(t => t.Id == c.TipoCarteiraId && t.ContaParaMetas))
                .Select(c => c.PessoaId)
                .Distinct();

            var periodos = db.PessoaPapeis.AsNoTracking().Where(x => x.PapelId == papelCliente && clientes.Contains(x.PessoaId));
            // Só datas: período encerrado fica com Ativo = falso e o fim preenchido (é histórico, conta).
            resultado[p.Id] = fonte switch
            {
                // Primeiro período de cliente começou dentro da meta.
                FonteIndicador.NovosClientes => await periodos
                    .Where(x => x.InicioEm >= inicio && x.InicioEm <= fim &&
                                !db.PessoaPapeis.Any(y => y.PessoaId == x.PessoaId && y.PapelId == papelCliente && y.InicioEm < inicio))
                    .Select(x => x.PessoaId).Distinct().CountAsync(ct),
                // Cliente no fim do período.
                FonteIndicador.ClientesAtivos => await periodos
                    .Where(x => x.InicioEm <= fim && (x.FimEm == null || x.FimEm >= fim))
                    .Select(x => x.PessoaId).Distinct().CountAsync(ct),
                // Voltou a ser cliente no período depois de um período anterior encerrado.
                FonteIndicador.ClientesReativados => await periodos
                    .Where(x => x.InicioEm >= inicio && x.InicioEm <= fim &&
                                db.PessoaPapeis.Any(y => y.PessoaId == x.PessoaId && y.PapelId == papelCliente &&
                                                         y.InicioEm < inicio && y.FimEm != null && y.FimEm < x.InicioEm))
                    .Select(x => x.PessoaId).Distinct().CountAsync(ct),
                FonteIndicador.InteracoesRegistradas => await db.Interacoes.AsNoTracking()
                    .Where(i => clientes.Contains(i.PessoaId) && i.DataHora >= de && i.DataHora < ate)
                    .CountAsync(ct),
                _ => 0
            };
        }
        return resultado;
    }

    /// <summary>Quem vende pelo participante no período (e a empresa, quando o nível é empresa/filial).</summary>
    private static async Task<(List<Guid> Vendedores, Guid? Empresa)> VendedoresAsync(LoneDbContext db, MetaParticipante p,
                                                                                      DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        switch (p.Nivel)
        {
            case NivelParticipante.Colaborador:
                return ([p.ReferenciaId], null);
            case NivelParticipante.Equipe:
                return (await db.MembrosEquipe.AsNoTracking()
                    .Where(m => m.EquipeId == p.ReferenciaId && m.InicioEm <= fim && (m.FimEm == null || m.FimEm >= inicio))
                    .Select(m => m.PessoaId).Distinct().ToListAsync(ct), null);
            case NivelParticipante.Departamento:
                return (await db.LotacoesColaborador.AsNoTracking()
                    .Where(l => l.DepartamentoId == p.ReferenciaId && l.InicioEm <= fim && (l.FimEm == null || l.FimEm >= inicio))
                    .Select(l => l.PessoaId).Distinct().ToListAsync(ct), null);
            default:
                var empresa = p.Nivel == NivelParticipante.Empresa
                    ? p.ReferenciaId
                    : await db.Estabelecimentos.AsNoTracking().Where(e => e.Id == p.ReferenciaId).Select(e => (Guid?)e.PessoaId).FirstOrDefaultAsync(ct);
                if (empresa is null) return ([], null);
                return (await db.VinculosColaborador.AsNoTracking()
                    .Where(v => v.EmpresaId == empresa && v.AdmissaoEm <= fim && (v.DesligamentoEm == null || v.DesligamentoEm >= inicio))
                    .Select(v => v.PessoaId).Distinct().ToListAsync(ct), empresa);
        }
    }
}
