using System.ComponentModel;
using Lone.Domain.Auditoria;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Equipe (ex.: "Televendas Sul"): líder, departamento e membros com vigência. É um nível da hierarquia das metas
/// (Empresa → Filial → Departamento → Equipe → Colaborador). Nunca é excluída: desativada.
/// </summary>
[DisplayName("Equipe")]
public class Equipe : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 80;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Departamento")]
    public Guid? DepartamentoId { get; set; }

    [DisplayName("Líder")]
    public Guid? LiderId { get; set; }

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;

    /// <summary>Membros com entrada e saída (nunca apagados: a saída encerra).</summary>
    public List<MembroEquipe> Membros { get; set; } = new();

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Equipe '{Nome}' desativada.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Equipe '{Nome}' reativada.");
    }
}

[DisplayName("Membro da equipe")]
public class MembroEquipe : EntidadeBase, IParteDeAgregado
{
    public Guid EquipeId { get; set; }

    [DisplayName("Pessoa")]
    public Guid PessoaId { get; set; }

    [DisplayName("Entrada")]
    public DateOnly InicioEm { get; set; }

    [DisplayName("Saída")]
    public DateOnly? FimEm { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Equipe);
    Guid IParteDeAgregado.RaizId => EquipeId;

    public bool Vigente(DateOnly inicio, DateOnly fim) => InicioEm <= fim && (FimEm is null || FimEm >= inicio);
}

/// <summary>
/// Indicador medido pelas metas (ex.: "Novos clientes", "Faturamento"). A fonte diz de onde vem o realizado; fontes de
/// vendas entram quando o módulo existir (D6). Código único e imutável. Nunca é excluído: desativado.
/// </summary>
[DisplayName("Indicador")]
public class Indicador : AgregadoRaiz
{
    public const int TamanhoMaximoCodigo = 30;
    public const int TamanhoMaximoNome = 80;

    [DisplayName("Código")]
    public string Codigo { get; set; } = string.Empty;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Fonte")]
    public FonteIndicador Fonte { get; set; }

    [DisplayName("Unidade")]
    public UnidadeIndicador Unidade { get; set; }

    [DisplayName("Sentido")]
    public SentidoIndicador Sentido { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Indicador '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Indicador '{Nome}' reativado.");
    }
}

/// <summary>
/// Meta de um período: itens (indicadores com peso), faixas de desempenho, participantes com os alvos de cada item,
/// realizado e o resultado congelado no fechamento. Nunca é excluída: rascunho pode ser cancelado (desativado).
/// </summary>
[DisplayName("Meta")]
public class Meta : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 100;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    [DisplayName("Início do período")]
    public DateOnly InicioEm { get; set; }

    [DisplayName("Fim do período")]
    public DateOnly FimEm { get; set; }

    [DisplayName("Situação")]
    public SituacaoMeta Situacao { get; set; } = SituacaoMeta.Rascunho;

    /// <summary>Teto do atingimento de cada item na nota ponderada (ex.: 150% = superar muito um item não compensa todo o resto).</summary>
    [DisplayName("Limite de atingimento por item (%)")]
    public decimal LimiteAtingimento { get; set; } = 150;

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;

    [DisplayName("Fechada em")]
    public DateTime? FechadaEm { get; set; }

    [DisplayName("Fechada por")]
    public string? FechadaPor { get; set; }

    public List<MetaItem> Itens { get; set; } = new();
    public List<MetaFaixa> Faixas { get; set; } = new();
    public List<MetaParticipante> Participantes { get; set; } = new();
    public List<MetaAlvo> Alvos { get; set; } = new();
}

/// <summary>Indicador que compõe a meta, com o peso na nota (a soma dos pesos é 100).</summary>
[DisplayName("Item da meta")]
public class MetaItem : EntidadeBase, IParteDeAgregado
{
    public Guid MetaId { get; set; }

    [DisplayName("Indicador")]
    public Guid IndicadorId { get; set; }

    [DisplayName("Peso (%)")]
    public decimal Peso { get; set; }

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Meta);
    Guid IParteDeAgregado.RaizId => MetaId;
}

/// <summary>Faixa de desempenho: a partir de quanto % (nota ponderada) vale, o nome e o % de prêmio.</summary>
[DisplayName("Faixa da meta")]
public class MetaFaixa : EntidadeBase, IParteDeAgregado
{
    public Guid MetaId { get; set; }

    [DisplayName("A partir de (%)")]
    public decimal InicioPercentual { get; set; }

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Prêmio (%)")]
    public decimal PercentualPremio { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Meta);
    Guid IParteDeAgregado.RaizId => MetaId;
}

/// <summary>Quem persegue a meta: um nível da hierarquia e o cadastro correspondente (empresa, filial, departamento, equipe, pessoa).</summary>
[DisplayName("Participante da meta")]
public class MetaParticipante : EntidadeBase, IParteDeAgregado
{
    public Guid MetaId { get; set; }

    [DisplayName("Nível")]
    public NivelParticipante Nivel { get; set; }

    /// <summary>Id da empresa (pessoa), do estabelecimento, do departamento, da equipe ou do colaborador (pessoa).</summary>
    [DisplayName("Participante")]
    public Guid ReferenciaId { get; set; }

    /// <summary>Congelado no fechamento: nota ponderada (%), faixa e % de prêmio (o que as comissões vão ler).</summary>
    [DisplayName("Nota final (%)")]
    public decimal? NotaFinal { get; set; }

    [DisplayName("Faixa")]
    public string? Faixa { get; set; }

    [DisplayName("Prêmio (%)")]
    public decimal? PercentualPremio { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Meta);
    Guid IParteDeAgregado.RaizId => MetaId;
}

/// <summary>Alvo de um participante num item, e o realizado (calculado, informado ou importado) e o resultado congelado.</summary>
[DisplayName("Alvo da meta")]
public class MetaAlvo : EntidadeBase, IParteDeAgregado
{
    public Guid MetaId { get; set; }
    public Guid ParticipanteId { get; set; }
    public Guid ItemId { get; set; }

    [DisplayName("Alvo")]
    public decimal Alvo { get; set; }

    /// <summary>Realizado informado/importado (itens de fonte "Informado") ou o calculado congelado no fechamento.</summary>
    [DisplayName("Realizado")]
    public decimal? Realizado { get; set; }

    [DisplayName("Origem do realizado")]
    public OrigemRealizado? OrigemRealizado { get; set; }

    [DisplayName("Realizado informado em")]
    public DateTime? RealizadoEm { get; set; }

    [DisplayName("Realizado informado por")]
    public string? RealizadoPor { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Meta);
    Guid IParteDeAgregado.RaizId => MetaId;
}
