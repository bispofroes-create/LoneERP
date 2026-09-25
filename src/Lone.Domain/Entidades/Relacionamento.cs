using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Interação com a pessoa (ligação, visita, e-mail...): quem, quando, o quê. Só inclusão: não se altera nem apaga
/// (uma correção é outra interação). A mais recente define a situação do relacionamento.
/// </summary>
[DisplayName("Interação")]
public class Interacao : EntidadePessoaFilha
{
    public const int TamanhoMaximoDescricao = 1000;

    [DisplayName("Data e hora")]
    public DateTime DataHora { get; set; }

    [DisplayName("Tipo")]
    public TipoInteracao Tipo { get; set; }

    [DisplayName("Descrição")]
    public string Descricao { get; set; } = string.Empty;

    [DisplayName("Registrada por")]
    public string Usuario { get; set; } = string.Empty;
}

/// <summary>
/// Regras de inatividade do relacionamento (uma linha só, Id fixo): quantos dias sem interação deixam a pessoa
/// "em risco" e "inativa". A situação é calculada na leitura, nunca gravada.
/// </summary>
[DisplayName("Parâmetros de relacionamento")]
public class ParametrosRelacionamento : AgregadoRaiz
{
    public static readonly Guid IdUnico = new("7a9e1c05-0000-0000-0000-000000000001");
    public const int DiasEmRiscoPadrao = 90;
    public const int DiasInativoPadrao = 180;

    [DisplayName("Dias sem interação para \"em risco\"")]
    public int DiasEmRisco { get; set; } = DiasEmRiscoPadrao;

    [DisplayName("Dias sem interação para \"inativo\"")]
    public int DiasInativo { get; set; } = DiasInativoPadrao;

    public SituacaoRelacionamento Situacao(DateTime? ultimaInteracao, DateTime agora)
    {
        if (ultimaInteracao is not { } ultima) return SituacaoRelacionamento.SemInteracao;
        var dias = (agora.Date - ultima.Date).TotalDays;
        return dias >= DiasInativo ? SituacaoRelacionamento.Inativo
            : dias >= DiasEmRisco ? SituacaoRelacionamento.EmRisco
            : SituacaoRelacionamento.Ativo;
    }

    public List<string> Validar()
    {
        var erros = new List<string>();
        if (DiasEmRisco < 1 || DiasInativo < 1) erros.Add("Use dias maiores que zero.");
        if (DiasInativo <= DiasEmRisco) erros.Add("Os dias para \"inativo\" precisam ser maiores que os dias para \"em risco\".");
        if (DiasInativo > 3650) erros.Add("Use no máximo 3.650 dias.");
        return erros;
    }
}
