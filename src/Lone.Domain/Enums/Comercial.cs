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

/// <summary>
/// Resultado de cada vínculo numa transferência de carteira (Motor Comercial, Fase 1d). Gravado no banco: valores novos
/// entram no fim.
/// </summary>
public enum ResultadoItemTransferencia : byte
{
    /// <summary>Vínculo da origem encerrado na véspera do efeito e vínculo do destino aberto no efeito (na prévia: "será transferido").</summary>
    Transferido = 0,

    /// <summary>Não entrou (regra da carteira, vínculo futuro, destino que já atende...): o motivo fica no item.</summary>
    NaoProcessado = 1,

    /// <summary>Falhou ao gravar (conflito de edição, cadastro que não existe mais): o motivo fica no item.</summary>
    Erro = 2
}

/// <summary>
/// Até onde o usuário enxerga no cadastro de Pessoas e no Comercial (Motor Comercial, Fase 2a; decisão F2). Fica no perfil;
/// o administrador vê tudo. Gravado no banco: valores novos entram no fim (ex.: "Meus territórios" na Fase 2b).
/// </summary>
public enum AlcanceComercial : byte
{
    /// <summary>Toda a base (o comportamento de antes; padrão, para ninguém perder acesso).</summary>
    Tudo = 0,

    /// <summary>A carteira de quem está nas equipes que o usuário lidera e nas equipes abaixo delas, mais a dele.</summary>
    MinhaEquipe = 1,

    /// <summary>Só os clientes da carteira da pessoa do usuário (e os que ele cobre numa ausência).</summary>
    MinhaCarteira = 2,

    /// <summary>Nenhum cadastro de Pessoas (perfis só de configuração ou de outros módulos).</summary>
    Nenhum = 3
}
