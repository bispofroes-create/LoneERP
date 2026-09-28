using Lone.Contracts.Pessoas;

namespace Lone.Application.Consultas;

/// <summary>Um indicador da faixa: o que mostra e a condição do catálogo que ele conta (e que o toque aplica).</summary>
public sealed record DefinicaoIndicador(string Id, string Nome, bool Alerta, CondicaoFiltro Condicao, string? Dica = null);

/// <summary>
/// Faixa de indicadores acima da lista de pessoas (Etapa 3; decisões do usuário de 27/09/2026): números da base toda,
/// cada um igual a uma condição do catálogo (tocar põe a condição no painel, então o número e a lista filtrada batem).
/// Indicador novo = uma linha aqui, com um campo que já existe no catálogo.
/// </summary>
public static class IndicadoresListaPessoas
{
    public static readonly IReadOnlyList<DefinicaoIndicador> Todos =
    [
        new(IndicadoresPessoas.ComBloqueio, "Com bloqueio", Alerta: true, Sim(CamposFiltroPessoas.Bloqueado),
            "Bloqueio ativo (comercial, financeiro, cadastral ou de faturamento)."),
        new(IndicadoresPessoas.DocumentosVencidos, "Documentos vencidos", Alerta: true, Sim(CamposFiltroPessoas.DocumentosVencidos)),
        new(IndicadoresPessoas.DocumentosVencendo, "Documentos vencendo", Alerta: false, Sim(CamposFiltroPessoas.DocumentosVencendoPeloAviso),
            "Dentro da antecedência de aviso de cada tipo de documento."),
        new(IndicadoresPessoas.ComPendencia, "Com pendência cadastral", Alerta: false, Sim(CamposFiltroPessoas.ComPendenciaCadastral),
            "Sem CPF/CNPJ, sem endereço, com município a corrigir ou contribuinte do ICMS sem IE.")
    ];

    private static CondicaoFiltro Sim(string campo) => new() { Campo = campo, Operador = OperadorFiltro.Sim };

    /// <summary>Cópia da condição (as definições são compartilhadas: quem recebe pode mexer na sua).</summary>
    public static CondicaoFiltro Copia(CondicaoFiltro c) => new() { Campo = c.Campo, Operador = c.Operador, Valores = [.. c.Valores] };
}
