using System.ComponentModel;
using Lone.Domain.Auditoria;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Tipo de território (Fase 2b): classificação livre para filtrar e agrupar ("Geográfico", "Segmento", "Estratégico").
/// Só classifica: não muda nenhum comportamento do motor (o comportamento é do mapa). Código único e imutável; nunca é
/// excluído: desativado.
/// </summary>
[DisplayName("Tipo de território")]
public class TipoTerritorio : AgregadoRaiz
{
    public const int TamanhoMaximoCodigo = 30;
    public const int TamanhoMaximoNome = 60;
    public const int TamanhoMaximoDescricao = 250;

    [DisplayName("Código")]
    public string Codigo { get; set; } = string.Empty;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Tipo de território '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Tipo de território '{Nome}' reativado.");
    }
}

/// <summary>
/// Mapa territorial (Fase 2b, decisão T1): uma <b>dimensão independente de atribuição</b>. Define quais territórios disputam
/// um cliente: num mapa exclusivo o cliente tem no máximo um território; entre mapas diferentes nunca há disputa. Não é uma
/// pasta: o motor sempre roda um mapa por vez. Empresa, exclusividade, universo e endereço de referência ficam travados
/// depois que o mapa tem uso (regra publicada ou atribuição), porque mudá-los mudaria resultados sem operação.
/// </summary>
[DisplayName("Mapa territorial")]
public class MapaTerritorial : AgregadoRaiz
{
    public const int TamanhoMaximoCodigo = 30;
    public const int TamanhoMaximoNome = 80;
    public const int TamanhoMaximoDescricao = 500;

    /// <summary>Identificador estável (ex.: "GEOGRAFIA"): relatórios e integrações podem usá-lo. Não muda depois de criado.</summary>
    [DisplayName("Código")]
    public string Codigo { get; set; } = string.Empty;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    /// <summary>Empresa do grupo dona do mapa. Nulo = vale para o grupo todo (mesmo padrão da carteira e das coberturas).</summary>
    [DisplayName("Empresa")]
    public Guid? EmpresaId { get; set; }

    /// <summary>Exclusivo: no máximo um território por cliente neste mapa (empate vira conflito). Não exclusivo: soma todos.</summary>
    [DisplayName("Exclusivo")]
    public bool Exclusivo { get; set; } = true;

    /// <summary>
    /// Finalidade do endereço que as condições de endereço das regras deste mapa consideram: só o endereço marcado como
    /// principal dessa finalidade (decisão T16). Parametrizável: o motor nunca assume "Comercial".
    /// </summary>
    [DisplayName("Endereço de referência")]
    public Guid FinalidadeEnderecoReferenciaId { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Os territórios deste mapa são copiados para os documentos comerciais (oportunidade, pedido, venda, comissão, meta...)
    /// no momento do fato (P-T1, DN-15). Nesta fase só prepara o contrato: nenhum documento existe ainda.
    /// </summary>
    [DisplayName("Registrar nos documentos")]
    public bool RegistrarNosDocumentos { get; set; }

    /// <summary>Universo: classificações de pessoa que podem ser atribuídas (decisão T9). Desmarcar desativa; nunca apaga.</summary>
    public List<MapaTerritorialClassificacao> Classificacoes { get; set; } = new();

    /// <summary>As classificações aceitas hoje.</summary>
    public IEnumerable<Guid> ClassificacoesAceitas => Classificacoes.Where(c => c.Ativo).Select(c => c.PapelId);

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Mapa territorial '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Mapa territorial '{Nome}' reativado.");
    }
}

/// <summary>Classificação de pessoa (papel do cadastro, ex.: Cliente) que faz parte do universo do mapa.</summary>
[DisplayName("Universo do mapa")]
public class MapaTerritorialClassificacao : EntidadeBase, IParteDeAgregado
{
    public Guid MapaId { get; set; }

    [DisplayName("Classificação")]
    public Guid PapelId { get; set; }

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;

    string IParteDeAgregado.RaizEntidade => nameof(MapaTerritorial);
    Guid IParteDeAgregado.RaizId => MapaId;
}

/// <summary>
/// Trava da árvore de um mapa (decisão D1 = B, 29/09/2026): uma linha por mapa, só com a versão. Toda mudança de estrutura
/// (criar, mover, mudar o início, encerrar, reativar) exige a versão da árvore que a tela mostrava e a troca na mesma
/// transação: duas mudanças simultâneas não passam as duas (não formam ciclo) e quem decidiu olhando uma árvore velha é
/// avisado. Separada do cadastro do mapa: mexer na árvore não invalida a ficha do mapa aberta, e vice-versa (desativar o
/// mapa também troca a versão da árvore, porque mapa desativado não aceita mudança de estrutura). Controle técnico: fora da
/// auditoria (a mudança em si é auditada no território).
/// </summary>
[NaoAuditar]
public class MapaTerritorialArvore
{
    public Guid MapaId { get; set; }

    /// <summary>rowversion: muda a cada mudança de estrutura do mapa.</summary>
    public byte[]? Versao { get; set; }

    /// <summary>Última mudança de estrutura (UTC).</summary>
    public DateTime AtualizadoEm { get; set; }
}

/// <summary>
/// Território (Fase 2b): nó da árvore de um mapa. A árvore só diz onde o nó está (para agregar e, no motor, para o desempate
/// por especificidade); quem entra no território é só a regra dele (2b-1b). O pai atual é cópia da posição aberta; o
/// histórico da posição está em <see cref="Posicoes"/>. O mapa não muda nunca, e o banco garante que o pai é do mesmo mapa.
/// Sem uso operacional pode ser criado, renomeado, movido, encerrado e reativado livremente (T14); com uso, mudanças de
/// estrutura só por operação territorial (T18).
/// </summary>
[DisplayName("Território")]
public class Territorio : AgregadoRaiz
{
    public const int TamanhoMaximoCodigo = 30;
    public const int TamanhoMaximoNome = 80;
    public const int TamanhoMaximoDescricao = 500;

    [DisplayName("Mapa")]
    public Guid MapaId { get; set; }

    /// <summary>Único no mapa (inclusive entre os encerrados).</summary>
    [DisplayName("Código")]
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Único entre os irmãos ativos (dois "Centro" em cidades diferentes podem existir).</summary>
    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Tipo")]
    public Guid TipoId { get; set; }

    /// <summary>Território acima hoje (cópia da posição aberta). Nulo = raiz do mapa.</summary>
    [DisplayName("Território acima")]
    public Guid? PaiId { get; set; }

    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    [DisplayName("Situação")]
    public SituacaoTerritorio Situacao { get; set; } = SituacaoTerritorio.Ativo;

    /// <summary>Último dia em que o território existiu (só quando encerrado).</summary>
    [DisplayName("Último dia")]
    public DateOnly? FimEm { get; set; }

    /// <summary>Posição na árvore por período (decisão T7). Nunca apagadas.</summary>
    public List<TerritorioPosicao> Posicoes { get; set; } = new();

    /// <summary>Quem responde pelo território, por função e período. Nunca apagados: encerram (ou são anulados antes de começar).</summary>
    public List<TerritorioResponsavel> Responsaveis { get; set; } = new();

    public bool Ativo => Situacao == SituacaoTerritorio.Ativo;
}

/// <summary>
/// Posição do território na árvore num período (decisão T7): responde "onde este território estava em 15/03" sem
/// reconstruir pela auditoria. <see cref="Ativo"/> falso = linha anulada (operação desfeita ou substituída no mesmo dia,
/// 2b-1b); fica no histórico.
/// </summary>
[DisplayName("Posição do território")]
public class TerritorioPosicao : EntidadeBase, IParteDeAgregado
{
    /// <summary>Repetido do território: é o que deixa o banco garantir que território e pai são do mesmo mapa.</summary>
    public Guid MapaId { get; set; }

    public Guid TerritorioId { get; set; }

    [DisplayName("Território acima")]
    public Guid? PaiId { get; set; }

    [DisplayName("Início")]
    public DateOnly InicioEm { get; set; }

    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;

    /// <summary>Operação territorial que abriu esta posição (nulo = gravada pela ficha, sem uso; 2b-1a).</summary>
    public Guid? OperacaoId { get; set; }

    /// <summary>A mudança da operação que a abriu.</summary>
    public Guid? OperacaoMudancaId { get; set; }

    /// <summary>Operação que a encerrou (fim = véspera do efeito).</summary>
    public Guid? OperacaoEncerramentoId { get; set; }

    /// <summary>Operação que a anulou (desfeita).</summary>
    public Guid? OperacaoAnulacaoId { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Territorio);
    Guid IParteDeAgregado.RaizId => TerritorioId;

    public bool VigenteEm(DateOnly data) => Ativo && InicioEm <= data && (FimEm is null || FimEm >= data);
}

/// <summary>
/// Responsável pelo território: uma pessoa <b>ou</b> uma equipe, numa função (o papel comercial: Vendedor, Supervisor...),
/// com período. Não passa por operação: não muda atribuição de cliente (decisão T4 — o território não é a carteira).
/// <see cref="Ativo"/> falso = incluído por engano e anulado antes de começar (nunca apagado).
/// </summary>
[DisplayName("Responsável pelo território")]
public class TerritorioResponsavel : EntidadeBase, IParteDeAgregado
{
    public const int TamanhoMaximoObservacao = 250;

    public Guid TerritorioId { get; set; }

    [DisplayName("Pessoa")]
    public Guid? PessoaId { get; set; }

    [DisplayName("Equipe")]
    public Guid? EquipeId { get; set; }

    /// <summary>A função é o papel comercial (Vendedor, Representante, Supervisor...): não existe outra lista de funções.</summary>
    [DisplayName("Função")]
    public Guid TipoCarteiraId { get; set; }

    [DisplayName("Início")]
    public DateOnly InicioEm { get; set; }

    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Observação")]
    public string? Observacao { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Operação que encerrou este responsável como consequência do encerramento do território (DN-09: responsável nunca é
    /// mudança da operação). Nulo = encerrado pela ficha.
    /// </summary>
    public Guid? OperacaoEncerramentoId { get; set; }

    string IParteDeAgregado.RaizEntidade => nameof(Territorio);
    Guid IParteDeAgregado.RaizId => TerritorioId;

    public bool VigenteEm(DateOnly data) => Ativo && InicioEm <= data && (FimEm is null || FimEm >= data);
}
