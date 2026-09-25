using System.ComponentModel;
using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>
/// Ocupação da CBO 2002 (Classificação Brasileira de Ocupações, Ministério do Trabalho). Tabela de referência,
/// preenchida só pela importação do arquivo oficial. O Id é o próprio código CBO de 6 dígitos (oficial e imutável),
/// pela mesma regra aprovada para os municípios do IBGE. Nunca é apagada: a que sai do arquivo é desativada.
/// </summary>
[DisplayName("Ocupação CBO"), NaoAuditar]
public class OcupacaoCbo
{
    public const int TamanhoMaximoTitulo = 200;

    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime AtualizadoEm { get; set; }

    /// <summary>Código no formato oficial "0000-00".</summary>
    public string CodigoFormatado => Formatar(Id);

    public static string Formatar(int codigo)
    {
        var texto = codigo.ToString("000000", System.Globalization.CultureInfo.InvariantCulture);
        return $"{texto[..4]}-{texto[4..]}";
    }
}
