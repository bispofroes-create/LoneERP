namespace Lone.Domain.Enums;

/// <summary>
/// Situação de um território (Fase 2b). Encerrado nunca é apagado: continua na árvore histórica (posições) e nos documentos
/// que o citam. Gravado no banco: valores novos entram no fim.
/// </summary>
public enum SituacaoTerritorio : byte
{
    /// <summary>Faz parte da árvore de hoje.</summary>
    Ativo = 0,

    /// <summary>Saiu da árvore: <c>Territorio.FimEm</c> é o último dia em que existiu.</summary>
    Encerrado = 1
}

// ---------------------------------------------------------------------------------------------------------------------
// Fase 2b-1b — operações territoriais, regras, exceções e atribuições. Todos gravados no banco: valores novos entram no fim.
// ---------------------------------------------------------------------------------------------------------------------

/// <summary>
/// Situação da operação territorial TE- (DN-04: só estados de negócio). "Agendada", "Em vigor" e "Simulação desatualizada"
/// são indicadores calculados, nunca estados: nada no banco depende do calendário para ficar coerente.
/// </summary>
public enum SituacaoOperacaoTerritorial : byte
{
    /// <summary>Mudanças sendo planejadas; editável.</summary>
    Rascunho = 0,

    /// <summary>Tem simulação atual; editar uma mudança volta a Rascunho.</summary>
    Simulada = 1,

    /// <summary>Gravou as linhas de fato "a partir de" EfeitoEm. Nunca mais muda (só pode ser desfeita, DN-05).</summary>
    Aplicada = 2,

    /// <summary>Encerrada sem efeito, com motivo. O número fica.</summary>
    Cancelada = 3,

    /// <summary>Aplicada com efeito futuro e desfeita antes da data (T15/DN-05): as linhas dela ficaram anuladas.</summary>
    Desfeita = 4
}

/// <summary>O que uma mudança planejada faz (seção G do plano). Atribuição nunca é mudança direta: é resultado do motor.</summary>
public enum TipoMudancaTerritorial : byte
{
    /// <summary>Nova versão da regra do território (critérios e prioridade).</summary>
    NovaVersaoRegra = 1,

    /// <summary>Encerra a regra vigente do território na véspera do efeito.</summary>
    EncerrarRegra = 2,

    /// <summary>Fixar o cliente no território (exceção).</summary>
    Fixar = 3,

    /// <summary>Retirar o cliente do território (exceção).</summary>
    Retirar = 4,

    /// <summary>Encerra uma exceção vigente na véspera do efeito.</summary>
    EncerrarExcecao = 5,

    /// <summary>Move o território (com uso) para outro pai. Só efeito até hoje (DN-01).</summary>
    MoverTerritorio = 6,

    /// <summary>Encerra o território (com uso). Só efeito até hoje (DN-01).</summary>
    EncerrarTerritorio = 7,

    /// <summary>Reativa o território (com uso). Só efeito até hoje (DN-01).</summary>
    ReativarTerritorio = 8
}

/// <summary>Exceção individual de um cliente num território (T2).</summary>
public enum TipoExcecaoTerritorio : byte
{
    Fixar = 1,
    Retirar = 2
}

/// <summary>De onde veio a exceção (T2).</summary>
public enum OrigemExcecaoTerritorio : byte
{
    Manual = 1,
    Divergencia = 2,
    Importacao = 3
}

/// <summary>Por que o cliente está no território: pela regra (e a versão dela) ou por uma exceção Fixar.</summary>
public enum OrigemAtribuicaoTerritorio : byte
{
    Regra = 1,
    Excecao = 2
}

/// <summary>O que o motor decidiu para um cliente num mapa (algoritmo T3).</summary>
public enum ResultadoAtribuicao : byte
{
    /// <summary>Nenhum território (sem candidato).</summary>
    SemTerritorio = 0,

    /// <summary>Um território venceu (ou, no mapa não exclusivo, o cliente está em um ou mais territórios).</summary>
    Atribuido = 1,

    /// <summary>Empate real no mapa exclusivo e a atribuição atual não é uma das empatadas: fica sem território.</summary>
    Conflito = 2,

    /// <summary>Empate real, mas a atribuição atual é uma das empatadas: é mantida (manter não é escolher).</summary>
    PermaneceEmConflito = 3,

    /// <summary>Duas ou mais fixações vigentes no mapa exclusivo. Inconsistência: bloqueia a aplicação.</summary>
    ConflitoDeFixacao = 4,

    /// <summary>Fixação em território encerrado/inexistente em D, ou Fixar e Retirar do mesmo território. Bloqueia a aplicação.</summary>
    FixacaoInvalida = 5,

    /// <summary>Cliente fora do universo do mapa.</summary>
    ForaDoUniverso = 6
}

/// <summary>Em que passo do T3 o resultado foi decidido.</summary>
public enum PassoDecisaoTerritorial : byte
{
    Nenhum = 0,
    Universo = 1,
    UnicoCandidato = 2,
    Fixacao = 3,
    Prioridade = 4,
    Especificidade = 5,
    NaoExclusivo = 6,
    Conflito = 7,
    Inconsistencia = 8
}

/// <summary>A situação de cada território envolvido na decisão de um cliente ("por que este? por que não aquele?").</summary>
public enum EstadoCandidatoTerritorial : byte
{
    Vencedor = 1,
    RetiradoPorExcecao = 2,
    PerdeuParaExcecao = 3,
    PerdeuPorPrioridade = 4,
    PerdeuPorEspecificidade = 5,
    Empatado = 6,
    FixacaoEmConflito = 7,
    FixacaoInvalida = 8
}

/// <summary>O que muda para um cliente quando o resultado do motor é comparado com as atribuições gravadas.</summary>
public enum EfeitoNoCliente : byte
{
    /// <summary>Nada muda.</summary>
    Permanece = 0,

    /// <summary>Não tinha território e passa a ter.</summary>
    Entra = 1,

    /// <summary>Tinha e fica sem.</summary>
    Sai = 2,

    /// <summary>Troca de território.</summary>
    Muda = 3,

    /// <summary>Mesmo território, mas a origem muda (outra versão da regra, ou regra ↔ exceção): reabre citando a nova.</summary>
    OrigemAtualizada = 4,

    /// <summary>Inconsistência (conflito de fixação, fixação inválida): nada é gravado e a aplicação é bloqueada.</summary>
    Bloqueado = 5
}

/// <summary>Se o efeito sobre o cliente vem das mudanças desta operação ou de uma divergência que já existia (DN-14).</summary>
public enum OrigemEfeitoSimulado : byte
{
    EstaOperacao = 1,
    DivergenciaExistente = 2
}

/// <summary>Tabela de uma linha fechada por uma operação (para o desfazer exato, T15).</summary>
public enum TabelaFechamentoTerritorial : byte
{
    Regra = 1,
    Excecao = 2,
    Posicao = 3,
    Atribuicao = 4,
    Responsavel = 5
}
