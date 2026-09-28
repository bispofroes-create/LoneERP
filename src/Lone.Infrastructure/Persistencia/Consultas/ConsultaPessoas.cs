using Lone.Application.Consultas;
using Lone.Application.Seguranca;
using Lone.Contracts.Pessoas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Papeis;
using Lone.Domain.Validacao;
using Lone.Domain.Comum;
using Lone.Infrastructure.Persistencia.Configuracoes;
using Lone.Infrastructure.Persistencia.Repositorios;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

/// <summary>
/// Consulta avançada de pessoas: cada critério tipado vira um Where do LINQ (parâmetros, nunca SQL montado com texto).
/// Cada critério usa um índice próprio (papéis, etiquetas, CNAE, carteira, interações, validade de documentos, UF).
/// Ordem por Nome + Id, com paginação por chave: a página 50 custa o mesmo que a primeira.
/// </summary>
public class ConsultaPessoas : ServicoDadosBase, IConsultaPessoas
{
    public ConsultaPessoas(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<PaginaPessoas> ConsultarAsync(ConsultaPessoasRequisicao requisicao, DateOnly hoje, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var filtrada = await FiltrarAsync(db, requisicao.Criterios, hoje, ct);

        int? total = requisicao.ContarTotal ? await filtrada.CountAsync(ct) : null;

        var consulta = filtrada;
        if (requisicao.AposNome is { } nome && requisicao.AposId is { } id)
            consulta = consulta.Where(p => string.Compare(p.Nome, nome) > 0 || (p.Nome == nome && p.Id.CompareTo(id) > 0));

        // Uma linha a mais só para saber se existe próxima página.
        var chaves = await consulta.OrderBy(p => p.Nome).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.Nome })
            .Take(requisicao.Limite + 1)
            .ToListAsync(ct);
        var temMais = chaves.Count > requisicao.Limite;
        if (temMais) chaves.RemoveAt(chaves.Count - 1);

        var ids = chaves.Select(c => c.Id).ToList();
        var resumos = await PessoaRepositorio.Resumir(db.Pessoas.AsNoTracking().Where(p => ids.Contains(p.Id)), db).ToListAsync(ct);
        var porId = resumos.ToDictionary(r => r.Id);

        return new PaginaPessoas
        {
            Itens = ids.Where(porId.ContainsKey).Select(i => porId[i]).ToList(),
            ProximoNome = temMais ? chaves[^1].Nome : null,
            ProximoId = temMais ? chaves[^1].Id : null,
            Total = total
        };
    }

    public async Task<List<int>> ContarAsync(IReadOnlyList<CriteriosPessoas> criterios, DateOnly hoje, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var totais = new List<int>(criterios.Count);
        foreach (var c in criterios)
            totais.Add(await (await FiltrarAsync(db, c, hoje, ct)).CountAsync(ct));
        return totais;
    }

    public async Task<List<string[]>> LinhasParaExportarAsync(CriteriosPessoas criterios, DateOnly hoje, int limite, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var filtrada = await FiltrarAsync(db, criterios, hoje, ct);
        var linhas = await Repositorios.PessoaRepositorio.ComReferencia(filtrada.OrderBy(p => p.Nome).ThenBy(p => p.Id).Take(limite), db)
            .Select(r => new
            {
                r.P.Codigo,
                Nome = r.P.NomeExibicao ?? r.P.NomeSocial ?? r.P.Nome,
                r.P.Natureza,
                r.P.DocumentoPrincipal,
                Cnpj = r.P.Estabelecimentos.Where(e => e.Principal).Select(e => e.Cnpj).FirstOrDefault(),
                r.P.Situacao,
                Papeis = db.Papeis.Where(c => r.P.Papeis.Any(x => x.Ativo && x.PapelId == c.Id)).OrderBy(c => c.Ordem).Select(c => c.Nome).ToList(),
                Cidade = r.Cidade, // endereço de referência da listagem (só exibição)
                Uf = r.Uf,
                Telefone = r.P.MeiosContato.Where(m => m.Ativo && m.Tipo != TipoContato.Email)
                    .OrderByDescending(m => m.Principal).Select(m => m.Valor).FirstOrDefault(),
                Email = r.P.MeiosContato.Where(m => m.Ativo && m.Tipo == TipoContato.Email)
                    .OrderByDescending(m => m.Principal).Select(m => m.Valor).FirstOrDefault(),
                r.P.CriadoEm
            })
            .ToListAsync(ct);

        return linhas.Select(l => new[]
        {
            l.Codigo.ToString("000000"),
            l.Nome,
            NomesPessoa.Natureza(l.Natureza),
            Documento.Formatar(l.Natureza == NaturezaPessoa.Juridica ? l.Cnpj : l.DocumentoPrincipal),
            NomesPessoa.Situacao(l.Situacao),
            string.Join(", ", l.Papeis),
            l.Cidade ?? string.Empty,
            l.Uf ?? string.Empty,
            l.Telefone ?? string.Empty,
            l.Email ?? string.Empty,
            l.CriadoEm.ToLocalTime().ToString("dd/MM/yyyy")
        }).ToList();
    }

    public async Task RegistrarExportacaoAsync(string descricao, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Auditoria.Add(new RegistroAuditoria
        {
            DataHora = DateTime.UtcNow,
            Usuario = db.Usuario ?? "sistema",
            OperacaoId = IdSequencial.Novo(),
            Origem = OrigemAlteracao.Usuario,
            Entidade = "ConsultaPessoas",
            RegistroId = string.Empty,
            RaizEntidade = "ConsultaPessoas",
            Acao = AcaoAuditoria.Evento,
            Descricao = descricao.Length > 1000 ? descricao[..1000] : descricao
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<OpcoesConsultaPessoasDto> OpcoesAsync(DateOnly hoje, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Quem atende (Motor Comercial, Fase 1c): quem pode ocupar algum papel comercial ativo ("Quem pode ser") e quem
        // está em algum vínculo ativo da carteira (mesmo sem a classificação hoje), para filtrar a carteira de cada um.
        var aceitas = db.TiposCarteiraClassificacoes.AsNoTracking()
            .Where(c => c.Ativo && db.TiposCarteira.Any(t => t.Id == c.TipoCarteiraId && t.Ativo))
            .Select(c => c.PapelId);
        return new OpcoesConsultaPessoasDto
        {
            Papeis = await db.Papeis.AsNoTracking().Where(p => p.Ativo).OrderBy(p => p.Ordem)
                .Select(p => new OpcaoConsultaDto(p.Id, p.Nome)).ToListAsync(ct),
            Etiquetas = await db.Etiquetas.AsNoTracking().Where(e => e.Ativo).OrderBy(e => e.Nome)
                .Select(e => new OpcaoConsultaDto(e.Id, e.Nome)).ToListAsync(ct),
            Vendedores = await db.Pessoas.AsNoTracking()
                .Where(p => p.Papeis.Any(x => x.Ativo && aceitas.Contains(x.PapelId)) ||
                            db.CarteiraClientes.Any(c => c.VendedorId == p.Id && c.Ativo))
                .OrderBy(p => p.NomeExibicao ?? p.Nome)
                .Select(p => new OpcaoConsultaDto(p.Id, p.NomeExibicao ?? p.Nome)).ToListAsync(ct),
            CamposPesquisaveis = await db.CamposPersonalizados.AsNoTracking()
                .Where(c => c.Ativo && c.Pesquisavel && c.Entidade == EntidadePersonalizavel.Pessoa)
                .OrderBy(c => c.Nome).Select(c => new OpcaoConsultaDto(c.Id, c.Nome)).ToListAsync(ct)
        };
    }

    public async Task<Dictionary<string, List<OpcaoFiltroDto>>> OpcoesFiltroAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        static List<OpcaoFiltroDto> Opcoes(IEnumerable<(Guid Id, string Nome)> itens) =>
            itens.OrderBy(i => i.Nome, StringComparer.CurrentCultureIgnoreCase).Select(i => new OpcaoFiltroDto(i.Id.ToString("D"), i.Nome)).ToList();
        static List<OpcaoFiltroDto> Textos(IEnumerable<string?> valores) =>
            valores.OfType<string>().Where(v => v.Trim().Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(v => v, StringComparer.CurrentCultureIgnoreCase).Select(v => new OpcaoFiltroDto(v, v)).ToList();

        var empresaDoGrupo = PapeisSistema.Id(TipoPapel.EmpresaDoGrupo);
        var naturezas = await db.Estabelecimentos.AsNoTracking().Where(e => e.NaturezaJuridica != null)
            .Select(e => e.NaturezaJuridica!).Distinct().ToListAsync(ct);

        return new Dictionary<string, List<OpcaoFiltroDto>>
        {
            [CatalogoFiltrosPessoas.FontePortes] = Textos(await db.Pessoas.AsNoTracking().Select(p => p.Porte).Distinct().ToListAsync(ct)),
            [CatalogoFiltrosPessoas.FonteNaturezasJuridicas] = naturezas.Order(StringComparer.Ordinal)
                .Select(n => new OpcaoFiltroDto(n, Lone.Domain.Fiscal.NaturezasJuridicas.Descrever(n) is { Length: > 0 } d ? d : n)).ToList(),
            [CatalogoFiltrosPessoas.FonteGruposEmpresariais] = Opcoes((await db.GruposEmpresariais.AsNoTracking().Where(g => g.Ativo)
                .Select(g => new { g.Id, g.Nome }).ToListAsync(ct)).Select(g => (g.Id, g.Nome))),
            [CatalogoFiltrosPessoas.FonteProfissoes] = Opcoes((await db.Profissoes.AsNoTracking().Where(x => x.Ativo)
                .Select(x => new { x.Id, x.Nome }).ToListAsync(ct)).Select(x => (x.Id, x.Nome))),
            [CatalogoFiltrosPessoas.FonteFinalidadesEndereco] = (await db.FinalidadesEndereco.AsNoTracking().Where(x => x.Ativo)
                .OrderBy(x => x.Ordem).Select(x => new { x.Id, x.Nome }).ToListAsync(ct))
                .Select(x => new OpcaoFiltroDto(x.Id.ToString("D"), x.Nome)).ToList(),
            [CatalogoFiltrosPessoas.FonteTiposDocumento] = (await db.TiposDocumento.AsNoTracking().Where(x => x.Ativo)
                .OrderBy(x => x.Ordem).ThenBy(x => x.Nome).Select(x => new { x.Id, x.Nome }).ToListAsync(ct))
                .Select(x => new OpcaoFiltroDto(x.Id.ToString("D"), x.Nome)).ToList(),
            [CatalogoFiltrosPessoas.FonteSituacoesReceita] = Textos(await db.Estabelecimentos.AsNoTracking()
                .Select(e => e.SituacaoReceita).Distinct().ToListAsync(ct)),
            [CatalogoFiltrosPessoas.FontePerfisComerciais] = Opcoes((await db.PerfisComerciais.AsNoTracking().Where(x => x.Ativo)
                .Select(x => new { x.Id, x.Nome }).ToListAsync(ct)).Select(x => (x.Id, x.Nome))),
            [CatalogoFiltrosPessoas.FonteCondicoesPagamento] = Opcoes((await db.CondicoesPagamento.AsNoTracking().Where(x => x.Ativo)
                .Select(x => new { x.Id, x.Nome }).ToListAsync(ct)).Select(x => (x.Id, x.Nome))),
            [CatalogoFiltrosPessoas.FonteEmpresas] = Opcoes((await db.Pessoas.AsNoTracking()
                .Where(p => p.Papeis.Any(x => x.Ativo && x.PapelId == empresaDoGrupo))
                .Select(p => new { p.Id, Nome = p.NomeExibicao ?? p.Nome }).ToListAsync(ct)).Select(x => (x.Id, x.Nome))),
            [CatalogoFiltrosPessoas.FonteCargos] = Opcoes((await db.Cargos.AsNoTracking().Where(x => x.Ativo)
                .Select(x => new { x.Id, x.Nome }).ToListAsync(ct)).Select(x => (x.Id, x.Nome))),
            [CatalogoFiltrosPessoas.FonteDepartamentos] = Opcoes((await db.Departamentos.AsNoTracking().Where(x => x.Ativo)
                .Select(x => new { x.Id, x.Nome }).ToListAsync(ct)).Select(x => (x.Id, x.Nome))),
            [CatalogoFiltrosPessoas.FonteSetores] = Opcoes((await db.Setores.AsNoTracking().Where(x => x.Ativo)
                .Select(x => new { x.Id, x.Nome }).ToListAsync(ct)).Select(x => (x.Id, x.Nome))),
            [CatalogoFiltrosPessoas.FonteTiposRelacionamento] = Opcoes((await db.TiposRelacionamento.AsNoTracking()
                .Select(x => new { x.Id, x.Nome }).ToListAsync(ct)).Select(x => (x.Id, x.Nome))),
            [CatalogoFiltrosPessoas.FonteOrigens] = Textos(await db.Pessoas.AsNoTracking().Select(p => p.OrigemCadastro).Distinct().ToListAsync(ct)),
            [CatalogoFiltrosPessoas.FonteFinalidadesTratamento] = Opcoes((await db.FinalidadesTratamento.AsNoTracking().Where(x => x.Ativo)
                .Select(x => new { x.Id, x.Nome }).ToListAsync(ct)).Select(x => (x.Id, x.Nome)))
        };
    }

    // ---------------------------------------------------------------- Critérios

    /// <summary>
    /// Um motor só: critérios no formato antigo viram condições do catálogo (ConsultaPessoasAppService.CondicoesDe) e cada
    /// condição vira um Where (FiltrosPessoasSql). Sem condição de situação: ativos e em análise (como a lista).
    /// </summary>
    private static async Task<IQueryable<Pessoa>> FiltrarAsync(LoneDbContext db, CriteriosPessoas c, DateOnly hoje, CancellationToken ct)
    {
        IQueryable<Pessoa> q = db.Pessoas.AsNoTracking();
        var condicoes = ConsultaPessoasAppService.CondicoesDe(c);

        if (!condicoes.Any(x => x.Campo == CamposFiltroPessoas.Situacao))
            q = q.Where(p => p.Situacao == SituacaoPessoa.Ativo || p.Situacao == SituacaoPessoa.EmAnalise);

        if (c.Texto is { } texto)
            q = PessoaRepositorio.AplicarBusca(q, texto, db);

        var parametros = condicoes.Any(x => x.Campo == CamposFiltroPessoas.Relacionamento)
            ? await db.ParametrosRelacionamento.AsNoTracking().FirstOrDefaultAsync(ct) ?? new ParametrosRelacionamento()
            : new ParametrosRelacionamento();

        return FiltrosPessoasSql.Aplicar(q, condicoes, new FiltrosPessoasSql.Contexto(db, hoje, parametros));
    }

}
