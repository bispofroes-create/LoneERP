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
