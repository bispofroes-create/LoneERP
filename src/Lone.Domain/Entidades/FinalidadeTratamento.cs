using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Finalidade de tratamento (LGPD): para quê a pessoa é contatada (ex.: Marketing). Mesmo padrão das finalidades de
/// endereço: o sistema trabalha com o Id; regra que precisa de uma específica usa o Código, que não muda. Nunca é
/// excluída: desativada, some das escolhas novas e continua no histórico. As de sistema não mudam de código nem são
/// desativadas. <see cref="SomenteHistorico"/> marca a finalidade que só guarda registros antigos ("Registro anterior"):
/// ela não é oferecida, não se concede, não se revoga e não autoriza comunicação.
/// </summary>
[DisplayName("Finalidade de tratamento")]
public class FinalidadeTratamento : AgregadoRaiz
{
    public const int TamanhoMaximoCodigo = 30;
    public const int TamanhoMaximoNome = 60;
    public const int TamanhoMaximoDescricao = 250;

    [DisplayName("Código")]
    public string Codigo { get; set; } = string.Empty;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    [DisplayName("Base legal")]
    public BaseLegal BaseLegal { get; set; } = BaseLegal.Consentimento;

    /// <summary>Classificação que o canal precisa ter para ser usado nesta finalidade (Nenhuma = qualquer canal).</summary>
    [DisplayName("Classificação exigida do canal")]
    public ClassificacaoCanal ClassificacaoExigida { get; set; }

    [DisplayName("Somente histórico")]
    public bool SomenteHistorico { get; set; }

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("De sistema")]
    public bool DoSistema { get; set; }

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;

    /// <summary>Pode ser escolhida para conceder consentimento (ativa, não é só histórico, base legal consentimento).</summary>
    public bool AceitaConsentimento => Ativo && !SomenteHistorico && BaseLegal == BaseLegal.Consentimento;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Finalidade de tratamento '{Nome}' desativada.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Finalidade de tratamento '{Nome}' reativada.");
    }
}
