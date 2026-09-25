using System.ComponentModel;
using System.Globalization;
using Lone.Domain.Auditoria;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Subclasse da CNAE 2.3 (tabela oficial do IBGE; Id = código de 7 dígitos). Carregada do IBGE; nunca apagada:
/// código que sai da lista oficial é desativado.
/// </summary>
[NaoAuditar]
public class Cnae
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime AtualizadoEm { get; set; }

    /// <summary>"0111-3/01".</summary>
    public static string Formatar(int codigo)
    {
        var t = codigo.ToString("0000000", CultureInfo.InvariantCulture);
        return $"{t[..4]}-{t[4]}/{t[5..]}";
    }

    /// <summary>"0111-3/01", "0111301" → 111301; nulo se não tiver 7 dígitos.</summary>
    public static int? Codigo(string? texto)
    {
        var digitos = new string((texto ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digitos.Length == 7 ? int.Parse(digitos, CultureInfo.InvariantCulture) : null;
    }
}

/// <summary>
/// CNAE do estabelecimento em tabela (principal e secundários), para filtros e relatórios com índice. É uma cópia
/// mantida pela API a partir dos campos de texto do estabelecimento (que continuam sendo a fonte), por isso não
/// entra no histórico (o texto já entra).
/// </summary>
[NaoAuditar]
public class EstabelecimentoCnae : EntidadePessoaFilha
{
    public Guid EstabelecimentoId { get; set; }
    public int Codigo { get; set; }
    public bool Principal { get; set; }
}

/// <summary>
/// Situação fiscal do estabelecimento num período (regime, contribuinte do ICMS, IE, situação na Receita, produtor
/// rural). Mantido pela API: cada mudança encerra o período aberto e abre outro a partir da data da mudança.
/// Nunca é apagado. Responde "qual era o regime deste cliente em março?" (notas antigas, apuração).
/// </summary>
[DisplayName("Histórico fiscal")]
public class HistoricoFiscal : EntidadePessoaFilha
{
    [DisplayName("Estabelecimento")]
    public Guid EstabelecimentoId { get; set; }

    [DisplayName("Início")]
    public DateOnly InicioEm { get; set; }

    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Regime tributário")]
    public RegimeTributario RegimeTributario { get; set; }

    [DisplayName("Contribuinte do ICMS")]
    public IndicadorIE IndicadorIE { get; set; }

    [DisplayName("Inscrição estadual")]
    public string? InscricaoEstadual { get; set; }

    [DisplayName("Situação na Receita")]
    public string? SituacaoReceita { get; set; }

    [DisplayName("Produtor rural")]
    public bool ProdutorRural { get; set; }

    public HistoricoFiscal Copia() => (HistoricoFiscal)MemberwiseClone();

    public bool MesmaSituacao(HistoricoFiscal outro) =>
        RegimeTributario == outro.RegimeTributario && IndicadorIE == outro.IndicadorIE &&
        InscricaoEstadual == outro.InscricaoEstadual && SituacaoReceita == outro.SituacaoReceita && ProdutorRural == outro.ProdutorRural;
}
