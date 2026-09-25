namespace Lone.Domain.Enums;

/// <summary>De onde vem o realizado de um indicador. Gravado no banco: fontes novas (ex.: vendas) entram no fim.</summary>
public enum FonteIndicador : byte
{
    /// <summary>Informado à mão ou importado (CSV), com auditoria.</summary>
    Informado = 0,

    /// <summary>Pessoas que ganharam o papel Cliente no período.</summary>
    NovosClientes = 1,

    /// <summary>Clientes com o papel ativo no fim do período.</summary>
    ClientesAtivos = 2,

    /// <summary>Clientes que voltaram a ser clientes no período (já tinham tido o papel antes).</summary>
    ClientesReativados = 3,

    /// <summary>Interações registradas com os clientes no período.</summary>
    InteracoesRegistradas = 4
}

public enum UnidadeIndicador : byte
{
    Quantidade = 0,
    Moeda = 1,
    Percentual = 2
}

/// <summary>Maior é melhor (vendas) ou menor é melhor (devoluções, prazo).</summary>
public enum SentidoIndicador : byte
{
    MaiorMelhor = 0,
    MenorMelhor = 1
}

public enum SituacaoMeta : byte
{
    Rascunho = 0,

    /// <summary>Alvos, pesos e faixas travados; participantes acompanham.</summary>
    Publicada = 1,

    /// <summary>Período encerrado: conferência do realizado antes do fechamento.</summary>
    EmApuracao = 2,

    /// <summary>Aprovada: resultado congelado (base para comissões).</summary>
    Fechada = 3
}

/// <summary>Nível da hierarquia a que um participante pertence.</summary>
public enum NivelParticipante : byte
{
    Empresa = 0,
    Filial = 1,
    Departamento = 2,
    Equipe = 3,
    Colaborador = 4
}

/// <summary>Como o realizado de um participante entrou.</summary>
public enum OrigemRealizado : byte
{
    Calculado = 0,
    Informado = 1,
    Importado = 2
}
