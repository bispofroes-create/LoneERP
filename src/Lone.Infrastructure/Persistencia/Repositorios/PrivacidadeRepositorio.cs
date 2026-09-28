using Lone.Application.Privacidade;
using Lone.Application.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Privacidade da pessoa: lê pessoa + telefones/e-mails + consentimentos; inclui períodos e grava revogações.
/// Nunca apaga consentimentos. A auditoria (ColetorAuditoria) registra cada inclusão/alteração com o motivo.
/// </summary>
public class PrivacidadeRepositorio : ServicoDadosBase, IPrivacidadeRepositorio
{
    public PrivacidadeRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<Pessoa?> ObterPessoaAsync(Guid pessoaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Sem escopo: por id, e a rota com o id da pessoa passa antes pelo filtro de escopo da API.
        return await db.Pessoas.AsNoTracking()
            .Include(p => p.MeiosContato)
            .Include(p => p.Consentimentos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == pessoaId, ct);
    }

    public async Task<List<FinalidadeTratamento>> ListarFinalidadesAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.FinalidadesTratamento.AsNoTracking().OrderBy(f => f.Ordem).ThenBy(f => f.Nome).ToListAsync(ct);
    }

    public async Task IncluirConsentimentoAsync(PessoaConsentimento consentimento, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.PessoaConsentimentos.Add(consentimento);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql
                                           && sql.Message.Contains(SqlMigracaoPrivacidade.IndiceEmVigor))
        {
            // Outro usuário concedeu a mesma finalidade/canal ao mesmo tempo.
            throw new ValidacaoException(["Já existe consentimento em vigor para esta finalidade e canal."]);
        }
    }

    public async Task RevogarConsentimentoAsync(PessoaConsentimento revogado, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var atual = await db.PessoaConsentimentos.FirstOrDefaultAsync(c => c.Id == revogado.Id, ct) ?? throw new ConflitoDeEdicaoException();
        if (!atual.Concedido) throw new ValidacaoException(["Este consentimento já foi revogado."]);
        atual.Concedido = false;
        atual.RevogadoEm = revogado.RevogadoEm;
        atual.RevogadoPor = revogado.RevogadoPor;
        atual.MotivoRevogacao = revogado.MotivoRevogacao;
        await db.SaveChangesAsync(ct);
    }
}
