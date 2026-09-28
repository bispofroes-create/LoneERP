using System.ComponentModel;
using Lone.Domain.Auditoria;
using Lone.Domain.Comercial;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Transferência de carteira (Motor Comercial, Fase 1d): "a partir de 01/10/2026, os clientes de João no papel Vendedor
/// passam para Maria. Motivo: desligamento." É o registro do lote (quem, quando, por quê, filtro e resultado); cada
/// cliente é gravado na própria transação, e os vínculos novos apontam para ela (CarteiraCliente.TransferenciaId). Nunca
/// é apagada nem desfeita: uma transferência errada se corrige com outra, também registrada.
/// </summary>
[DisplayName("Transferência de carteira")]
public class TransferenciaCarteira : AgregadoRaiz
{
    public const int TamanhoMaximoTexto = 250;
    public const int TamanhoMaximoUsuario = 100;

    /// <summary>Ano e sequência do número legível ("TR-2026-0001"), únicos juntos.</summary>
    [DisplayName("Ano")]
    public int Ano { get; set; }

    [DisplayName("Sequência")]
    public int Sequencia { get; set; }

    /// <summary>Primeiro dia do destino: a origem termina na véspera.</summary>
    [DisplayName("Efeito")]
    public DateOnly EfeitoEm { get; set; }

    /// <summary>Quem atendia os clientes.</summary>
    [DisplayName("Origem")]
    public Guid OrigemId { get; set; }

    /// <summary>Só este papel comercial (nulo = todos os papéis da origem).</summary>
    [DisplayName("Papel comercial")]
    public Guid? TipoCarteiraId { get; set; }

    /// <summary>Só os vínculos desta empresa (nulo = de todas).</summary>
    [DisplayName("Empresa")]
    public Guid? EmpresaId { get; set; }

    [DisplayName("Motivo")]
    public string Motivo { get; set; } = string.Empty;

    [DisplayName("Observação")]
    public string? Observacao { get; set; }

    /// <summary>Nome de quem fez (a auditoria também registra; aqui para a lista das transferências).</summary>
    [DisplayName("Feita por")]
    public string Usuario { get; set; } = string.Empty;

    /// <summary>Clientes com algum vínculo transferido.</summary>
    [DisplayName("Transferidos")]
    public int Transferidos { get; set; }

    /// <summary>Clientes sem nenhum vínculo transferido (regra da carteira, vínculo futuro...), sem erro.</summary>
    [DisplayName("Não processados")]
    public int NaoProcessados { get; set; }

    /// <summary>Clientes que falharam ao gravar (conflito de edição...).</summary>
    [DisplayName("Com erro")]
    public int Erros { get; set; }

    /// <summary>Falso enquanto os clientes são gravados; verdadeiro quando o resultado foi registrado.</summary>
    [DisplayName("Concluída")]
    public bool Concluida { get; set; }

    public string Numero => RegrasTransferencia.Numero(Ano, Sequencia);
}

/// <summary>
/// Resultado de um vínculo numa transferência: transferido (com o vínculo novo), não processado ou com erro (com o
/// motivo). É o "resultado por cliente" que pode ser reaberto depois. Fica fora da auditoria campo a campo: o próprio
/// item é o registro, gravado uma vez e nunca alterado.
/// </summary>
[DisplayName("Item da transferência"), NaoAuditar]
public class TransferenciaCarteiraItem : EntidadeBase
{
    public const int TamanhoMaximoMotivo = 1000;

    public Guid TransferenciaId { get; set; }

    /// <summary>O cliente (a pessoa da carteira).</summary>
    public Guid ClienteId { get; set; }

    /// <summary>O vínculo da origem (encerrado na véspera, se transferido). Nulo quando o cliente nem pôde ser lido.</summary>
    public Guid? VinculoOrigemId { get; set; }

    /// <summary>O vínculo aberto para o destino (só quando transferido).</summary>
    public Guid? VinculoNovoId { get; set; }

    /// <summary>Quem passaria a atender (escolhido na prévia ou pela divisão).</summary>
    public Guid? DestinoId { get; set; }

    public Guid? TipoCarteiraId { get; set; }

    public Guid? EmpresaId { get; set; }

    /// <summary>Período do vínculo da origem antes da transferência (início e fim planejado; nulo = em aberto).</summary>
    public DateOnly? InicioOrigem { get; set; }

    public DateOnly? FimOrigem { get; set; }

    public ResultadoItemTransferencia Resultado { get; set; }

    public string? Motivo { get; set; }
}
