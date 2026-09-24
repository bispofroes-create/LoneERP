using System.Text.Json;
using Lone.Infrastructure.Integracoes.Cnpj;

namespace Lone.Tests.Integracoes;

public class CnpjWsInscricaoConsultaTests
{
    [Fact]
    public void Le_as_inscricoes_estaduais_com_UF_e_situacao()
    {
        using var json = JsonDocument.Parse("""
            {
              "razao_social": "EMPRESA TESTE",
              "estabelecimento": {
                "inscricoes_estaduais": [
                  { "inscricao_estadual": "110042490114", "ativo": true,  "estado": { "sigla": "SP" } },
                  { "inscricao_estadual": "86925860",     "ativo": false, "estado": { "sigla": "rj" } },
                  { "inscricao_estadual": null,           "ativo": true,  "estado": { "sigla": "MG" } }
                ]
              }
            }
            """);

        var inscricoes = CnpjWsInscricaoConsulta.Ler(json.RootElement);

        Assert.Equal(2, inscricoes.Count);
        Assert.Equal(("SP", "110042490114", true), (inscricoes[0].Uf, inscricoes[0].Numero, inscricoes[0].Ativa));
        Assert.Equal(("RJ", false), (inscricoes[1].Uf, inscricoes[1].Ativa));
    }

    [Fact]
    public void Resposta_sem_inscricoes_vira_lista_vazia()
    {
        using var json = JsonDocument.Parse("""{ "estabelecimento": { } }""");
        Assert.Empty(CnpjWsInscricaoConsulta.Ler(json.RootElement));
    }
}
