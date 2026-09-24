using System.Text.Json;
using Lone.Infrastructure.Integracoes.Ibge;

namespace Lone.Tests.Integracoes;

public class IbgeMunicipiosOficiaisTests
{
    [Fact]
    public void Le_so_codigo_e_nome_mesmo_com_regioes_nulas()
    {
        using var json = JsonDocument.Parse("""
            [
              { "id": 3120904, "nome": "Curvelo", "microrregiao": { "id": 31019, "mesorregiao": { "UF": { "sigla": "MG" } } } },
              { "id": 5101837, "nome": "Boa Esperança do Norte", "microrregiao": null, "regiao-imediata": null },
              { "id": null, "nome": "Sem código" },
              "lixo"
            ]
            """);

        var lista = IbgeMunicipiosOficiais.Ler(json.RootElement);

        Assert.Equal(new[] { (3120904, "Curvelo"), (5101837, "Boa Esperança do Norte") }, lista.Select(m => (m.Codigo, m.Nome)));
    }
}
