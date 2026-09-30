using System.Globalization;
using System.Text.Json;
using Lone.Application.Consultas;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Territorios;
using Lone.Domain.Enums;

namespace Lone.Application.Territorios;

/// <summary>
/// Os critérios de uma regra de território: JSON gravado (ids do catálogo e dos cadastros), conferência contra a lista
/// fechada da DN-07 e o texto congelado com os nomes do dia (T6/L: etiqueta ou município renomeado depois não muda o
/// passado).
/// </summary>
public static class TextoRegraTerritorio
{
    public const int MaximoGrupos = 20;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string ParaJson(GruposRegraTerritorioDto grupos) => JsonSerializer.Serialize(grupos, Json);

    public static GruposRegraTerritorioDto DeJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<GruposRegraTerritorioDto>(json, Json) ?? new();

    /// <summary>Os campos citados (para ler os atributos que a regra avaliou).</summary>
    public static IEnumerable<string> Campos(GruposRegraTerritorioDto g) =>
        g.Inclusao.Concat(g.Exclusao).SelectMany(x => x.Condicoes).Select(c => c.Campo);

    /// <summary>
    /// Limpa e confere os grupos: pelo menos um grupo de inclusão; nenhum grupo vazio; cada condição válida no catálogo;
    /// só campos da lista fechada (DN-07); campos com permissão só para quem a tem. As condições ficam normalizadas.
    /// </summary>
    public static List<string> Validar(GruposRegraTerritorioDto? grupos, Func<string, bool> possui)
    {
        var erros = new List<string>();
        if (grupos is null || grupos.Inclusao.Count == 0)
        {
            erros.Add("A regra precisa de pelo menos um grupo de inclusão (quem entra).");
            return erros;
        }
        if (grupos.Inclusao.Count + grupos.Exclusao.Count > MaximoGrupos)
            erros.Add($"Use no máximo {MaximoGrupos} grupos por regra.");
        foreach (var grupo in grupos.Inclusao.Concat(grupos.Exclusao))
        {
            grupo.Condicoes ??= [];
            if (grupo.Condicoes.Count == 0)
            {
                erros.Add("Há um grupo sem condição: preencha ou retire o grupo.");
                continue;
            }
            erros.AddRange(CatalogoFiltrosPessoas.Normalizar(grupo.Condicoes));
            foreach (var c in grupo.Condicoes)
            {
                var d = CatalogoFiltrosPessoas.Obter(c.Campo);
                if (d is null) continue; // o Normalizar já apontou
                if (!d.UsavelEmRegraTerritorio)
                    erros.Add($"\"{d.Nome}\" não pode ser usado em regra de território (lista fechada de campos; campos relativos a hoje, " +
                              "da carteira, dados pessoais e campos personalizados ficam fora).");
                else if ((d.Permissao is { } p && !possui(p)) || (d.PermissaoEmRegraTerritorio is { } q && !possui(q)))
                    erros.Add($"Você não tem permissão para usar \"{d.Nome}\" em regra de território.");
            }
        }
        return erros.Distinct().ToList();
    }

    /// <summary>
    /// O texto congelado: "Entra quem atende a: [UF: MG · Etiquetas: VIP] ou [CNAE: começa com 47]. Não entra: [...]".
    /// <paramref name="nomes"/>: campo → valor → nome do dia (opções do catálogo, cadastros e municípios).
    /// </summary>
    public static string Texto(GruposRegraTerritorioDto g, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> nomes)
    {
        string Grupo(GrupoCondicoesTerritorioDto x) => "[" + string.Join(" · ", x.Condicoes.Select(c => Condicao(c, nomes))) + "]";
        var texto = "Entra quem atende a: " + string.Join(" ou ", g.Inclusao.Select(Grupo)) + ".";
        if (g.Exclusao.Count > 0) texto += " Não entra quem atende a: " + string.Join(" ou ", g.Exclusao.Select(Grupo)) + ".";
        return texto;
    }

    public static string Condicao(CondicaoFiltro c, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> nomes)
    {
        var d = CatalogoFiltrosPessoas.Obter(c.Campo);
        var nome = d?.Nome ?? c.Campo;
        var deCampo = nomes.GetValueOrDefault(c.Campo);
        string Valor(string v) => deCampo?.GetValueOrDefault(v) ?? d?.Opcoes?.FirstOrDefault(o => o.Valor == v)?.Texto ?? v;
        var valores = string.Join(", ", c.Valores.Select(Valor));
        return c.Operador switch
        {
            OperadorFiltro.Sim => nome,
            OperadorFiltro.Nao => $"{nome}: não",
            OperadorFiltro.UmDestes => $"{nome}: {valores}",
            OperadorFiltro.TodosDestes => $"{nome}: todos de {valores}",
            OperadorFiltro.NenhumDestes => $"{nome}: nenhum de {valores}",
            OperadorFiltro.Entre when c.Valores.Count == 2 => $"{nome}: {Valor(c.Valores[0])} a {Valor(c.Valores[1])}",
            OperadorFiltro.Vazio => $"{nome}: não preenchido",
            OperadorFiltro.NaoVazio => $"{nome}: preenchido",
            OperadorFiltro.ComecaCom => $"{nome}: começa com {valores}",
            OperadorFiltro.Contem => $"{nome}: contém {valores}",
            OperadorFiltro.Igual => $"{nome}: igual a {valores}",
            OperadorFiltro.APartirDe => $"{nome}: a partir de {valores}",
            OperadorFiltro.Ate => $"{nome}: até {valores}",
            _ => $"{nome}: {valores}"
        };
    }

    /// <summary>Os municípios citados (a tela de filtro busca sob demanda; o texto congelado precisa do nome).</summary>
    public static List<int> Municipios(GruposRegraTerritorioDto g) =>
        [.. g.Inclusao.Concat(g.Exclusao).SelectMany(x => x.Condicoes).Where(c => c.Campo == CamposFiltroPessoas.Municipio)
            .SelectMany(c => c.Valores).Select(v => int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0).Distinct()];

    // ------------------------------------------------------------------ Nomes para as telas

    public static string NomeTipo(TipoMudancaTerritorial t) => t switch
    {
        TipoMudancaTerritorial.NovaVersaoRegra => "Nova versão da regra",
        TipoMudancaTerritorial.EncerrarRegra => "Encerrar regra",
        TipoMudancaTerritorial.Fixar => "Fixar cliente",
        TipoMudancaTerritorial.Retirar => "Retirar cliente",
        TipoMudancaTerritorial.EncerrarExcecao => "Encerrar exceção",
        TipoMudancaTerritorial.MoverTerritorio => "Mover território",
        TipoMudancaTerritorial.EncerrarTerritorio => "Encerrar território",
        TipoMudancaTerritorial.ReativarTerritorio => "Reativar território",
        _ => t.ToString()
    };

    public static string NomeResultado(ResultadoAtribuicao r) => r switch
    {
        ResultadoAtribuicao.SemTerritorio => "Sem território",
        ResultadoAtribuicao.Atribuido => "Atribuído",
        ResultadoAtribuicao.Conflito => "Conflito (sem território)",
        ResultadoAtribuicao.PermaneceEmConflito => "Conflito (mantém o atual)",
        ResultadoAtribuicao.ConflitoDeFixacao => "Conflito de fixação (bloqueia)",
        ResultadoAtribuicao.FixacaoInvalida => "Fixação inválida (bloqueia)",
        ResultadoAtribuicao.ForaDoUniverso => "Fora do universo do mapa",
        _ => r.ToString()
    };

    public static string NomeEfeito(EfeitoNoCliente e) => e switch
    {
        EfeitoNoCliente.Permanece => "Permanece",
        EfeitoNoCliente.Entra => "Entra",
        EfeitoNoCliente.Sai => "Sai",
        EfeitoNoCliente.Muda => "Muda de território",
        EfeitoNoCliente.OrigemAtualizada => "Mesmo território, outra origem",
        EfeitoNoCliente.Bloqueado => "Bloqueado (inconsistência)",
        _ => e.ToString()
    };

    public static string NomePasso(PassoDecisaoTerritorial p) => p switch
    {
        PassoDecisaoTerritorial.Universo => "Universo do mapa",
        PassoDecisaoTerritorial.UnicoCandidato => "Único candidato",
        PassoDecisaoTerritorial.Fixacao => "Fixação (exceção)",
        PassoDecisaoTerritorial.Prioridade => "Prioridade",
        PassoDecisaoTerritorial.Especificidade => "Especificidade (o território mais abaixo na árvore)",
        PassoDecisaoTerritorial.NaoExclusivo => "Mapa não exclusivo (todos os candidatos)",
        PassoDecisaoTerritorial.Conflito => "Conflito: empate que a regra não resolve",
        PassoDecisaoTerritorial.Inconsistencia => "Inconsistência nas exceções",
        _ => "Nenhuma regra atende"
    };
}
