using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lone.Api.Seguranca;

namespace Lone.Tests.Arquitetura;

/// <summary>
/// Testes de arquitetura do escopo de acesso (Fase 2a-2): nenhuma consulta de Pessoas pode esquecer o escopo. Leem o
/// código-fonte (ao lado dos testes, na mesma solução), porque o que se quer garantir é a forma do código:
/// <list type="bullet">
/// <item>na Infraestrutura, toda leitura direta de <c>db.Pessoas</c> tem, na mesma linha ou nas duas de cima, o comentário
/// "Sem escopo:" com o porquê (as outras começam por <c>EscopoPessoasSql</c>);</item>
/// <item>as rotas de Pessoas passam pelo filtro de escopo, e o id da pessoa só aparece com os nomes que o filtro confere.</item>
/// </list>
/// </summary>
public partial class EscopoArquiteturaTests
{
    private static string Raiz([CallerFilePath] string arquivo = "")
    {
        var pasta = new DirectoryInfo(Path.GetDirectoryName(arquivo)!);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Lone.slnx")))
            pasta = pasta.Parent;
        return pasta?.FullName ?? throw new InvalidOperationException("Não achei a pasta da solução (Lone.slnx).");
    }

    [GeneratedRegex(@"\b[Dd]b\.Pessoas\b|Set<Pessoa>\(\)")]
    private static partial Regex LeituraDePessoas();

    /// <summary>Onde <c>db.Pessoas</c> é a própria definição do escopo ou da tabela.</summary>
    private static readonly string[] Definicoes = ["EscopoPessoasSql.cs", "LoneDbContext.cs"];

    [Fact]
    public void Toda_leitura_direta_de_pessoas_na_infraestrutura_diz_por_que_fica_sem_escopo()
    {
        var pasta = Path.Combine(Raiz(), "src", "Lone.Infrastructure");
        var semMotivo = new List<string>();
        foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.cs", SearchOption.AllDirectories))
        {
            var relativo = Path.GetRelativePath(pasta, arquivo);
            if (relativo.Contains("Migracoes") || relativo.StartsWith("obj") || relativo.StartsWith("bin") ||
                Definicoes.Contains(Path.GetFileName(arquivo)))
                continue;
            var linhas = File.ReadAllLines(arquivo);
            for (var i = 0; i < linhas.Length; i++)
            {
                var linha = linhas[i].Trim();
                if (linha.StartsWith("//") || !LeituraDePessoas().IsMatch(linha)) continue;
                if (!linhas.Skip(Math.Max(0, i - 2)).Take(i - Math.Max(0, i - 2) + 1).Any(l => l.Contains("Sem escopo")))
                    semMotivo.Add($"{relativo}:{i + 1}: {linha}");
            }
        }
        Assert.True(semMotivo.Count == 0,
            "Consulta de Pessoas sem o escopo de acesso. Use EscopoPessoasSql.Pessoas(db, escopo) ou, se for de propósito, " +
            "comente \"// Sem escopo: <motivo>\" logo acima:\n" + string.Join("\n", semMotivo));
    }

    [Fact]
    public void Rotas_de_pessoas_e_de_anexos_passam_pelo_filtro_de_escopo()
    {
        var codigo = File.ReadAllText(Path.Combine(Raiz(), "src", "Lone.Api", "Endpoints", "PessoasEndpoints.cs"));
        Assert.Matches(@"MapGroup\(Rotas\.Pessoas\.Grupo\)[^;]*\.AddEndpointFilter<FiltroEscopoPessoa>\(\)", codigo);
        Assert.Matches(@"MapGroup\(Rotas\.Anexos\.Grupo\)[^;]*\.AddEndpointFilter<FiltroEscopoAnexo>\(\)", codigo);
    }

    /// <summary>Parâmetros de rota do grupo de Pessoas que NÃO são o id de uma pessoa (o filtro não os confere).</summary>
    private static readonly string[] OutrosIds = ["documentoId", "consentimentoId", "bloqueioId", "relacionamentoId", "filtroId"];

    [Fact]
    public void O_id_da_pessoa_nas_rotas_so_usa_os_nomes_que_o_filtro_confere()
    {
        var codigo = File.ReadAllText(Path.Combine(Raiz(), "src", "Lone.Api", "Endpoints", "PessoasEndpoints.cs"));
        var conhecidos = FiltroEscopoPessoa.ParametrosDaPessoa.Concat(OutrosIds).ToHashSet();
        var nomes = Regex.Matches(codigo, @"\{(\w+)(:\w+)?\}").Select(m => m.Groups[1].Value).Distinct().ToList();
        var novos = nomes.Where(n => !conhecidos.Contains(n)).ToList();
        Assert.True(novos.Count == 0,
            "Parâmetro de rota novo em PessoasEndpoints: se for o id de uma pessoa, use {id} ou {pessoaId} (o filtro de escopo " +
            "confere); se não for, inclua em OutrosIds. Novos: " + string.Join(", ", novos));
    }
}
