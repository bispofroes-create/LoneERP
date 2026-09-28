using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Tipo de ausência de quem atende a carteira (férias, folga, licença, afastamento, treinamento...). Nunca é excluído:
/// desativado, some das escolhas novas mas continua nas coberturas gravadas.
/// </summary>
[DisplayName("Tipo de ausência")]
public class TipoAusencia : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 40;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Tipo de ausência '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Tipo de ausência '{Nome}' reativado.");
    }
}

/// <summary>
/// Parâmetros do módulo Comercial (um registro só, <see cref="IdUnico"/>): antecedência do aviso de fim dos vínculos da
/// carteira e a regra padrão do crédito durante as ausências. Cada cobertura pode ter a sua regra.
/// </summary>
[DisplayName("Parâmetros comerciais")]
public class ParametrosComerciais : AgregadoRaiz
{
    public static readonly Guid IdUnico = new("7a9e1c06-0000-0000-0000-000000000001");
    public const int MaximoDiasAviso = 365;
    public const int MaximoDiasRetroativos = 365;

    /// <summary>Com quantos dias de antecedência o fim de um vínculo da carteira é destacado (e entra em "Carteira vencendo").</summary>
    [DisplayName("Aviso de fim do vínculo (dias)")]
    public int DiasAvisoFimVinculo { get; set; } = 30;

    /// <summary>
    /// Até quantos dias antes de hoje uma transferência de carteira pode ter efeito, ou uma cobertura pode começar
    /// (decisões T1 e T6 da Fase 1d). Mais para trás reescreveria períodos que podem já ter sido apurados (metas e, no
    /// futuro, comissões). 0 = só hoje ou datas futuras.
    /// </summary>
    [DisplayName("Datas no passado: até (dias)")]
    public int DiasRetroativosMaximo { get; set; } = 30;

    /// <summary>Regra sugerida para o crédito das vendas durante uma ausência coberta (decisão MC-9: titular).</summary>
    [DisplayName("Crédito durante a ausência")]
    public RegraCreditoAusencia CreditoNaAusencia { get; set; } = RegraCreditoAusencia.Titular;

    /// <summary>Percentual sugerido para quem cobre quando a regra é "Dividido".</summary>
    [DisplayName("Percentual de quem cobre (%)")]
    public decimal? PercentualSubstitutoPadrao { get; set; }
}

/// <summary>
/// Cobertura de uma ausência (Motor Comercial, Fase 1c): enquanto o titular está ausente (férias, licença...), outra
/// pessoa ou uma equipe atende os clientes da carteira dele, no escopo escolhido (todos os papéis ou um; todas as
/// empresas ou uma). A carteira não muda: a cobertura vale pelas datas e acaba sozinha no fim. Nunca é apagada:
/// cancelada (se ainda não começou) ou encerrada (mudando o fim).
/// </summary>
[DisplayName("Cobertura de ausência")]
public class CoberturaComercial : AgregadoRaiz
{
    public const int TamanhoMaximoTexto = 250;

    /// <summary>Quem vai se ausentar (a pessoa da carteira).</summary>
    [DisplayName("Titular")]
    public Guid TitularId { get; set; }

    [DisplayName("Tipo de ausência")]
    public Guid TipoAusenciaId { get; set; }

    [DisplayName("Início")]
    public DateOnly InicioEm { get; set; }

    /// <summary>Obrigatório: ausência sem fim é transferência de carteira (encerrar e incluir outro).</summary>
    [DisplayName("Fim")]
    public DateOnly FimEm { get; set; }

    /// <summary>Quem cobre (uma pessoa) — ou a equipe, nunca os dois.</summary>
    [DisplayName("Quem cobre")]
    public Guid? SubstitutoId { get; set; }

    [DisplayName("Equipe que cobre")]
    public Guid? EquipeSubstitutaId { get; set; }

    /// <summary>Só este papel comercial (nulo = todos os papéis do titular).</summary>
    [DisplayName("Papel comercial")]
    public Guid? TipoCarteiraId { get; set; }

    /// <summary>Só esta empresa do grupo (nulo = todas).</summary>
    [DisplayName("Empresa")]
    public Guid? EmpresaId { get; set; }

    [DisplayName("Crédito das vendas")]
    public RegraCreditoAusencia RegraCredito { get; set; }

    /// <summary>Percentual de quem cobre, só quando a regra é "Dividido".</summary>
    [DisplayName("Percentual de quem cobre (%)")]
    public decimal? PercentualSubstituto { get; set; }

    /// <summary>Quem cobre pode ver e atender os clientes do titular (a restrição por carteira entra na Fase 2).</summary>
    [DisplayName("Quem cobre acessa os clientes")]
    public bool PermiteAcesso { get; set; } = true;

    [DisplayName("Observação")]
    public string? Observacao { get; set; }

    [DisplayName("Cancelada")]
    public bool Cancelada { get; set; }

    [DisplayName("Motivo do cancelamento")]
    public string? MotivoCancelamento { get; set; }

    public bool Vigente(DateOnly data) => !Cancelada && InicioEm <= data && FimEm >= data;

    public SituacaoCobertura Situacao(DateOnly hoje) =>
        Cancelada ? SituacaoCobertura.Cancelada
        : InicioEm > hoje ? SituacaoCobertura.Agendada
        : FimEm < hoje ? SituacaoCobertura.Encerrada
        : SituacaoCobertura.Vigente;
}
