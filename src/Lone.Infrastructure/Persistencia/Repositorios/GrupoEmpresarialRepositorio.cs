using Lone.Application.GruposEmpresariais;
using Lone.Application.Seguranca;
using Lone.Contracts.GruposEmpresariais;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Grupos empresariais (SQL Server via EF Core). Nunca apaga.</summary>
public class GrupoEmpresarialRepositorio : ServicoDadosBase, IGrupoEmpresarialRepositorio
{
    public GrupoEmpresarialRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<GrupoEmpresarial>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.GruposEmpresariais.AsNoTracking().ToListAsync(ct);
    }

    public async Task<GrupoEmpresarial?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.GruposEmpresariais.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarEmpresasAsync(Guid? somenteId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Sem escopo: configuração de grupos empresariais (contagem e empresas do grupo), com permissão própria.
        var pessoas = db.Pessoas.AsNoTracking().Where(p => p.GrupoEmpresarialId != null);
        if (somenteId is { } id) pessoas = pessoas.Where(p => p.GrupoEmpresarialId == id);
        return await pessoas.GroupBy(p => p.GrupoEmpresarialId!.Value)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Quantidade, ct);
    }

    public async Task<List<EmpresaDoGrupoEmpresarialDto>> ListarEmpresasAsync(Guid grupoId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Sem escopo: configuração de grupos empresariais (contagem e empresas do grupo), com permissão própria.
        var linhas = await db.Pessoas.AsNoTracking()
            .Where(p => p.GrupoEmpresarialId == grupoId)
            .Select(p => new
            {
                p.Id,
                p.Codigo,
                p.Natureza,
                p.Nome,
                p.NomeExibicao,
                p.NomeSocial,
                p.Situacao,
                Fantasia = p.Estabelecimentos.Where(e => e.Principal).Select(e => e.NomeFantasia).FirstOrDefault(),
                Cnpj = p.Estabelecimentos.Where(e => e.Principal).Select(e => e.Cnpj).FirstOrDefault(),
                Estabelecimentos = p.Estabelecimentos.Count,
                Ativos = p.Estabelecimentos.Count(e => e.Ativo)
            })
            .ToListAsync(ct);

        return linhas
            .Select(l => new EmpresaDoGrupoEmpresarialDto
            {
                Id = l.Id,
                Codigo = l.Codigo,
                Nome = NomePessoa.ParaExibir(l.Natureza, l.Nome, l.NomeExibicao, l.NomeSocial, l.Fantasia),
                RazaoSocial = l.Nome,
                CnpjPrincipal = l.Cnpj,
                QuantidadeEstabelecimentos = l.Estabelecimentos,
                EstabelecimentosAtivos = l.Ativos,
                Situacao = l.Situacao
            })
            .OrderBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task SalvarAsync(GrupoEmpresarial item, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        if (novo)
        {
            if (item.Id == Guid.Empty) item.Id = IdSequencial.Novo();
            db.GruposEmpresariais.Add(item);
        }
        else
        {
            var atual = await db.GruposEmpresariais.FirstOrDefaultAsync(g => g.Id == item.Id, ct) ?? throw new ConflitoDeEdicaoException();
            var versaoAberta = item.Versao;
            item.Versao = atual.Versao;
            item.CriadoEm = atual.CriadoEm;
            item.AtualizadoEm = atual.AtualizadoEm;

            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(item);
            entrada.Property(g => g.Versao).OriginalValue = versaoAberta;
            atual.ReceberEventosDe(item);
            entrada.Property(g => g.AtualizadoEm).IsModified = true;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoDeEdicaoException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ValidacaoException(["Já existe um grupo empresarial com este nome."]);
        }
    }
}
