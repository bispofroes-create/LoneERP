using Lone.Application.Infraestrutura;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Lone.Infrastructure.Persistencia.Servicos;

/// <summary>O que a API decidiu sobre aplicar migrações ao iniciar.</summary>
public enum DecisaoMigracaoAoIniciar
{
    /// <summary>A configuração não pede (padrão, inclusive em Development).</summary>
    NaoPedida,

    /// <summary>A configuração pede, mas falta o opt-in explícito da variável de ambiente: NÃO aplica.</summary>
    Bloqueada,

    /// <summary>Configuração E opt-in explícito: aplica (e registra que foi por opt-in).</summary>
    Permitida
}

public sealed record ResultadoMigracaoAoIniciar(DecisaoMigracaoAoIniciar Decisao, IReadOnlyList<string> Pendentes, bool Aplicou);

/// <summary>
/// Incidente P18 (06/10/2026): migração de banco persistente nunca acontece só porque a API foi iniciada. Para aplicar ao
/// iniciar são necessárias as DUAS coisas: <c>Banco:AplicarMigracoesAoIniciar = true</c> na configuração E a variável
/// de ambiente <see cref="Variavel"/> com o valor exato <see cref="ValorQuePermite"/>. Fail closed: variável ausente,
/// "false" ou qualquer outro valor (inclusive com espaços) não aplica. Com migração pendente e sem permissão, só registra
/// um aviso claro — a API sobe normalmente e a migração é aplicada de forma consciente (Update-Database com LONE_CONEXAO).
/// </summary>
public static class MigracaoAoIniciar
{
    public const string ChaveConfiguracao = "Banco:AplicarMigracoesAoIniciar";
    public const string Variavel = "LONE_PERMITIR_MIGRACAO_AUTOMATICA";
    public const string ValorQuePermite = "true";

    public static DecisaoMigracaoAoIniciar Decidir(IConfiguration configuracao, string? valorDaVariavel)
    {
        var pedida = bool.TryParse(configuracao[ChaveConfiguracao], out var valor) && valor;
        if (!pedida) return DecisaoMigracaoAoIniciar.NaoPedida;
        return string.Equals(valorDaVariavel, ValorQuePermite, StringComparison.OrdinalIgnoreCase)
            ? DecisaoMigracaoAoIniciar.Permitida
            : DecisaoMigracaoAoIniciar.Bloqueada;
    }

    public static async Task<ResultadoMigracaoAoIniciar> ExecutarAsync(IConfiguration configuracao, string? valorDaVariavel, IBancoDeDados banco,
                                                                     ILogger log, CancellationToken ct = default)
    {
        var decisao = Decidir(configuracao, valorDaVariavel);
        IReadOnlyList<string> pendentes;
        try
        {
            pendentes = await banco.MigracoesPendentesAsync(ct);
        }
        catch (Exception ex) when (decisao != DecisaoMigracaoAoIniciar.Permitida)
        {
            // Só informativo: sem banco agora, a API sobe como antes (a saúde do banco tem rota própria).
            log.LogWarning(ex, "Não foi possível conferir as migrações pendentes ao iniciar (nada foi aplicado).");
            return new(decisao, [], false);
        }

        if (pendentes.Count == 0)
            return new(decisao, pendentes, false);

        if (decisao == DecisaoMigracaoAoIniciar.Permitida)
        {
            log.LogWarning("Aplicando {Quantidade} migração(ões) ao iniciar POR OPT-IN EXPLÍCITO ({Chave}=true e {Variavel}=true): {Pendentes}",
                pendentes.Count, ChaveConfiguracao, Variavel, string.Join(", ", pendentes));
            await banco.PrepararAsync(ct);
            return new(decisao, pendentes, true);
        }

        log.LogWarning(
            "Há {Quantidade} migração(ões) pendente(s) neste banco: {Pendentes}. NADA foi aplicado: a aplicação automática ao iniciar " +
            "está desligada. Aplique de forma consciente (Update-Database com LONE_CONEXAO apontando para o banco alvo). " +
            "Para aplicar ao iniciar são necessários {Chave}=true E a variável {Variavel}=true.",
            pendentes.Count, string.Join(", ", pendentes), ChaveConfiguracao, Variavel);
        return new(decisao, pendentes, false);
    }
}
