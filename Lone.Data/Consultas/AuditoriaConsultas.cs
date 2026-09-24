using Lone.Aplicacao.Auditoria;
using Lone.Aplicacao.Seguranca;
using Lone.Data.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Data.Consultas;

public class AuditoriaConsultas : ServicoDadosBase, IAuditoriaConsultas
{
    public AuditoriaConsultas(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<RegistroHistorico>> ListarPorRaizAsync(
        string raizEntidade, int raizId, int limite, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Auditoria.AsNoTracking()
            .Where(a => a.RaizEntidade == raizEntidade && a.RaizId == raizId)
            .OrderByDescending(a => a.DataHora).ThenByDescending(a => a.Id)
            .Take(limite)
            .Select(a => new RegistroHistorico
            {
                DataHora = a.DataHora,
                Usuario = a.Usuario,
                Origem = a.Origem,
                Acao = a.Acao,
                Entidade = a.Entidade,
                Campo = a.Campo,
                ValorAnterior = a.ValorAnterior,
                ValorNovo = a.ValorNovo
            })
            .ToListAsync(ct);
    }
}
