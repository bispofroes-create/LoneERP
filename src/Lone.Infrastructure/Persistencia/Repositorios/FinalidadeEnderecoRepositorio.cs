using Lone.Application.Enderecos;
using Lone.Application.Seguranca;
using Lone.Contracts.Enderecos;
using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

public class FinalidadeEnderecoRepositorio : ServicoDadosBase, IFinalidadeEnderecoRepositorio
{
    public FinalidadeEnderecoRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<FinalidadeEnderecoCadastro>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.FinalidadesEndereco.AsNoTracking().OrderBy(f => f.Ordem).ToListAsync(ct);
    }
}

/// <summary>
/// Aponta endereços duplicados na base (só leitura). Pessoas com 2+ endereços ativos, em ordem de Id, em lotes:
/// a comparação normalizada (DuplicidadeEndereco) roda na memória, lote a lote, sem carregar a base inteira.
/// </summary>
public class EnderecosDuplicadosConsulta : ServicoDadosBase, IEnderecosDuplicadosConsulta
{
    private readonly IEscopoPessoas _escopo;

    public EnderecosDuplicadosConsulta(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario, IEscopoPessoas escopo)
        : base(fabrica, usuario)
    {
        _escopo = escopo;
    }

    public async Task<PaginaEnderecosDuplicados> ListarAsync(Guid? apos, int limite, int examinar, CancellationToken ct)
    {
        var escopo = await _escopo.ObterAsync(ct);
        await using var db = await AbrirAsync(ct);
        var candidatas = db.PessoaEnderecos.AsNoTracking().Where(e => e.Ativo);
        // Só os cadastros no alcance do usuário (Fase 2a-2).
        if (!escopo.Tudo)
        {
            var noEscopo = Consultas.EscopoPessoasSql.Pessoas(db, escopo);
            candidatas = candidatas.Where(e => noEscopo.Any(p => p.Id == e.PessoaId));
        }
        if (apos is { } a) candidatas = candidatas.Where(e => e.PessoaId.CompareTo(a) > 0);
        var ids = await candidatas.GroupBy(e => e.PessoaId).Where(g => g.Count() > 1)
            .OrderBy(g => g.Key).Select(g => g.Key).Take(examinar).ToListAsync(ct);

        var pagina = new PaginaEnderecosDuplicados();
        if (ids.Count == 0) return pagina;

        var enderecos = (await db.PessoaEnderecos.AsNoTracking().Where(e => e.Ativo && ids.Contains(e.PessoaId)).ToListAsync(ct))
            .GroupBy(e => e.PessoaId).ToDictionary(g => g.Key, g => g.ToList());
        Guid? ultimo = null;
        foreach (var id in ids)
        {
            ultimo = id;
            var pares = DuplicidadeEndereco.Pares(enderecos[id], incluirPossiveis: true);
            if (pares.Count == 0) continue;
            pagina.Itens.Add(new EnderecosDuplicadosDto
            {
                PessoaId = id,
                Enderecos = pares.Select(p => $"{DuplicidadeEndereco.Resumo(p.A)} ⇄ {DuplicidadeEndereco.Resumo(p.B)}" +
                                             (p.Semelhanca == SemelhancaEndereco.Possivel ? " (possível)" : string.Empty)).ToList()
            });
            if (pagina.Itens.Count >= limite) break;
        }

        // Sem escopo: nomes da página, cujas pessoas já passaram pelo escopo acima.
        var nomes = await db.Pessoas.AsNoTracking().Where(p => pagina.Itens.Select(i => i.PessoaId).Contains(p.Id))
            .Select(p => new { p.Id, p.Codigo, Nome = p.NomeExibicao ?? p.Nome }).ToDictionaryAsync(p => p.Id, ct);
        foreach (var item in pagina.Itens)
            if (nomes.TryGetValue(item.PessoaId, out var n)) { item.Codigo = n.Codigo; item.Nome = n.Nome; }

        // Continua depois da última examinada (se o lote veio cheio ou parou pelo limite, há mais para ver).
        pagina.ProximoId = ids.Count == examinar || pagina.Itens.Count >= limite ? ultimo : null;
        return pagina;
    }
}
