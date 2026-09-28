using Lone.Application.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

/// <summary>
/// O escopo de acesso de Pessoas no banco (Motor Comercial, Fase 2a-2), num ponto só. Toda consulta que lista, conta ou
/// abre cadastros de Pessoas começa por <see cref="Pessoas"/> (ou passa a consulta por <see cref="Aplicar"/>); o teste de
/// arquitetura confere que <c>db.Pessoas</c> não aparece fora das exceções justificadas.
/// A regra é a do domínio (<see cref="RegrasEscopo.VinculoNoEscopo"/>): EXISTS em CarteiraClientes pelos vendedores
/// alcançados, com o índice (VendedorId, Ativo, FimEm). Só LINQ parametrizado.
/// </summary>
public static class EscopoPessoasSql
{
    /// <summary>A tabela de Pessoas já no escopo (sem rastreamento).</summary>
    public static IQueryable<Pessoa> Pessoas(LoneDbContext db, EscopoResolvido escopo) =>
        Aplicar(db.Pessoas.AsNoTracking(), escopo, db);

    /// <summary>Restringe uma consulta de Pessoas ao escopo. Tudo: não muda nada (o comportamento de antes).</summary>
    public static IQueryable<Pessoa> Aplicar(IQueryable<Pessoa> consulta, EscopoResolvido escopo, LoneDbContext db)
    {
        if (escopo.Tudo) return consulta;
        if (escopo.Vazio) return consulta.Where(p => false);
        var vinculos = db.CarteiraClientes.AsNoTracking().Where(RegrasEscopo.VinculoNoEscopo(escopo));
        return consulta.Where(p => vinculos.Any(c => c.PessoaId == p.Id));
    }
}

/// <summary>Conferência de um cadastro contra o escopo (rotas com o id da pessoa, documento em uso, depois de salvar).</summary>
public sealed class PessoasNoEscopo : ServicoDadosBase, IPessoasNoEscopo
{
    public PessoasNoEscopo(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<SituacaoNoEscopo> SituacaoAsync(Guid pessoaId, EscopoResolvido escopo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // A tabela inteira, de propósito: "não existe" e "fora do escopo" são respostas diferentes para quem pergunta.
        if (!await db.Pessoas.AsNoTracking().AnyAsync(p => p.Id == pessoaId, ct)) return SituacaoNoEscopo.NaoExiste;
        if (escopo.Tudo) return SituacaoNoEscopo.NoEscopo;
        return await EscopoPessoasSql.Aplicar(db.Pessoas.AsNoTracking().Where(p => p.Id == pessoaId), escopo, db).AnyAsync(ct)
            ? SituacaoNoEscopo.NoEscopo
            : SituacaoNoEscopo.ForaDoEscopo;
    }
}
