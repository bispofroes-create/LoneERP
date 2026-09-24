using System.Text.Json;
using Lone.Contracts.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Api;

/// <summary>O JSON que a API envia é o mesmo que o aplicativo lê (as duas pontas usam OpcoesJson).</summary>
public class ContratoJsonTests
{
    [Fact]
    public void Enums_viajam_pelo_nome_inclusive_combinacoes()
    {
        var endereco = new EnderecoDto { Finalidades = FinalidadeEndereco.Principal | FinalidadeEndereco.Cobranca };

        var json = JsonSerializer.Serialize(endereco, OpcoesJson.Padrao);

        Assert.Contains("\"finalidades\":\"Principal, Cobranca\"", json);
        Assert.Equal(endereco.Finalidades, JsonSerializer.Deserialize<EnderecoDto>(json, OpcoesJson.Padrao)!.Finalidades);
    }

    [Fact]
    public void Pessoa_vai_e_volta_sem_perder_versao_datas_e_listas()
    {
        var pessoa = new PessoaDto
        {
            Id = Guid.NewGuid(),
            Versao = [0, 0, 0, 0, 0, 0, 7, 209],
            Natureza = NaturezaPessoa.Fisica,
            Nome = "Maria",
            DataNascimento = new DateOnly(1990, 5, 17),
            Papeis = [new PapelDto { Papel = TipoPapel.Cliente, InicioEm = new DateOnly(2026, 1, 2) }]
        };

        var volta = JsonSerializer.Deserialize<PessoaDto>(JsonSerializer.Serialize(pessoa, OpcoesJson.Padrao), OpcoesJson.Padrao)!;

        Assert.Equal(pessoa.Id, volta.Id);
        Assert.Equal(pessoa.Versao, volta.Versao);
        Assert.Equal(pessoa.DataNascimento, volta.DataNascimento);
        Assert.Equal(TipoPapel.Cliente, volta.Papeis[0].Papel);
    }

    [Fact]
    public void Rotas_com_id_seguem_o_padrao_da_api()
    {
        var id = Guid.Parse("0f7a4c1e-0000-0000-0000-0197a1b2c3d4");

        Assert.Equal("api/v1/pessoas/0f7a4c1e-0000-0000-0000-0197a1b2c3d4", Rotas.Pessoas.PorId(id));
        Assert.Equal("api/v1/pessoas/0f7a4c1e-0000-0000-0000-0197a1b2c3d4/historico", Rotas.Pessoas.Historico(id));
        Assert.Equal("api/v1/usuarios/0f7a4c1e-0000-0000-0000-0197a1b2c3d4/desbloquear", Rotas.Usuarios.Desbloquear(id));
    }
}
