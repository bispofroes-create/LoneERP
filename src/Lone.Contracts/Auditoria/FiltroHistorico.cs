using System.Globalization;

namespace Lone.Contracts.Auditoria;

/// <summary>
/// Filtro do histórico de um cadastro (03/10/2026): tipo (a parte do cadastro que mudou, ex.: "Endereco"), período e
/// quem alterou. Vazio = tudo. Datas em UTC: o aplicativo converte o dia local em instantes (fim exclusivo).
/// </summary>
public sealed class FiltroHistorico
{
    /// <summary>Partes do cadastro (o <see cref="RegistroHistorico.Entidade"/>); vazio = todas.</summary>
    public List<string> Entidades { get; set; } = new();

    /// <summary>A partir deste instante (UTC), inclusive.</summary>
    public DateTime? DeUtc { get; set; }

    /// <summary>Antes deste instante (UTC), exclusivo.</summary>
    public DateTime? AteUtc { get; set; }

    /// <summary>Nome do usuário exatamente como aparece no histórico; nulo = todos.</summary>
    public string? Usuario { get; set; }

    public bool Vazio => Entidades.Count == 0 && DeUtc is null && AteUtc is null && string.IsNullOrWhiteSpace(Usuario);

    /// <summary>Parâmetros da URL (entidade=...&amp;de=...&amp;ate=...&amp;usuario=...), começando com "&amp;"; vazio sem filtro.</summary>
    public string ParaConsulta()
    {
        var partes = new List<string>();
        partes.AddRange(Entidades.Select(e => "entidade=" + Uri.EscapeDataString(e)));
        if (DeUtc is { } de) partes.Add("de=" + Uri.EscapeDataString(Utc(de)));
        if (AteUtc is { } ate) partes.Add("ate=" + Uri.EscapeDataString(Utc(ate)));
        if (!string.IsNullOrWhiteSpace(Usuario)) partes.Add("usuario=" + Uri.EscapeDataString(Usuario.Trim()));
        return partes.Count == 0 ? string.Empty : "&" + string.Join("&", partes);
    }

    private static string Utc(DateTime d) =>
        DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}

/// <summary>Valores que existem no histórico de um cadastro, para as listas do filtro.</summary>
public sealed class OpcoesHistorico
{
    /// <summary>Partes do cadastro que têm registro (o texto vem de <c>Lone.Domain.Auditoria.DescritorCampos.Entidade</c>).</summary>
    public List<string> Entidades { get; set; } = new();

    /// <summary>Quem já alterou o cadastro.</summary>
    public List<string> Usuarios { get; set; } = new();
}
