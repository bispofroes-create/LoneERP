using Lone.Domain.Enums;

namespace Lone.Contracts.Pessoas;

/// <summary>Como o valor de uma coluna da lista chega (texto invariável) e como a tela mostra.</summary>
public enum TipoColunaLista
{
    Texto = 0,

    /// <summary>Número inteiro ou decimal com ponto ("1500.5").</summary>
    Numero = 1,

    /// <summary>Valor em reais com ponto ("1500.5"); a tela mostra "R$ 1.500,50".</summary>
    Moeda = 2,

    /// <summary>"aaaa-mm-dd"; a tela mostra "dd/mm/aaaa".</summary>
    Data = 3,

    /// <summary>Só dígitos; a tela aplica a máscara de telefone.</summary>
    Telefone = 4,

    /// <summary>Só dígitos; a tela mostra "00000-000".</summary>
    Cep = 5,

    /// <summary>Código de uma opção (enum); a tela mostra o texto da opção (<see cref="ColunaListaDto.Opcoes"/>).</summary>
    Opcao = 6,

    // Colunas que a linha já traz em PessoaResumo (sem valor em Valores):
    Codigo = 20,
    Documento = 21,
    Natureza = 22,
    Papeis = 23,
    Situacao = 24,
    Cidade = 25
}

public enum DirecaoOrdenacao
{
    Crescente = 1,
    Decrescente = 2
}

/// <summary>Coluna clicada no cabeçalho. Sem ordenação = ordem padrão (nome).</summary>
public sealed class OrdenacaoLista
{
    public string Coluna { get; set; } = string.Empty;
    public DirecaoOrdenacao Direcao { get; set; } = DirecaoOrdenacao.Crescente;
}

/// <summary>Uma coluna que o usuário pode mostrar na lista de pessoas.</summary>
public sealed class ColunaListaDto
{
    /// <summary>Identificador estável (o mesmo Id do campo do filtro quando existe). Vai para as visões e preferências.</summary>
    public string Id { get; set; } = string.Empty;
    public string Grupo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public TipoColunaLista Tipo { get; set; }

    /// <summary>Largura inicial na tela (pontos).</summary>
    public double Largura { get; set; } = 160;

    /// <summary>Aparece quando o usuário ainda não escolheu as colunas (ou ao "Restaurar padrão").</summary>
    public bool Padrao { get; set; }

    /// <summary>Campo do catálogo de filtros usado pela linha de filtro desta coluna (nulo = coluna sem filtro rápido).</summary>
    public string? CampoFiltro { get; set; }

    /// <summary>Texto de cada código (colunas do tipo <see cref="TipoColunaLista.Opcao"/>).</summary>
    public List<OpcaoFiltroDto> Opcoes { get; set; } = new();
}

/// <summary>
/// Colunas escolhidas (na ordem da tela), ordenação e linha de filtro das colunas. Guardado por usuário
/// (preferência da tela) e dentro das visões salvas. O nome é fixo e não entra em <see cref="Colunas"/>.
/// </summary>
public sealed class LayoutListaPessoas
{
    public List<string> Colunas { get; set; } = new();
    public OrdenacaoLista? Ordenacao { get; set; }
    public bool FiltroNasColunas { get; set; }

    /// <summary>Linhas compactas (mais linhas na tela).</summary>
    public bool Compacta { get; set; }

    /// <summary>
    /// Abas acima da lista, na ordem (Ids de <see cref="AbasPessoas"/>; "Todos" é fixo e não entra). Nulo = as abas padrão.
    /// Só na preferência do usuário: as visões salvas não mexem nas abas.
    /// </summary>
    public List<string>? Abas { get; set; }

    /// <summary>
    /// Clique na linha abre a ficha direto (em vez de mostrar a prévia ao lado). Escolha de cada usuário; o padrão é a
    /// prévia. Só na preferência do usuário: as visões salvas não mexem nisso.
    /// </summary>
    public bool CliqueAbreFicha { get; set; }
}

/// <summary>
/// Abas da lista de pessoas: por natureza ("natureza:Fisica"), por papel do cadastro de papéis ("papel:{Id}") ou uma
/// visão salva ("visao:{Id}"). Cada usuário escolhe as suas; "Todos" é sempre a primeira.
/// </summary>
public static class AbasPessoas
{
    public const string Todos = "todos";
    public const string PrefixoNatureza = "natureza:";
    public const string PrefixoPapel = "papel:";
    public const string PrefixoVisao = "visao:";

    /// <summary>Máximo de abas escolhidas (além de "Todos").</summary>
    public const int Maximo = 10;

    public static string Natureza(NaturezaPessoa natureza) => PrefixoNatureza + natureza;
    public static string Papel(Guid papelId) => PrefixoPapel + papelId.ToString("D");
    public static string Visao(Guid visaoId) => PrefixoVisao + visaoId.ToString("D");

    public static NaturezaPessoa? NaturezaDe(string? id) =>
        id is not null && id.StartsWith(PrefixoNatureza, StringComparison.Ordinal) &&
        Enum.TryParse<NaturezaPessoa>(id[PrefixoNatureza.Length..], ignoreCase: false, out var n) && Enum.IsDefined(n) &&
        !int.TryParse(id[PrefixoNatureza.Length..], out _)
            ? n : null;

    public static Guid? PapelDe(string? id) => GuidDe(id, PrefixoPapel);
    public static Guid? VisaoDe(string? id) => GuidDe(id, PrefixoVisao);

    /// <summary>Id no formato de uma aba conhecida (natureza, papel ou visão).</summary>
    public static bool Valido(string? id) => NaturezaDe(id) is not null || PapelDe(id) is not null || VisaoDe(id) is not null;

    private static Guid? GuidDe(string? id, string prefixo) =>
        id is not null && id.StartsWith(prefixo, StringComparison.Ordinal) && Guid.TryParse(id[prefixo.Length..], out var g) ? g : null;
}

/// <summary>Colunas da lista de pessoas: Ids e limites (iguais na API e no aplicativo).</summary>
public static class ColunasPessoas
{
    /// <summary>Nome da preferência da tela (uma por usuário).</summary>
    public const string TelaLista = "pessoas-lista";

    /// <summary>Coluna fixa à esquerda (não sai da lista, mas ordena e filtra).</summary>
    public const string Nome = CamposFiltroPessoas.Nome;

    public const int MaximoColunas = 40;
}
