using Lone.Contracts.Pessoas;

namespace Lone.Application.Consultas;

/// <summary>
/// Uma coluna que a lista de pessoas pode mostrar. O Id é o mesmo do campo do filtro quando existe (a linha de filtro da
/// coluna usa esse campo). O valor e a ordenação no banco ficam na Infraestrutura (ColunasPessoasSql), pelo mesmo Id.
/// </summary>
public sealed record DefinicaoColunaLista(
    string Id,
    string Grupo,
    string Nome,
    TipoColunaLista Tipo,
    double Largura,
    bool Padrao = false,
    string? CampoFiltro = null,
    string? Permissao = null);

/// <summary>
/// Colunas da lista de pessoas: só campos com um valor por pessoa (telefone e e-mail = o principal; bairro e CEP = do
/// endereço de referência, o mesmo da cidade). Grupos na ordem da ficha. Coluna nova = uma definição aqui + o valor em
/// ColunasPessoasSql (um teste confere os dois lados).
/// </summary>
public static class ColunasListaPessoas
{
    private const string PermissaoFinanceiro = Lone.Contracts.Seguranca.Permissoes.Pessoas.VisualizarFinanceiro;

    public static IReadOnlyList<DefinicaoColunaLista> Colunas { get; } =
    [
        // ---- Identificação ----
        new(CamposFiltroPessoas.Codigo, "Identificação", "Código", TipoColunaLista.Codigo, 100, CampoFiltro: CamposFiltroPessoas.Codigo),
        new(CamposFiltroPessoas.Documento, "Identificação", "CPF / CNPJ", TipoColunaLista.Documento, 180, true, CamposFiltroPessoas.Documento),
        new(CamposFiltroPessoas.Natureza, "Identificação", "Tipo", TipoColunaLista.Natureza, 80, true, CamposFiltroPessoas.Natureza),
        new(CamposFiltroPessoas.NomeFantasia, "Identificação", "Nome fantasia", TipoColunaLista.Texto, 200, CampoFiltro: CamposFiltroPessoas.NomeFantasia),
        new(CamposFiltroPessoas.Papeis, "Identificação", "Papéis", TipoColunaLista.Papeis, 210, true, CamposFiltroPessoas.Papeis),
        new(CamposFiltroPessoas.DataNascimento, "Identificação", "Data de nascimento", TipoColunaLista.Data, 150, CampoFiltro: CamposFiltroPessoas.DataNascimento),
        new(CamposFiltroPessoas.DataAbertura, "Identificação", "Data de abertura", TipoColunaLista.Data, 140, CampoFiltro: CamposFiltroPessoas.DataAbertura),
        new(CamposFiltroPessoas.Porte, "Identificação", "Porte", TipoColunaLista.Texto, 140, CampoFiltro: CamposFiltroPessoas.Porte),
        new(CamposFiltroPessoas.GrupoEmpresarial, "Identificação", "Grupo empresarial", TipoColunaLista.Texto, 190, CampoFiltro: CamposFiltroPessoas.GrupoEmpresarial),

        // ---- Dados pessoais ----
        new(CamposFiltroPessoas.Sexo, "Dados pessoais", "Sexo", TipoColunaLista.Opcao, 130, CampoFiltro: CamposFiltroPessoas.Sexo),
        new(CamposFiltroPessoas.EstadoCivil, "Dados pessoais", "Estado civil", TipoColunaLista.Opcao, 140, CampoFiltro: CamposFiltroPessoas.EstadoCivil),
        new(CamposFiltroPessoas.Profissao, "Dados pessoais", "Profissão", TipoColunaLista.Texto, 170, CampoFiltro: CamposFiltroPessoas.Profissao),

        // ---- Telefones e e-mails ----
        new(CamposFiltroPessoas.Telefone, "Telefones e e-mails", "Telefone principal", TipoColunaLista.Telefone, 170, CampoFiltro: CamposFiltroPessoas.Telefone),
        new(CamposFiltroPessoas.Email, "Telefones e e-mails", "E-mail principal", TipoColunaLista.Texto, 240, CampoFiltro: CamposFiltroPessoas.Email),

        // ---- Endereços ----
        new(CamposFiltroPessoas.Cidade, "Endereços", "Cidade / UF", TipoColunaLista.Cidade, 200, true, CamposFiltroPessoas.Cidade),
        new(CamposFiltroPessoas.Bairro, "Endereços", "Bairro", TipoColunaLista.Texto, 160, CampoFiltro: CamposFiltroPessoas.Bairro),
        new(CamposFiltroPessoas.Cep, "Endereços", "CEP", TipoColunaLista.Cep, 110, CampoFiltro: CamposFiltroPessoas.Cep),

        // ---- Fiscal ----
        new(CamposFiltroPessoas.Regime, "Fiscal", "Regime tributário", TipoColunaLista.Opcao, 170, CampoFiltro: CamposFiltroPessoas.Regime),
        new(CamposFiltroPessoas.InscricaoEstadual, "Fiscal", "Inscrição estadual", TipoColunaLista.Texto, 160, CampoFiltro: CamposFiltroPessoas.InscricaoEstadual),
        new(CamposFiltroPessoas.SituacaoReceita, "Fiscal", "Situação na Receita", TipoColunaLista.Texto, 160, CampoFiltro: CamposFiltroPessoas.SituacaoReceita),
        new(CamposFiltroPessoas.CnaePrincipal, "Fiscal", "CNAE principal", TipoColunaLista.Texto, 120, CampoFiltro: CamposFiltroPessoas.CnaePrincipal),

        // ---- Comercial – Cliente ----
        new(CamposFiltroPessoas.Vendedor, "Comercial – Cliente", "Vendedor", TipoColunaLista.Texto, 170, CampoFiltro: CamposFiltroPessoas.Vendedor),
        new(CamposFiltroPessoas.LimiteCredito, "Comercial – Cliente", "Limite de crédito", TipoColunaLista.Moeda, 150,
            CampoFiltro: CamposFiltroPessoas.LimiteCredito, Permissao: PermissaoFinanceiro),
        new(CamposFiltroPessoas.PerfilComercial, "Comercial – Cliente", "Perfil comercial", TipoColunaLista.Texto, 170, CampoFiltro: CamposFiltroPessoas.PerfilComercial),

        // ---- Interações ----
        new(CamposFiltroPessoas.Origem, "Interações", "Como conheceu", TipoColunaLista.Texto, 170, CampoFiltro: CamposFiltroPessoas.Origem),

        // ---- Situação ----
        new(CamposFiltroPessoas.Situacao, "Situação", "Situação", TipoColunaLista.Situacao, 120, true, CamposFiltroPessoas.Situacao),

        // ---- Cadastro ----
        new(CamposFiltroPessoas.CadastradoEm, "Cadastro", "Cadastrado em", TipoColunaLista.Data, 140, CampoFiltro: CamposFiltroPessoas.CadastradoEm),
        new(CamposFiltroPessoas.AlteradoEm, "Cadastro", "Alterado em", TipoColunaLista.Data, 140, CampoFiltro: CamposFiltroPessoas.AlteradoEm)
    ];

    private static readonly Dictionary<string, DefinicaoColunaLista> PorId = Colunas.ToDictionary(c => c.Id, StringComparer.Ordinal);

    /// <summary>Colunas que a linha sempre traz (PessoaResumo): não precisam de consulta extra.</summary>
    public static bool VemNaLinha(TipoColunaLista tipo) => tipo >= TipoColunaLista.Codigo;

    public static DefinicaoColunaLista? Obter(string? id) => id is not null && PorId.TryGetValue(id, out var d) ? d : null;

    /// <summary>A coluna do nome (fixa) também ordena; as outras ordenam pelo Id da coluna.</summary>
    public static bool Ordenavel(string id) => id == ColunasPessoas.Nome || PorId.ContainsKey(id);

    /// <summary>
    /// Confere colunas e ordenação pedidas (Ids conhecidos, sem repetir, até o máximo). Devolve os erros e deixa em
    /// <paramref name="colunas"/> só as que precisam de consulta extra (as da linha já vêm sempre).
    /// </summary>
    public static List<string> Normalizar(List<string> colunas, OrdenacaoLista? ordenacao, out List<DefinicaoColunaLista> definicoes)
    {
        var erros = new List<string>();
        definicoes = [];
        if (colunas.Count > ColunasPessoas.MaximoColunas) erros.Add($"Colunas demais: use no máximo {ColunasPessoas.MaximoColunas}.");
        foreach (var id in colunas.Distinct(StringComparer.Ordinal))
        {
            if (Obter(id) is { } d) definicoes.Add(d);
            else erros.Add($"Coluna desconhecida: {id}.");
        }
        if (ordenacao is not null)
        {
            if (!Ordenavel(ordenacao.Coluna ?? string.Empty)) erros.Add("Coluna de ordenação desconhecida.");
            if (!Enum.IsDefined(ordenacao.Direcao)) erros.Add("Direção de ordenação inválida.");
        }
        colunas.Clear();
        colunas.AddRange(definicoes.Where(d => !VemNaLinha(d.Tipo)).Select(d => d.Id));
        return erros;
    }

    /// <summary>
    /// Layout lido de uma preferência ou visão: tira colunas desconhecidas ou repetidas e ordenação inválida (uma coluna
    /// que sumiu do sistema não quebra a tela). Colunas sem permissão ficam: a tela esconde as que o usuário não pode ver.
    /// </summary>
    public static LayoutListaPessoas? Limpar(LayoutListaPessoas? layout)
    {
        if (layout is null) return null;
        layout.Colunas = (layout.Colunas ?? []).Where(id => id is not null && PorId.ContainsKey(id))
            .Distinct(StringComparer.Ordinal).Take(ColunasPessoas.MaximoColunas).ToList();
        if (layout.Ordenacao is { } o && (!Ordenavel(o.Coluna ?? string.Empty) || !Enum.IsDefined(o.Direcao)))
            layout.Ordenacao = null;
        // Abas: só Ids no formato conhecido, sem repetir (papel ou visão que deixou de existir a tela ignora). O limite é o
        // dobro do máximo: além das abas visíveis (o cliente já corta em Maximo), vêm as guardadas de papéis desativados,
        // que voltam se o papel for reativado; cortar em Maximo as perderia.
        if (layout.Abas is not null)
            layout.Abas = layout.Abas.Where(AbasPessoas.Valido).Distinct(StringComparer.Ordinal).Take(AbasPessoas.Maximo * 2).ToList();
        return layout;
    }
}
