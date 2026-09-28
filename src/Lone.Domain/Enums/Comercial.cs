namespace Lone.Domain.Enums;

/// <summary>
/// Como os vínculos de um papel comercial (tipo de carteira) entram no crédito da venda (Motor Comercial, Fase 1).
/// Gravado no banco: valores novos entram no fim.
/// </summary>
public enum TipoCreditoComercial : byte
{
    /// <summary>Atende o cliente, mas não recebe crédito da venda (ex.: televendas de apoio).</summary>
    Nenhum = 0,

    /// <summary>Entra na divisão do crédito de receita, que soma 100% por empresa e data (ex.: vendedor 70%, representante 30%).</summary>
    Receita = 1,

    /// <summary>Crédito extra, fora dos 100% (ex.: supervisor ou especialista com 5%). Sem percentual, vale 100%.</summary>
    Sobreposicao = 2
}

/// <summary>Como o vínculo da carteira foi criado. Gravado no banco: valores novos entram no fim.</summary>
public enum OrigemVinculoCarteira : byte
{
    /// <summary>Incluído na ficha do cliente.</summary>
    Manual = 0,

    /// <summary>Incluído na ficha no lugar de outro, que foi encerrado na véspera (confirmação "Encerrar anterior...").</summary>
    Substituicao = 1,

    /// <summary>Transferência de carteira (Fase 1d).</summary>
    Transferencia = 2,

    /// <summary>Distribuição automática (Fase 2).</summary>
    Distribuicao = 3,

    /// <summary>Importação de arquivo.</summary>
    Importacao = 4
}

/// <summary>
/// Quem fica com o crédito das vendas feitas durante uma ausência coberta (Motor Comercial, Fase 1c). Gravado no banco:
/// valores novos entram no fim.
/// </summary>
public enum RegraCreditoAusencia : byte
{
    /// <summary>O crédito continua do titular (comum em férias e folgas): a carteira é dele.</summary>
    Titular = 0,

    /// <summary>O crédito vai para quem cobre (comum em licença longa).</summary>
    Substituto = 1,

    /// <summary>Dividido: o percentual informado para quem cobre, o resto para o titular.</summary>
    Dividido = 2
}

/// <summary>Situação de uma cobertura numa data (calculada; não é gravada).</summary>
public enum SituacaoCobertura : byte
{
    Agendada = 0,
    Vigente = 1,
    Encerrada = 2,
    Cancelada = 3
}
