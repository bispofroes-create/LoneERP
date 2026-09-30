using System.ComponentModel;
using Lone.Domain.Auditoria;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

// Fase 2b-1b — o motor territorial. Três fatos oficiais (regra, exceção, atribuição), a operação TE- com as mudanças
// planejadas, as simulações (evidência imutável do que foi mostrado) e o que a aplicação fez, e três controles técnicos
// (parâmetros, motor do mapa, numerador). Nada é apagado: o que termina ganha fim; o que foi desfeito fica anulado.

/// <summary>
/// Parâmetros do motor territorial (um registro só, <see cref="IdUnico"/>; DN-08). Separados dos Parâmetros comerciais: a
/// retroatividade das operações territoriais é uma política própria, não a das coberturas.
/// </summary>
[DisplayName("Parâmetros territoriais")]
public class ParametrosTerritoriais : AgregadoRaiz
{
    public static readonly Guid IdUnico = new("7a9e1c08-0000-0000-0000-000000000001");
    public const int PadraoDiasRetroativos = 30;
    public const int MaximoDiasRetroativos = 365;

    /// <summary>
    /// Até quantos dias antes de hoje uma operação territorial pode ter efeito (T8). Nunca vale antes do último efeito
    /// aplicado no mapa, qualquer que seja este número. 0 = só hoje ou datas futuras (as futuras só para regras e exceções).
    /// </summary>
    [DisplayName("Operações territoriais: datas no passado até (dias)")]
    public int DiasRetroativosMaximo { get; set; } = PadraoDiasRetroativos;
}

/// <summary>
/// Controle do motor de um mapa (DN-02): uma linha por mapa, com a versão de tudo que muda o resultado da atribuição
/// (aplicação de operação, mudança estrutural da árvore, ativação/desativação do mapa). É a trava que serializa as
/// aplicações e a base das simulações. Separada da versão cadastral do mapa (nome e descrição não invalidam simulação) e
/// da trava da árvore (regra nova não é mudança de estrutura). Controle técnico: fora da auditoria.
/// </summary>
[NaoAuditar]
public class MapaTerritorialMotor
{
    public Guid MapaId { get; set; }

    /// <summary>rowversion: muda a cada alteração capaz de mudar o resultado do motor.</summary>
    public byte[]? Versao { get; set; }

    /// <summary>Última operação aplicada (e não desfeita) no mapa.</summary>
    public Guid? UltimaOperacaoId { get; set; }

    /// <summary>Efeito da última operação aplicada: nenhuma outra pode ter efeito anterior (T8, RT-1).</summary>
    public DateOnly? UltimoEfeitoEm { get; set; }

    public DateTime AtualizadoEm { get; set; }
}

/// <summary>Numerador de documentos por prefixo e ano (TE-; DN-13). Incremento atômico no banco, nunca MAX+1.</summary>
[NaoAuditar]
public class NumeracaoDocumento
{
    public const int TamanhoMaximoPrefixo = 10;

    public string Prefixo { get; set; } = string.Empty;
    public int Ano { get; set; }
    public int Ultimo { get; set; }
}

/// <summary>
/// Uma versão da regra do território (T6/T20): os grupos de inclusão e exclusão sobre o catálogo de filtros de Pessoas e a
/// prioridade. Publicada só por operação; imutável depois disso (só fim, anulação e os ids de encerramento mudam). Os
/// critérios ficam também em texto congelado, porque etiqueta e município podem ser renomeados depois.
/// </summary>
[DisplayName("Regra do território")]
public class RegraTerritorio : EntidadeBase, IParteDeAgregado
{
    public const int TamanhoMaximoGrupos = 20000;
    public const int TamanhoMaximoCriterios = 4000;

    public Guid MapaId { get; set; }
    public Guid TerritorioId { get; set; }

    /// <summary>v1, v2... por território.</summary>
    [DisplayName("Versão")]
    public int Numero { get; set; }

    /// <summary>Grupos de inclusão e de exclusão, em JSON (ids do catálogo e dos cadastros).</summary>
    [DisplayName("Critérios")]
    public string Grupos { get; set; } = string.Empty;

    /// <summary>Os mesmos critérios em texto, com os nomes do dia da publicação.</summary>
    [DisplayName("Critérios (texto)")]
    public string Criterios { get; set; } = string.Empty;

    /// <summary>1 = mais alta; nulo = sem prioridade (perde de qualquer número). Mudar = nova versão.</summary>
    [DisplayName("Prioridade")]
    public int? Prioridade { get; set; }

    [DisplayName("Início")]
    public DateOnly InicioEm { get; set; }

    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;

    public Guid OperacaoId { get; set; }
    public Guid? OperacaoMudancaId { get; set; }
    public Guid? OperacaoEncerramentoId { get; set; }
    public Guid? OperacaoAnulacaoId { get; set; }

    /// <summary>rowversion: a base que as mudanças planejadas citam.</summary>
    public byte[]? Versao { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Territorio);
    Guid IParteDeAgregado.RaizId => TerritorioId;

    public bool VigenteEm(DateOnly data) => Ativo && InicioEm <= data && (FimEm is null || FimEm >= data);
}

/// <summary>
/// Exceção de um cliente num território (T2): Fixar em / Retirar de, com vigência, motivo e origem. Nasce e termina antes
/// do fim só por operação. <see cref="Exclusivo"/> é cópia do mapa, amarrada por chave estrangeira (não muda com uso).
/// </summary>
[DisplayName("Exceção territorial")]
public class ExcecaoTerritorio : EntidadeBase, IParteDeAgregado
{
    public const int TamanhoMaximoMotivo = 250;

    public Guid MapaId { get; set; }

    [DisplayName("Mapa exclusivo")]
    public bool Exclusivo { get; set; }

    public Guid TerritorioId { get; set; }

    [DisplayName("Cliente")]
    public Guid PessoaId { get; set; }

    [DisplayName("Tipo")]
    public TipoExcecaoTerritorio Tipo { get; set; }

    [DisplayName("Início")]
    public DateOnly InicioEm { get; set; }

    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Motivo")]
    public string Motivo { get; set; } = string.Empty;

    [DisplayName("Origem")]
    public OrigemExcecaoTerritorio Origem { get; set; } = OrigemExcecaoTerritorio.Manual;

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;

    public Guid OperacaoId { get; set; }
    public Guid? OperacaoMudancaId { get; set; }
    public Guid? OperacaoEncerramentoId { get; set; }
    public Guid? OperacaoAnulacaoId { get; set; }

    public byte[]? Versao { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Territorio);
    Guid IParteDeAgregado.RaizId => TerritorioId;

    public bool VigenteEm(DateOnly data) => Ativo && InicioEm <= data && (FimEm is null || FimEm >= data);
}

/// <summary>
/// Atribuição: o resultado gravado do motor (cliente × território, com período), citando a versão da regra ou a exceção
/// que decidiu e a operação que a abriu. Nunca recalculada ao consultar. Fora da auditoria campo a campo: o item aplicado
/// da operação é o registro (antes, depois e explicação), e são milhares de linhas por operação.
/// </summary>
[NaoAuditar]
public class AtribuicaoTerritorio : EntidadeBase
{
    public Guid MapaId { get; set; }
    public bool Exclusivo { get; set; }
    public Guid TerritorioId { get; set; }
    public Guid PessoaId { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public OrigemAtribuicaoTerritorio Origem { get; set; }
    public Guid? RegraId { get; set; }
    public Guid? ExcecaoId { get; set; }
    public bool Ativo { get; set; } = true;

    public Guid OperacaoId { get; set; }
    public Guid? OperacaoEncerramentoId { get; set; }
    public Guid? OperacaoAnulacaoId { get; set; }

    public bool VigenteEm(DateOnly data) => Ativo && InicioEm <= data && (FimEm is null || FimEm >= data);
}

/// <summary>
/// Operação territorial TE- (T5, T17, DN-04): o pacote de mudanças de um mapa com data de efeito e motivo. Planejada em
/// rascunho, simulada (cada simulação guardada, a atual apontada aqui: DN-03), aplicada de uma vez (T11), cancelada ou
/// desfeita (T15/DN-05). Nunca é apagada.
/// </summary>
[DisplayName("Operação territorial")]
public class OperacaoTerritorial : AgregadoRaiz
{
    public const int TamanhoMaximoMotivo = 250;
    public const int TamanhoMaximoObservacao = 1000;
    public const int TamanhoMaximoUsuario = 100;
    public const string Prefixo = "TE";

    [DisplayName("Ano")]
    public int Ano { get; set; }

    [DisplayName("Sequência")]
    public int Sequencia { get; set; }

    [DisplayName("Mapa")]
    public Guid MapaId { get; set; }

    /// <summary>Primeiro dia em que as mudanças valem (o que termina, termina na véspera).</summary>
    [DisplayName("Efeito")]
    public DateOnly EfeitoEm { get; set; }

    [DisplayName("Motivo")]
    public string Motivo { get; set; } = string.Empty;

    [DisplayName("Observação")]
    public string? Observacao { get; set; }

    [DisplayName("Situação")]
    public SituacaoOperacaoTerritorial Situacao { get; set; } = SituacaoOperacaoTerritorial.Rascunho;

    [DisplayName("Criada por")]
    public Guid? CriadaPorId { get; set; }

    [DisplayName("Criada por")]
    public string CriadaPor { get; set; } = string.Empty;

    /// <summary>A simulação atual (as anteriores continuam guardadas e imutáveis; DN-03/RT-3).</summary>
    public Guid? SimulacaoAtualId { get; set; }

    [DisplayName("Aplicada em")]
    public DateTime? AplicadaEm { get; set; }
    public Guid? AplicadaPorId { get; set; }
    [DisplayName("Aplicada por")]
    public string? AplicadaPor { get; set; }

    [DisplayName("Cancelada em")]
    public DateTime? CanceladaEm { get; set; }
    public Guid? CanceladaPorId { get; set; }
    [DisplayName("Cancelada por")]
    public string? CanceladaPor { get; set; }
    [DisplayName("Motivo do cancelamento")]
    public string? CanceladaMotivo { get; set; }

    [DisplayName("Desfeita em")]
    public DateTime? DesfeitaEm { get; set; }
    public Guid? DesfeitaPorId { get; set; }
    [DisplayName("Desfeita por")]
    public string? DesfeitaPor { get; set; }
    [DisplayName("Motivo do desfazer")]
    public string? DesfeitaMotivo { get; set; }

    /// <summary>A operação que era a última do mapa antes desta ser aplicada, e o efeito dela (o desfazer devolve os dois).</summary>
    public Guid? OperacaoAnteriorDoMapaId { get; set; }
    public DateOnly? EfeitoAnteriorDoMapa { get; set; }

    // Contagens da aplicação (as da simulação ficam na simulação).
    [DisplayName("Clientes que entraram")]
    public int Entraram { get; set; }
    [DisplayName("Clientes que saíram")]
    public int Sairam { get; set; }
    [DisplayName("Clientes que mudaram")]
    public int Mudaram { get; set; }
    [DisplayName("Origem atualizada")]
    public int OrigemAtualizada { get; set; }

    public List<OperacaoTerritorialMudanca> Mudancas { get; set; } = new();

    public string Numero => Territorios.RegrasOperacaoTerritorial.Numero(Ano, Sequencia);

    public bool Aberta => Situacao is SituacaoOperacaoTerritorial.Rascunho or SituacaoOperacaoTerritorial.Simulada;

    /// <summary>Indicador (DN-04): aplicada e o efeito ainda não chegou.</summary>
    public bool Agendada(DateOnly hoje) => Situacao == SituacaoOperacaoTerritorial.Aplicada && EfeitoEm > hoje;

    /// <summary>Indicador (DN-04): aplicada e o efeito já chegou.</summary>
    public bool EmVigor(DateOnly hoje) => Situacao == SituacaoOperacaoTerritorial.Aplicada && EfeitoEm <= hoje;
}

/// <summary>
/// Uma mudança planejada (T17): o tipo, o alvo, a base que o usuário viu (id e versão, para detectar que ela mudou depois:
/// lacuna L5) e o antes → depois. O conteúdo variável fica em JSON de forma fechada por tipo (<see cref="Territorios.DadosMudancaTerritorial"/>).
/// </summary>
[DisplayName("Mudança da operação territorial")]
public class OperacaoTerritorialMudanca : EntidadeBase, IParteDeAgregado
{
    public const int TamanhoMaximoJson = 20000;

    public Guid OperacaoId { get; set; }
    public Guid MapaId { get; set; }

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("Tipo")]
    public TipoMudancaTerritorial Tipo { get; set; }

    [DisplayName("Território")]
    public Guid? TerritorioId { get; set; }

    [DisplayName("Cliente")]
    public Guid? PessoaId { get; set; }

    /// <summary>Base: a versão da regra (regra), a exceção (encerrar exceção) ou a posição aberta (estrutura) que o usuário viu.</summary>
    public Guid? RegraBaseId { get; set; }
    public Guid? ExcecaoBaseId { get; set; }
    public Guid? PosicaoBaseId { get; set; }

    /// <summary>rowversion da linha-base quando a mudança foi incluída (regra/exceção); nulo quando não há base. A auditoria
    /// registra que mudou, sem o valor (bytes técnicos não dizem nada a quem lê o histórico).</summary>
    [NaoAuditarValor]
    public byte[]? BaseVersao { get; set; }

    /// <summary>Foto em JSON da base quando a mudança foi incluída (nulo = inclusão).</summary>
    [DisplayName("Antes")]
    public string? Antes { get; set; }

    /// <summary>O proposto, em JSON do tipo fechado.</summary>
    [DisplayName("Depois")]
    public string Depois { get; set; } = "{}";

    string IParteDeAgregado.RaizEntidade => nameof(OperacaoTerritorial);
    Guid IParteDeAgregado.RaizId => OperacaoId;
}

/// <summary>
/// Uma simulação (DN-03): evidência imutável do que o sistema mostrou. Nunca atualizada nem apagada; a operação aponta a
/// atual. Guarda a versão do motor e da árvore lidas, a assinatura, quem, quando e as contagens.
/// </summary>
[NaoAuditar]
public class OperacaoTerritorialSimulacao : EntidadeBase
{
    public Guid OperacaoId { get; set; }
    public DateOnly EfeitoEm { get; set; }
    public DateTime SimuladaEm { get; set; }
    public Guid? SimuladaPorId { get; set; }
    public string SimuladaPor { get; set; } = string.Empty;
    public byte[] VersaoMotor { get; set; } = [];
    public byte[] VersaoArvore { get; set; } = [];
    public byte[] Assinatura { get; set; } = [];
    public DateTime AtributosAvaliadosEm { get; set; }

    public int Entram { get; set; }
    public int Saem { get; set; }
    public int Mudam { get; set; }
    public int OrigemAtualizada { get; set; }
    public int EmConflito { get; set; }
    public int Inconsistencias { get; set; }
    public int DaOperacao { get; set; }
    public int Divergencias { get; set; }

    public List<OperacaoTerritorialSimulacaoItem> Itens { get; set; } = new();
}

/// <summary>Um cliente afetado numa simulação: atual → proposto, o efeito, a origem (esta operação ou divergência) e a explicação.</summary>
[NaoAuditar]
public class OperacaoTerritorialSimulacaoItem : EntidadeBase
{
    public const int TamanhoMaximoExplicacao = 8000;

    public Guid SimulacaoId { get; set; }
    public Guid PessoaId { get; set; }
    public ResultadoAtribuicao Resultado { get; set; }
    public EfeitoNoCliente Efeito { get; set; }
    public OrigemEfeitoSimulado OrigemEfeito { get; set; }
    public Guid? TerritorioAtualId { get; set; }
    public Guid? TerritorioPropostoId { get; set; }
    public string Explicacao { get; set; } = "{}";
}

/// <summary>O que a aplicação fez com um cliente, com a explicação congelada. Gravado uma vez; o banco nega UPDATE e DELETE.</summary>
[NaoAuditar]
public class OperacaoTerritorialItem : EntidadeBase
{
    public Guid OperacaoId { get; set; }
    public Guid PessoaId { get; set; }
    public ResultadoAtribuicao Resultado { get; set; }
    public EfeitoNoCliente Efeito { get; set; }
    public Guid? TerritorioAnteriorId { get; set; }
    public Guid? TerritorioNovoId { get; set; }
    public Guid? AtribuicaoEncerradaId { get; set; }
    public Guid? AtribuicaoNovaId { get; set; }
    public string Explicacao { get; set; } = "{}";
}

/// <summary>Uma linha de fato que a operação fechou, com o fim de antes: o desfazer (T15) a reabre exatamente como era.</summary>
[NaoAuditar]
public class OperacaoTerritorialFechamento
{
    public Guid OperacaoId { get; set; }
    public TabelaFechamentoTerritorial Tabela { get; set; }
    public Guid LinhaId { get; set; }
    public DateOnly? FimAnterior { get; set; }
}
