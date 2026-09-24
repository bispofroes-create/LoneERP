using System.Globalization;

namespace Lone.Domain.CamposPersonalizados;

/// <summary>Texto legível de um valor, igual para qualquer tipo (usado pela auditoria).</summary>
public static class ValorCampo
{
    public const string PrefixoOpcao = "opcao:";

    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    public static string? Descrever(string? texto, decimal? numero, DateTime? data, bool? logico, Guid? opcaoId)
    {
        if (opcaoId is { } opcao) return PrefixoOpcao + opcao; // trocado pelo texto da opção na consulta
        if (logico is { } sim) return sim ? "Sim" : "Não";
        if (numero is { } n) return n.ToString("#,0.######", Brasil);
        if (data is { } d) return d.TimeOfDay == TimeSpan.Zero ? d.ToString("dd/MM/yyyy", Brasil) : d.ToString("dd/MM/yyyy HH:mm", Brasil);
        return texto;
    }
}
