namespace Lone.Domain.Enums;

public enum EscopoBloqueio : byte
{
    Comercial = 0,
    Financeiro = 1,
    Cadastral = 2,

    /// <summary>Impede emitir nota/faturar para a pessoa (pedidos ainda podem ser digitados).</summary>
    Faturamento = 3
}

/// <summary>Tipo de interação com a pessoa (relacionamento). Gravado no banco: tipos novos no fim.</summary>
public enum TipoInteracao : byte
{
    Ligacao = 0,
    Visita = 1,
    Email = 2,
    WhatsApp = 3,
    Reuniao = 4,
    Outro = 9
}

/// <summary>Situação do relacionamento, calculada pelos dias sem interação (regras em ParametrosRelacionamento).</summary>
public enum SituacaoRelacionamento : byte
{
    SemInteracao = 0,
    Ativo = 1,
    EmRisco = 2,
    Inativo = 3
}

public enum OrigemBloqueio : byte
{
    Manual = 0,
    Automatico = 1
}
