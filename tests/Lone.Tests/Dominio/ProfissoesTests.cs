using System.Text;
using Lone.Domain.Entidades;
using Lone.Domain.Profissoes;

namespace Lone.Tests.Dominio;

public class ProfissoesTests
{
    [Fact]
    public void Nome_vindo_da_CBO_cabe_no_cadastro()
    {
        Assert.Equal("Engenheiro civil", RegrasProfissao.NomeDaOcupacao("  Engenheiro   civil "));

        var longo = RegrasProfissao.NomeDaOcupacao(new string('a', 120));
        Assert.Equal(Profissao.TamanhoMaximoNome, longo.Length);
        Assert.EndsWith("…", longo);
    }

    [Fact]
    public void Ocupacao_escolhida_na_pessoa_precisa_existir_e_estar_vigente()
    {
        Assert.NotNull(RegrasProfissao.ValidarOcupacaoEscolhida(214205, null));
        Assert.NotNull(RegrasProfissao.ValidarOcupacaoEscolhida(214205, new OcupacaoCbo { Id = 214205, Titulo = "x", Ativo = false }));
        Assert.Null(RegrasProfissao.ValidarOcupacaoEscolhida(214205, new OcupacaoCbo { Id = 214205, Titulo = "x", Ativo = true }));
    }

    [Fact]
    public void Nome_e_descricao_sao_limpos()
    {
        var profissao = new Profissao { Nome = "  Advogado   tributarista ", Descricao = " " };

        RegrasProfissao.Normalizar(profissao);

        Assert.Equal("Advogado tributarista", profissao.Nome);
        Assert.Null(profissao.Descricao);
    }

    [Fact]
    public void Ocupacao_CBO_precisa_existir_na_tabela()
    {
        var profissao = new Profissao { Nome = "Advogado", OcupacaoCboId = 241005 };

        Assert.Contains(RegrasProfissao.Validar(profissao, null), e => e.Contains("2410-05 não existe"));
        Assert.Empty(RegrasProfissao.Validar(profissao, new OcupacaoCbo { Id = 241005, Titulo = "Advogado" }));
        Assert.Contains("Informe o nome da profissão.", RegrasProfissao.Validar(new Profissao(), null));
    }

    [Fact]
    public void Profissao_desativada_so_continua_em_quem_ja_tinha()
    {
        var desativada = new Profissao { Id = Guid.NewGuid(), Nome = "Datilógrafo", Ativo = false };

        Assert.NotNull(RegrasProfissao.ValidarEscolhida(desativada.Id, null, desativada));
        Assert.Null(RegrasProfissao.ValidarEscolhida(desativada.Id, desativada.Id, desativada));
        Assert.NotNull(RegrasProfissao.ValidarEscolhida(Guid.NewGuid(), null, null)); // não existe mais
        Assert.Null(RegrasProfissao.ValidarEscolhida(null, desativada.Id, null));      // tirar a profissão sempre pode
    }

    [Fact]
    public void Desativar_e_reativar_registram_o_evento()
    {
        var profissao = new Profissao { Nome = "Advogado" };

        profissao.Desativar();
        profissao.Reativar();

        Assert.Equal(new[] { "Profissão 'Advogado' desativada.", "Profissão 'Advogado' reativada." }, profissao.RetirarEventos());
    }

    [Fact]
    public void Arquivo_oficial_da_CBO_em_latin1_com_cabecalho()
    {
        var arquivo = Encoding.Latin1.GetBytes("CODIGO;TITULO\r\n241005;Advogado\r\n010105;Oficial general da aeronáutica\r\n\r\n");

        var lido = LeitorCbo.Ler(arquivo);

        Assert.Equal(new[] { 10105, 241005 }, lido.Ocupacoes.Select(o => o.Id));
        Assert.Equal("Oficial general da aeronáutica", lido.Ocupacoes[0].Titulo);
        Assert.Equal("0101-05", lido.Ocupacoes[0].CodigoFormatado);
        Assert.Equal(0, lido.LinhasIgnoradas);
    }

    [Fact]
    public void Arquivo_em_UTF8_com_aspas_hifen_repetido_e_linha_ruim()
    {
        var arquivo = Encoding.UTF8.GetBytes("﻿\"2410-05\";\"Advogado\"\n241005;Repetido\nlixo\n12;Curto\n");

        var lido = LeitorCbo.Ler(arquivo);

        var ocupacao = Assert.Single(lido.Ocupacoes);
        Assert.Equal("Advogado", ocupacao.Titulo); // repetido: vale o primeiro
        Assert.Equal(2, lido.LinhasIgnoradas);     // "lixo" e o código curto
    }
}
