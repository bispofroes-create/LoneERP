using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Municipios;

namespace Lone.Tests.Cliente;

public class SeletorMunicipioTests
{
    private static readonly List<MunicipioDto> MinasGerais =
    [
        new() { Id = 3136504, Nome = "Juramento", Uf = "MG" },
        new() { Id = 3120904, Nome = "Curvelo", Uf = "MG" },
        new() { Id = 3136702, Nome = "Juiz de Fora", Uf = "MG" },
        new() { Id = 3162500, Nome = "São João del-Rei", Uf = "MG" },
        new() { Id = 3127107, Nome = "Frutal", Uf = "MG" }
    ];

    private static (SeletorMunicipio Seletor, List<string> Leituras) Novo()
    {
        var leituras = new List<string>();
        var seletor = new SeletorMunicipio
        {
            Fonte = uf =>
            {
                leituras.Add(uf);
                return Task.FromResult<IReadOnlyList<MunicipioDto>>(uf == "MG" ? MinasGerais : []);
            }
        };
        return (seletor, leituras);
    }

    [Fact]
    public async Task Sem_UF_nao_busca_e_com_UF_sugere_pelo_comeco_do_nome()
    {
        var (seletor, leituras) = Novo();
        Assert.False(seletor.PodeDigitar);

        seletor.Uf = "MG";
        seletor.Texto = "ju";
        await seletor.FiltrarAsync();

        Assert.Equal(new[] { "Juiz de Fora", "Juramento" }, seletor.Sugestoes.Select(m => m.Nome));
        Assert.True(seletor.Pendente); // digitou, mas ainda não escolheu
        Assert.Null(seletor.MunicipioId);
        Assert.Single(leituras, "MG");
    }

    [Fact]
    public async Task Busca_ignora_acento_e_maiusculas_e_a_lista_da_UF_e_lida_uma_vez()
    {
        var (seletor, leituras) = Novo();
        seletor.Uf = "MG";

        seletor.Texto = "SAO JOAO";
        await seletor.FiltrarAsync();
        seletor.Texto = "del rei";
        await seletor.FiltrarAsync();

        Assert.Equal("São João del-Rei", Assert.Single(seletor.Sugestoes).Nome);
        Assert.Single(leituras);
    }

    [Fact]
    public async Task Escolher_da_lista_grava_o_codigo_e_nome_exato_unico_e_escolhido_sozinho()
    {
        var (seletor, _) = Novo();
        seletor.Uf = "MG";
        seletor.Texto = "jura";
        await seletor.FiltrarAsync();

        seletor.EscolherCommand.Execute(seletor.Sugestoes[0]);
        Assert.Equal(3136504, seletor.MunicipioId);
        Assert.Equal("Juramento", seletor.Texto);
        Assert.False(seletor.Pendente);
        Assert.Empty(seletor.Sugestoes);

        seletor.Texto = "curvelo";
        await seletor.FiltrarAsync();
        Assert.Equal(3120904, seletor.MunicipioId);
    }

    [Fact]
    public async Task Texto_que_nao_existe_avisa_e_nao_passa_na_validacao()
    {
        var (seletor, _) = Novo();
        seletor.Uf = "MG";
        seletor.Texto = "Cidade Inventada";
        await seletor.FiltrarAsync();

        Assert.Contains("Nenhum município", seletor.Aviso);
        Assert.NotNull(seletor.Validar("Naturalidade"));
    }

    [Fact]
    public void Trocar_a_UF_desfaz_a_escolha_de_outra_UF_e_definir_nao_consulta_nada()
    {
        var (seletor, leituras) = Novo();
        seletor.Definir(3120904, "Curvelo", "MG");
        Assert.Equal(3120904, seletor.MunicipioId);
        Assert.Empty(leituras);

        seletor.Uf = "SP";

        Assert.Null(seletor.MunicipioId);
        Assert.Equal(string.Empty, seletor.Texto);
        Assert.Null(seletor.Validar("Naturalidade")); // vazio é aceito; quem exige é quem usa
    }

    [Fact]
    public async Task Tabela_ainda_vazia_no_servidor_explica_em_vez_de_dizer_que_nao_existe()
    {
        var (seletor, _) = Novo();
        seletor.Uf = "SP";
        seletor.Texto = "Campinas";
        await seletor.FiltrarAsync();

        Assert.Contains("ainda não foi carregada", seletor.Aviso);
    }
}
