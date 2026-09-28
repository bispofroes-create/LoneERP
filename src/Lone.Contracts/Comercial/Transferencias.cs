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

/// <summary>Opções do assistente de transferência numa chamada só (lida quando a tela abre).</summary>
public sealed class TransferenciaOpcoesDto
{
    /// <summary>Quem pode ser origem ou destino: pessoas que podem ocupar algum papel comercial ativo.</summary>
    public List<AtendenteOpcaoDto> Pessoas { get; set; } = new();

    /// <summary>Quantos clientes cada pessoa atende hoje (vínculos ativos vigentes).</summary>
    public Dictionary<Guid, int> ClientesHoje { get; set; } = new();

    public List<TipoCarteiraDto> Papeis { get; set; } = new();
    public List<Lone.Contracts.Empresas.EmpresaResumo> Empresas { get; set; } = new();

    /// <summary>Até quantos dias antes de hoje vale a data de efeito (Parâmetros comerciais).</summary>
    public int DiasRetroativosMaximo { get; set; } = 30;
}

/// <summary>Um vínculo que valia na data consultada ("Carteira em uma data").</summary>
public sealed class VinculoEmDataDto
{
    public Guid VinculoId { get; set; }
    public Guid ClienteId { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public Guid TipoCarteiraId { get; set; }
    public string Papel { get; set; } = string.Empty;

    /// <summary>Quem atendia.</summary>
    public Guid PessoaId { get; set; }
    public string Pessoa { get; set; } = string.Empty;
    public string? Empresa { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public bool Exclusivo { get; set; }
    public OrigemVinculoCarteira Origem { get; set; }

    /// <summary>Número da transferência que criou o vínculo ("TR-2026-0001").</summary>
    public string? Transferencia { get; set; }

    public TipoCreditoComercial TipoCredito { get; set; }

    /// <summary>Percentual do crédito naquele dia (só na consulta por cliente); nulo = sem crédito ou divisão indefinida.</summary>
    public decimal? Credito { get; set; }

    /// <summary>Ausência de quem atendia que valia naquele dia, com quem cobria.</summary>
    public string? Cobertura { get; set; }
}

/// <summary>"Como estava a carteira em DD/MM": de um cliente (quem ocupava cada papel) ou de uma pessoa (quem ela atendia).</summary>
public sealed class CarteiraEmDataDto
{
    public DateOnly Data { get; set; }
    public Guid? ClienteId { get; set; }
    public Guid? PessoaId { get; set; }

    /// <summary>Nome do cliente ou da pessoa consultada.</summary>
    public string? Nome { get; set; }

    public List<VinculoEmDataDto> Vinculos { get; set; } = new();

    /// <summary>Na consulta por pessoa: as ausências dela que valiam naquele dia.</summary>
    public List<string> Ausencias { get; set; } = new();

    /// <summary>Verdadeiro quando a lista foi cortada no limite (pessoa com muitos clientes).</summary>
    public bool Cortada { get; set; }
}
