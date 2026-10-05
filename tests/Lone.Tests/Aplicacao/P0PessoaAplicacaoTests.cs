using Lone.Application.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Aplicacao;

/// <summary>P0 no serviço: o conteúdo novo dos DTOs (Ativo do contato e do sócio, data de saída) e o evento de conferência (D7).</summary>
public class P0PessoaAplicacaoTests
{
    [Fact]
    public void Mapeamento_leva_Ativo_e_a_data_de_saida_so_enquanto_inativo()
    {
        var dto = new PessoaDto
        {
            Id = Guid.NewGuid(), Natureza = NaturezaPessoa.Juridica, Nome = "Empresa",
            Contatos = [new ContatoDto { Id = Guid.NewGuid(), Nome = "Maria", Principal = true, Ativo = false }],
            Socios =
            [
                new SocioDto { Id = Guid.NewGuid(), Nome = "Voltou", Ativo = true, SaiuEm = new DateOnly(2026, 1, 10) },
                new SocioDto { Id = Guid.NewGuid(), Nome = "Saiu", Ativo = false, SaiuEm = new DateOnly(2026, 2, 1) }
            ]
        };

        var p = PessoaMapeamento.ParaEntidade(dto);

        var contato = Assert.Single(p.Contatos);
        Assert.Equal((false, false), (contato.Ativo, contato.Principal)); // inativo nunca é principal
        Assert.Null(p.Socios.Single(s => s.Nome == "Voltou").SaiuEm);
        Assert.Equal(new DateOnly(2026, 2, 1), p.Socios.Single(s => s.Nome == "Saiu").SaiuEm);

        var volta = PessoaMapeamento.ParaDto(p);
        Assert.False(volta.Contatos.Single().Ativo);
        Assert.False(volta.Socios.Single(s => s.Nome == "Saiu").Ativo);
    }

    [Fact]
    public void Dto_antigo_sem_Ativo_grava_como_ativo()
    {
        Assert.True(new ContatoDto().Ativo);
        Assert.True(new SocioDto().Ativo);
    }

    private static PessoaSocio S(string nome, string? documento = null) =>
        new() { Id = Guid.NewGuid(), Nome = nome, Qualificacao = "Sócio", Documento = documento };

    [Fact]
    public void Socio_novo_compativel_com_um_gravado_conta_como_nao_identificado()
    {
        var gravado = S("Maria Souza");
        var anterior = new Pessoa { Id = Guid.NewGuid(), Socios = [gravado, S("MARIA SOUZA")] };
        var dados = new Pessoa
        {
            Id = anterior.Id,
            Socios = [gravado, anterior.Socios[1], S("Maria Souza"), S("Ana Lima")] // o 3º é ambíguo; a 4ª é nova de verdade
        };

        Assert.Equal(1, PessoaAppService.SociosNaoIdentificados(dados, anterior));
        Assert.Equal(0, PessoaAppService.SociosNaoIdentificados(dados, anterior: null)); // cadastro novo: nada a conferir
    }
}
