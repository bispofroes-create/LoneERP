using System.ComponentModel;

namespace Lone.Core.Entidades;

/// <summary>Papel de cliente: dados comerciais e de crédito.</summary>
[DisplayName("Cliente")]
public class Cliente : PapelBase
{
    [DisplayName("Limite de crédito")]
    public decimal? LimiteCredito { get; set; }

    /// <summary>Texto livre por enquanto; vira cadastro próprio quando existir o financeiro.</summary>
    [DisplayName("Condição de pagamento")]
    public string? CondicaoPagamento { get; set; }

    /// <summary>Bloqueio manual para vendas (vira a entidade Bloqueio na etapa 3).</summary>
    [DisplayName("Bloqueado")]
    public bool Bloqueado { get; set; }

    [DisplayName("Motivo do bloqueio")]
    public string? MotivoBloqueio { get; set; }

    [DisplayName("Bloqueado em")]
    public DateTime? BloqueadoEm { get; set; }

    /// <summary>
    /// Regra do bloqueio: ao bloquear, registra a data; se já estava bloqueado, mantém a data original;
    /// ao desbloquear, limpa data e motivo.
    /// </summary>
    public void AjustarBloqueio(Cliente? anterior, DateTime agora)
    {
        if (!Bloqueado)
        {
            BloqueadoEm = null;
            MotivoBloqueio = null;
        }
        else
        {
            BloqueadoEm = anterior is { Bloqueado: true } ? anterior.BloqueadoEm : agora;
        }
    }
}
