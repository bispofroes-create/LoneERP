using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;

namespace Lone.Tests.Dominio;

/// <summary>
/// V2-1: o catálogo oficial de países da NF-e (cPais), gerado da NT 2018.003 v1.01 (aba cPais_2) por
/// Ferramentas/gerar-paises-nfe.py. Confere contagens, códigos críticos, zeros à esquerda, histórico, sucessões e
/// renomeações. Tudo em memória: nada de banco nem internet.
/// </summary>
public class CatalogoPaisesNFeTests
{
    private static readonly DateOnly Referencia = new(2026, 10, 7);
    private static readonly DateOnly FimDasTrocas = new(2018, 5, 31);
    private static readonly IReadOnlyList<PaisNFeOficial> Todos = PaisesNFeOficiais.Todos;

    private static PaisNFeOficial Codigo(string codigo) => Todos.Single(p => p.CodigoPaisNFe == codigo);

    private static bool Vigente(PaisNFeOficial p, DateOnly data) =>
        p.VigenciaInicio <= data && (p.VigenciaFim is null || data <= p.VigenciaFim);

    [Fact]
    public void Fonte_versao_e_hash_sao_os_da_planilha_conferida()
    {
        Assert.Equal("Tabela de Países da NF-e - NT 2018.003", PaisesNFeOficiais.Fonte);
        Assert.Equal("1.01", PaisesNFeOficiais.VersaoFonte);
        Assert.Equal("76386b536812e13baf5f09f49c057e78650be387a96c576377c3998f39358a9f", PaisesNFeOficiais.HashFonte);
    }

    [Fact]
    public void Tem_261_codigos_distintos_e_249_vigentes_em_07_10_2026()
    {
        Assert.Equal(261, Todos.Count);
        Assert.Equal(261, Todos.Select(p => p.CodigoPaisNFe).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(249, Todos.Count(p => Vigente(p, Referencia)));
    }

    [Fact]
    public void Todo_codigo_tem_quatro_digitos_e_esta_em_ordem()
    {
        Assert.All(Todos, p => Assert.True(Pais.CodigoValido(p.CodigoPaisNFe), p.CodigoPaisNFe));
        Assert.Equal(Todos.Select(p => p.CodigoPaisNFe).Order(StringComparer.Ordinal), Todos.Select(p => p.CodigoPaisNFe));
        Assert.All(Todos, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.NomeFiscal));
            Assert.True(p.NomeFiscal.Length <= Pais.TamanhoMaximoNome);
            Assert.True(p.SituacaoFonte is null || p.SituacaoFonte.Length <= Pais.TamanhoMaximoSituacao);
            Assert.True(p.VigenciaFim is null || p.VigenciaFim >= p.VigenciaInicio);
        });
    }

    [Theory]
    [InlineData("1058", "BRASIL")]
    [InlineData("2496", "ESTADOS UNIDOS")]
    [InlineData("6289", "REINO UNIDO")]
    [InlineData("0639", "ARGENTINA")]
    [InlineData("6076", "PORTUGAL")]
    [InlineData("3999", "JAPAO")]
    [InlineData("0230", "ALEMANHA")]
    [InlineData("1600", "CHINA, REPUBLICA POPULAR")]
    [InlineData("1490", "CANADA")]
    [InlineData("5780", "PALESTINA")]
    [InlineData("7600", "SUDÃO DO SUL")]
    [InlineData("0990", "BONAIRE")]
    public void Codigos_criticos_existem_vigentes_com_o_nome_oficial(string codigo, string nome)
    {
        var pais = Codigo(codigo);
        Assert.Equal(nome, pais.NomeFiscal);
        Assert.Null(pais.VigenciaFim);
        Assert.True(Vigente(pais, Referencia));
    }

    [Theory]
    [InlineData("0132", "AFEGANISTAO")]
    [InlineData("0200", "CURACAO")]
    [InlineData("0639", "ARGENTINA")]
    [InlineData("0990", "BONAIRE")]
    public void Zero_a_esquerda_preservado(string codigo, string nome)
    {
        Assert.Equal(nome, Codigo(codigo).NomeFiscal);
        var semZero = codigo.TrimStart('0');
        Assert.DoesNotContain(Todos, p => p.CodigoPaisNFe == semZero);
    }

    [Fact]
    public void Trinta_e_dois_codigos_comecam_com_zero()
    {
        Assert.Equal(32, Todos.Count(p => p.CodigoPaisNFe[0] == '0'));
    }

    [Fact]
    public void Codigo_RFB_de_tres_digitos_nao_vira_codigo_NFe()
    {
        // 249 é o código RFB dos Estados Unidos; o cPais é 2496. O catálogo não tem 249 nem 0249.
        Assert.DoesNotContain(Todos, p => p.CodigoPaisNFe is "249" or "0249");
        Assert.Equal("ESTADOS UNIDOS", Codigo("2496").NomeFiscal);
        Assert.False(Pais.CodigoValido("249"));
    }

    [Fact]
    public void Codigos_encerrados_continuam_no_catalogo_com_a_data_de_fim()
    {
        var encerrados = Todos.Where(p => p.VigenciaFim is not null).Select(p => p.CodigoPaisNFe).ToArray();
        Assert.Equal(new[] { "0200", "0477", "1504", "1508", "1511", "3599", "3964", "4235", "4525", "4885", "4901", "8737" }, encerrados);
        Assert.All(Todos.Where(p => p.VigenciaFim is not null), p =>
        {
            Assert.Equal(FimDasTrocas, p.VigenciaFim);
            Assert.False(Vigente(p, Referencia));
            Assert.True(Vigente(p, FimDasTrocas)); // o último dia ainda vale
        });
        Assert.All(new[] { "0477", "1511", "3964", "4235", "4525", "4901", "8737" },
            c => Assert.Equal("EXCLUÍDO", Codigo(c).SituacaoFonte));
    }

    [Fact]
    public void Vigencia_inicial_preservada_inclusive_datas_que_vieram_como_texto()
    {
        Assert.Equal(new DateOnly(2006, 1, 1), Codigo("1511").VigenciaInicio); // na planilha, texto "01/01/2006"
        Assert.Equal(new DateOnly(2015, 12, 7), Codigo("0200").VigenciaInicio);
        Assert.Equal(new DateOnly(2011, 7, 1), Codigo("7595").VigenciaInicio);
        Assert.Equal(new DateOnly(2012, 11, 29), Codigo("5780").VigenciaInicio);
        Assert.False(Vigente(Codigo("5780"), new DateOnly(2012, 11, 28)));
        Assert.Equal(new DateOnly(2017, 1, 1), Codigo("2003").VigenciaInicio);
    }

    [Theory]
    [InlineData("0200", "2003", "CURACAO")]
    [InlineData("3599", "0990", "BONAIRE")]
    [InlineData("1504", "3212", "GUERNSEY, ILHA DO CANAL (INCLUI ALDERNEY E SARK)")]
    [InlineData("1508", "3930", "JERSEY, ILHA DO CANAL")]
    [InlineData("4885", "4898", "MAYOTTE (ILHAS FRANCESAS)")]
    public void Sucessao_de_codigo_registrada_sem_apagar_o_antigo(string antigo, string novo, string nome)
    {
        var a = Codigo(antigo);
        var n = Codigo(novo);
        Assert.Equal(novo, a.CodigoSucessor);
        Assert.Equal("ALTERADO", a.SituacaoFonte);
        Assert.Equal(FimDasTrocas, a.VigenciaFim);
        Assert.Equal(nome, a.NomeFiscal);
        Assert.Equal(nome, n.NomeFiscal);
        Assert.Null(n.VigenciaFim);
        Assert.Null(n.CodigoSucessor);
        Assert.True(Vigente(n, Referencia));
    }

    [Fact]
    public void So_as_cinco_trocas_explicitas_tem_sucessor_e_todo_sucessor_existe()
    {
        var comSucessor = Todos.Where(p => p.CodigoSucessor is not null).ToArray();
        Assert.Equal(5, comSucessor.Length);
        Assert.All(comSucessor, p =>
        {
            Assert.True(Pais.CodigoValido(p.CodigoSucessor));
            Assert.NotEqual(p.CodigoPaisNFe, p.CodigoSucessor);
            Assert.NotNull(p.VigenciaFim);
            Assert.Contains(Todos, s => s.CodigoPaisNFe == p.CodigoSucessor && s.VigenciaFim is null);
        });
        // CORRIGIDO (São Martinho) e EXCLUÍDO não ganham sucessor.
        Assert.Null(Codigo("6980").CodigoSucessor);
        Assert.Null(Codigo("6998").CodigoSucessor);
        Assert.All(Todos.Where(p => p.SituacaoFonte == "EXCLUÍDO"), p => Assert.Null(p.CodigoSucessor));
    }

    [Theory]
    [InlineData("1988", "KUWAIT", "COVEITE")]
    [InlineData("4499", "MACEDONIA DO NORTE", "MACEDONIA, ANT.REP.IUGOSLAVA")]
    [InlineData("7544", "ESSUATINI", "SUAZILANDIA")]
    public void Renomeacao_vira_uma_entrada_com_o_nome_atual(string codigo, string atual, string anterior)
    {
        var pais = Assert.Single(Todos, p => p.CodigoPaisNFe == codigo);
        Assert.Equal(atual, pais.NomeFiscal);
        Assert.Equal(anterior, pais.NomeAnterior);
        Assert.Equal("APENAS ALTERAÇÃO DE NOME", pais.SituacaoFonte);
        Assert.Null(pais.VigenciaFim);
        Assert.Null(pais.CodigoSucessor);
        Assert.DoesNotContain(Todos, p => p.NomeFiscal == anterior);
    }

    [Fact]
    public void So_as_tres_renomeacoes_tem_nome_anterior()
    {
        Assert.Equal(new[] { "1988", "4499", "7544" }, Todos.Where(p => p.NomeAnterior is not null).Select(p => p.CodigoPaisNFe));
    }

    [Fact]
    public void Entre_os_vigentes_nenhum_nome_se_repete()
    {
        var vigentes = Todos.Where(p => Vigente(p, Referencia)).ToArray();
        Assert.Equal(vigentes.Length, vigentes.Select(p => p.NomeFiscal).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Nome_fiscal_fica_como_na_fonte()
    {
        Assert.Equal("ALBANIA, REPUBLICA  DA", Codigo("0175").NomeFiscal); // dois espaços, como na planilha
    }

    [Theory]
    [InlineData("1058", true)]
    [InlineData("0639", true)]
    [InlineData("639", false)]
    [InlineData("249", false)]
    [InlineData("06390", false)]
    [InlineData("ABCD", false)]
    [InlineData(" 639", false)]
    [InlineData("12-4", false)]
    [InlineData("١٠٥٨", false)] // dígitos não ASCII
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Codigo_valido_exige_quatro_digitos_sem_completar(string? codigo, bool valido)
    {
        Assert.Equal(valido, Pais.CodigoValido(codigo));
    }

    [Fact]
    public void Vigente_em_respeita_inicio_e_fim_inclusivos()
    {
        var pais = new Pais { VigenciaInicio = new DateOnly(2006, 1, 1), VigenciaFim = new DateOnly(2018, 5, 31) };
        Assert.False(pais.VigenteEm(new DateOnly(2005, 12, 31)));
        Assert.True(pais.VigenteEm(new DateOnly(2006, 1, 1)));
        Assert.True(pais.VigenteEm(new DateOnly(2018, 5, 31)));
        Assert.False(pais.VigenteEm(new DateOnly(2018, 6, 1)));
        pais.VigenciaFim = null;
        Assert.True(pais.VigenteEm(Referencia));
    }
}
