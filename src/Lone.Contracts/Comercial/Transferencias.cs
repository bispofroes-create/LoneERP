using Lone.Domain.Enums;

namespace Lone.Contracts.Comercial;

/// <summary>
/// Pedido de transferência de carteira (Motor Comercial, Fase 1d), igual na prévia e na execução: "a partir de
/// <see cref="EfeitoEm"/>, os clientes de <see cref="OrigemId"/> (no papel e na empresa escolhidos) passam para o destino".
/// </summary>
public sealed class TransferenciaRequisicao
{
    public Guid OrigemId { get; set; }

    /// <summary>Só este papel comercial (nulo = todos os papéis da origem).</summary>
    public Guid? TipoCarteiraId { get; set; }

    /// <summary>Só os vínculos desta empresa (nulo = de todas).</summary>
    public Guid? EmpresaId { get; set; }

    /// <summary>Primeiro dia do destino (a origem termina na véspera).</summary>
    public DateOnly EfeitoEm { get; set; }

    /// <summary>Um destino, ou vários: os clientes são divididos pela menor carteira (e podem ser trocados em <see cref="DestinoPorCliente"/>).</summary>
    public List<Guid> Destinos { get; set; } = new();

    public string? Motivo { get; set; }
    public string? Observacao { get; set; }

    /// <summary>Só estes clientes (os marcados na prévia). Nulo = todos os que o filtro alcança.</summary>
    public List<Guid>? Clientes { get; set; }

    /// <summary>Destino escolhido na prévia para um cliente (precisa ser um dos <see cref="Destinos"/>).</summary>
    public Dictionary<Guid, Guid>? DestinoPorCliente { get; set; }
}

/// <summary>Um vínculo na prévia ou no resultado: o cliente, o papel, quem atendia, para quem vai e o que acontece.</summary>
public sealed class ItemTransferenciaDto
{
    public Guid ClienteId { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public Guid? TipoCarteiraId { get; set; }
    public string? Papel { get; set; }
    public string? Empresa { get; set; }
    public Guid? DestinoId { get; set; }
    public string? Destino { get; set; }

    /// <summary>Período do vínculo da origem como estava (início e fim planejado).</summary>
    public DateOnly? InicioOrigem { get; set; }
    public DateOnly? FimOrigem { get; set; }

    /// <summary>Na prévia, "Transferido" quer dizer "será transferido".</summary>
    public ResultadoItemTransferencia Resultado { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>Quantos clientes vão para cada destino.</summary>
public sealed record DestinoTransferenciaDto(Guid DestinoId, string Destino, int Clientes);

/// <summary>Prévia da transferência: nada foi gravado.</summary>
public sealed class PreviaTransferenciaDto
{
    /// <summary>Clientes com algum vínculo a transferir.</summary>
    public int Transferir { get; set; }

    /// <summary>Clientes em que nada será transferido (com o motivo em cada item).</summary>
    public int NaoProcessar { get; set; }

    public List<DestinoTransferenciaDto> PorDestino { get; set; } = new();

    /// <summary>Avisos que não impedem (efeito no passado, ausências da origem...).</summary>
    public List<string> Avisos { get; set; } = new();

    public List<ItemTransferenciaDto> Itens { get; set; } = new();
}

/// <summary>Uma transferência gravada, com o resultado por cliente (quando pedida por Id).</summary>
public sealed class TransferenciaDto
{
    public Guid Id { get; set; }

    /// <summary>"TR-2026-0001".</summary>
    public string Numero { get; set; } = string.Empty;
    public DateOnly EfeitoEm { get; set; }
    public Guid OrigemId { get; set; }
    public string? Origem { get; set; }
    public Guid? TipoCarteiraId { get; set; }
    public string? Papel { get; set; }
    public Guid? EmpresaId { get; set; }
    public string? Empresa { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public DateTime CriadaEm { get; set; }
    public int Transferidos { get; set; }
    public int NaoProcessados { get; set; }
    public int Erros { get; set; }

    /// <summary>Falso se a gravação foi interrompida antes de registrar o resultado.</summary>
    public bool Concluida { get; set; }

    /// <summary>Vazio na lista; preenchido ao abrir uma transferência.</summary>
    public List<ItemTransferenciaDto> Itens { get; set; } = new();
}
