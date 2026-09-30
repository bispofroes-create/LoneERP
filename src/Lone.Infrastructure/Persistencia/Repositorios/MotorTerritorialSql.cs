using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// A trava do motor de um mapa (DN-02) e o numerador de documentos (DN-13), usados dentro da transação de quem grava.
/// Toda conferência de versão é um UPDATE ... WHERE Versao = @vista: sob READ_COMMITTED_SNAPSHOT (ligado no LoneERP) o
/// UPDATE espera a trava de quem está gravando e confere a versão confirmada; uma leitura comum veria a versão velha.
/// </summary>
internal static class MotorTerritorialSql
{
    public const string MensagemMotorAusente =
        "Inconsistência interna: o mapa territorial não tem o controle do motor (MapaTerritorialMotor). Nada foi gravado.";

    /// <summary>
    /// Troca a versão do motor do mapa, sem exigir a anterior: mudança estrutural da árvore, ativação/desativação e campos
    /// travados pelo uso mudam o resultado do motor, e as simulações abertas precisam ficar desatualizadas. Também serializa
    /// com uma aplicação em curso no mesmo mapa (espera o COMMIT dela). Sempre depois da trava da árvore (ordem única).
    /// </summary>
    public static async Task TocarAsync(LoneDbContext db, Guid mapaId, DateTime agora, CancellationToken ct)
    {
        var afetadas = await db.MapaTerritorialMotores.Where(m => m.MapaId == mapaId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.AtualizadoEm, agora), ct);
        if (afetadas == 0) throw new InvalidOperationException(MensagemMotorAusente);
    }

    /// <summary>Exige a versão do motor que a simulação leu (e troca). Falso = o mapa mudou desde a simulação.</summary>
    public static async Task<bool> ExigirAsync(LoneDbContext db, Guid mapaId, byte[] versaoVista, DateTime agora, CancellationToken ct) =>
        await db.MapaTerritorialMotores.Where(m => m.MapaId == mapaId && m.Versao == versaoVista)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.AtualizadoEm, agora), ct) == 1;

    /// <summary>
    /// Próximo número do prefixo no ano (TE-2026-0001...), por incremento atômico no banco, nunca MAX + 1. Precisa de
    /// transação aberta: a trava do UPDATE (e a do intervalo, com HOLDLOCK, quando o ano ainda não tem linha) fica até o
    /// COMMIT, então dois pedidos simultâneos recebem números diferentes; se a transação cair, o número volta (não sobra
    /// buraco por rascunho que não chegou a ser gravado).
    /// </summary>
    public static async Task<int> ProximoNumeroAsync(LoneDbContext db, string prefixo, int ano, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A numeração de documentos precisa de transação aberta.");
        var numeros = await db.Database.SqlQuery<int>($"""
            SET NOCOUNT ON;
            DECLARE @numero TABLE (Value int NOT NULL);
            UPDATE NumeracoesDocumento WITH (UPDLOCK, HOLDLOCK)
               SET Ultimo = Ultimo + 1
            OUTPUT inserted.Ultimo INTO @numero (Value)
             WHERE Prefixo = {prefixo} AND Ano = {ano};
            IF NOT EXISTS (SELECT 1 FROM @numero)
                INSERT INTO NumeracoesDocumento (Prefixo, Ano, Ultimo)
                OUTPUT inserted.Ultimo INTO @numero (Value)
                VALUES ({prefixo}, {ano}, 1);
            SELECT Value FROM @numero;
            """).ToListAsync(ct);
        return numeros.Single();
    }
}
