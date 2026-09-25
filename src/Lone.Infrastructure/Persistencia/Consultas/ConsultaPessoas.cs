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
        var vendedor = PapeisSistema.Id(TipoPapel.Vendedor);
        var representante = PapeisSistema.Id(TipoPapel.Representante);
        return new OpcoesConsultaPessoasDto
        {
            Papeis = await db.Papeis.AsNoTracking().Where(p => p.Ativo).OrderBy(p => p.Ordem)
                .Select(p => new OpcaoConsultaDto(p.Id, p.Nome)).ToListAsync(ct),
            Etiquetas = await db.Etiquetas.AsNoTracking().Where(e => e.Ativo).OrderBy(e => e.Nome)
                .Select(e => new OpcaoConsultaDto(e.Id, e.Nome)).ToListAsync(ct),
            Vendedores = await db.Pessoas.AsNoTracking()
                .Where(p => p.Papeis.Any(x => x.Ativo && (x.PapelId == vendedor || x.PapelId == representante)))
                .OrderBy(p => p.NomeExibicao ?? p.Nome)
                .Select(p => new OpcaoConsultaDto(p.Id, p.NomeExibicao ?? p.Nome)).ToListAsync(ct),
            CamposPesquisaveis = await db.CamposPersonalizados.AsNoTracking()
                .Where(c => c.Ativo && c.Pesquisavel && c.Entidade == EntidadePersonalizavel.Pessoa)
                .OrderBy(c => c.Nome).Select(c => new OpcaoConsultaDto(c.Id, c.Nome)).ToListAsync(ct)
        };
    }

    // ---------------------------------------------------------------- Critérios

    private static async Task<IQueryable<Pessoa>> FiltrarAsync(LoneDbContext db, CriteriosPessoas c, DateOnly hoje, CancellationToken ct)
    {
        IQueryable<Pessoa> q = db.Pessoas.AsNoTracking();

        if (c.Situacoes.Count > 0)
        {
            var situacoes = c.Situacoes;
            q = q.Where(p => situacoes.Contains(p.Situacao));
        }
        else
            q = q.Where(p => p.Situacao == SituacaoPessoa.Ativo || p.Situacao == SituacaoPessoa.EmAnalise);

        if (c.Naturezas.Count > 0)
        {
            var naturezas = c.Naturezas;
            q = q.Where(p => naturezas.Contains(p.Natureza));
        }

        if (c.Texto is { } texto)
            q = PessoaRepositorio.AplicarBusca(q, texto, db);

        if (c.PapeisIds.Count > 0)
        {
            var papeis = c.PapeisIds;
            q = c.TodosOsPapeis
                ? papeis.Aggregate(q, (atual, papel) => atual.Where(p => p.Papeis.Any(x => x.Ativo && x.PapelId == papel)))
                : q.Where(p => p.Papeis.Any(x => x.Ativo && papeis.Contains(x.PapelId)));
        }

        if (c.EtiquetasIds.Count > 0)
        {
            var etiquetas = c.EtiquetasIds;
            q = q.Where(p => p.Etiquetas.Any(e => etiquetas.Contains(e.EtiquetaId)));
        }

        if (c.Uf is { } uf)
            q = q.Where(p => p.Enderecos.Any(e => e.Ativo && e.Uf == uf));
        if (c.MunicipioId is { } municipio)
            q = q.Where(p => p.Enderecos.Any(e => e.Ativo && e.MunicipioId == municipio));

        if (c.Cnae is { } cnae)
        {
            // Prefixo de código numérico = faixa: "47" → 4700000..4799999 (usa o índice (Codigo, Principal)).
            var faltam = 7 - cnae.Length;
            var de = int.Parse(cnae) * (int)Math.Pow(10, faltam);
            var ate = de + (int)Math.Pow(10, faltam) - 1;
            var somentePrincipal = c.SomenteCnaePrincipal;
            q = q.Where(p => db.EstabelecimentoCnaes.Any(x => x.PessoaId == p.Id && x.Codigo >= de && x.Codigo <= ate &&
                                                              (!somentePrincipal || x.Principal)));
        }
        if (c.ProdutorRural is { } rural)
            q = q.Where(p => p.Estabelecimentos.Any(e => e.Ativo && e.ProdutorRural == rural));
        if (c.Regime is { } regime)
            q = q.Where(p => p.Estabelecimentos.Any(e => e.Ativo && e.RegimeTributario == regime));

        if (c.VendedorId is { } vendedor)
            q = q.Where(p => db.CarteiraClientes.Any(x => x.PessoaId == p.Id && x.VendedorId == vendedor && x.Ativo &&
                                                          x.InicioEm <= hoje && (x.FimEm == null || x.FimEm >= hoje)));
        if (c.SemCarteira)
            q = q.Where(p => !db.CarteiraClientes.Any(x => x.PessoaId == p.Id && x.Ativo && x.InicioEm <= hoje && (x.FimEm == null || x.FimEm >= hoje)));

        if (c.SemInteracaoDias is { } dias)
        {
            var desde = hoje.AddDays(-dias + 1).ToDateTime(TimeOnly.MinValue);
            q = q.Where(p => !db.Interacoes.Any(i => i.PessoaId == p.Id && i.DataHora >= desde));
        }

        if (c.Relacionamento is { } situacao)
        {
            var parametros = await db.ParametrosRelacionamento.AsNoTracking().FirstOrDefaultAsync(ct) ?? new ParametrosRelacionamento();
            // Mesma regra de ParametrosRelacionamento.Situacao: dias completos desde a última interação.
            var limiteRisco = hoje.AddDays(-parametros.DiasEmRisco + 1).ToDateTime(TimeOnly.MinValue);
            var limiteInativo = hoje.AddDays(-parametros.DiasInativo + 1).ToDateTime(TimeOnly.MinValue);
            q = situacao switch
            {
                SituacaoRelacionamento.SemInteracao => q.Where(p => !db.Interacoes.Any(i => i.PessoaId == p.Id)),
                SituacaoRelacionamento.Ativo => q.Where(p => db.Interacoes.Any(i => i.PessoaId == p.Id && i.DataHora >= limiteRisco)),
                SituacaoRelacionamento.EmRisco => q.Where(p =>
                    db.Interacoes.Any(i => i.PessoaId == p.Id && i.DataHora >= limiteInativo) &&
                    !db.Interacoes.Any(i => i.PessoaId == p.Id && i.DataHora >= limiteRisco)),
                SituacaoRelacionamento.Inativo => q.Where(p =>
                    db.Interacoes.Any(i => i.PessoaId == p.Id) &&
                    !db.Interacoes.Any(i => i.PessoaId == p.Id && i.DataHora >= limiteInativo)),
                _ => q
            };
        }

        if (c.Bloqueado is { } bloqueado)
            q = bloqueado
                ? q.Where(p => db.Bloqueios.Any(b => b.PessoaId == p.Id && b.FimEm == null))
                : q.Where(p => !db.Bloqueios.Any(b => b.PessoaId == p.Id && b.FimEm == null));

        if (c.DocumentosVencidos)
            q = q.Where(p => p.Documentos.Any(d => d.Ativo && d.ValidoAte != null && d.ValidoAte < hoje));
        if (c.DocumentosVencendoDias is { } vencendo)
        {
            var ate = hoje.AddDays(vencendo);
            q = q.Where(p => p.Documentos.Any(d => d.Ativo && d.ValidoAte != null && d.ValidoAte >= hoje && d.ValidoAte <= ate));
        }

        if (c.CampoId is { } campo)
        {
            if (c.CampoValor is { } valor)
            {
                var inicio = TextoDoCampo(valor);
                q = q.Where(p => p.ValoresPersonalizados.Any(v => v.CampoId == campo &&
                    EF.Property<string>(v, ConfiguracaoValorPersonalizado.ColunaBusca).StartsWith(inicio)));
            }
            else
                q = q.Where(p => p.ValoresPersonalizados.Any(v => v.CampoId == campo));
        }

        if (c.CadastradoDe is { } cadDe)
        {
            var de = cadDe.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            q = q.Where(p => p.CriadoEm >= de);
        }
        if (c.CadastradoAte is { } cadAte)
        {
            var ate = cadAte.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            q = q.Where(p => p.CriadoEm < ate);
        }

        return q;
    }

    private static string TextoDoCampo(string valor) =>
        valor.Length > ConfiguracaoValorPersonalizado.TamanhoBusca ? valor[..ConfiguracaoValorPersonalizado.TamanhoBusca] : valor;
}
