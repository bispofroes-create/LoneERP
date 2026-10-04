using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Profissoes;

namespace Lone.Tests.Cliente;

public class ProfissoesFormularioTests
{
    private static readonly ProfissaoDto Advogado = new() { Id = Guid.NewGuid(), Nome = "Advogado", Ativo = true };
    private static readonly ProfissaoDto Medico = new() { Id = Guid.NewGuid(), Nome = "Médico", Ativo = true };
    private static readonly ProfissaoDto Datilografo = new() { Id = Guid.NewGuid(), Nome = "Datilógrafo", Ativo = false };

    [Fact]
    public void Seletor_sugere_sem_acento_e_escolhe_sozinho_o_nome_exato()
    {
        var seletor = new SeletorDeLista();
        seletor.DefinirItens([new("1", "Médico"), new("2", "Médico veterinário"), new("3", "Advogado")]);

        seletor.Texto = "medic";
        Assert.Equal(new[] { "Médico", "Médico veterinário" }, seletor.Sugestoes.Select(s => s.Texto));
        Assert.True(seletor.Pendente);
        Assert.NotNull(seletor.Validar("Profissão"));

        seletor.Texto = "advogado";
        Assert.Equal("3", seletor.Chave);
        Assert.False(seletor.Pendente);
    }

    [Fact]
    public void Seletor_acha_pela_busca_extra()
    {
        var seletor = new SeletorDeLista();
        seletor.DefinirItens([new("241005", "2410-05 · Advogado", "241005")]);

        seletor.Texto = "2410";

        Assert.Single(seletor.Sugestoes);
    }

    [Fact]
    public void Ficha_mostra_a_profissao_gravada_mesmo_desativada_mas_so_oferece_as_ativas()
    {
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Ana", ProfissaoId = Datilografo.Id },
            profissoes: [Advogado, Medico, Datilografo]);

        Assert.Equal("Datilógrafo (desativada)", f.Profissao.Texto);
        Assert.Equal(new[] { "Advogado", "Médico" }, f.Profissao.Itens.Select(i => i.Texto));
        Assert.Equal(Datilografo.Id, f.ParaDto().ProfissaoId);

        f.Profissao.Texto = "medico";
        Assert.Equal(Medico.Id, f.ParaDto().ProfissaoId);

        f.Profissao.Texto = string.Empty;
        Assert.Null(f.ParaDto().ProfissaoId);
    }

    [Fact]
    public void Ficha_oferece_a_CBO_depois_das_cadastradas_sem_repetir_codigo_nem_nome()
    {
        var engenheiro = new ProfissaoDto { Id = Guid.NewGuid(), Nome = "Engenheiro de Software", Ativo = true, OcupacaoCboId = 212405 };
        var f = PessoaFormulario.NovaPessoa(profissoes: [engenheiro, Advogado]);
        f.OferecerOcupacoesCbo([
            new OcupacaoCboDto { Codigo = 212405, Titulo = "Analista de desenvolvimento de sistemas" }, // já ligada
            new OcupacaoCboDto { Codigo = 241005, Titulo = "ADVOGADO" },                               // mesmo nome
            new OcupacaoCboDto { Codigo = 214205, Titulo = "Engenheiro civil" }]);

        f.Profissao.Texto = "engenheiro";

        Assert.Equal(new[] { "Engenheiro de Software", "Engenheiro civil" }, f.Profissao.Sugestoes.Select(s => s.Texto));
        Assert.Equal("CBO 2142-05", f.Profissao.Sugestoes[1].Detalhe);
        Assert.Equal(3, f.Profissao.Itens.Count);
    }

    [Fact]
    public void Ocupacao_da_CBO_escolhida_vai_para_a_API_sem_profissao()
    {
        var f = PessoaFormulario.NovaPessoa(profissoes: [Advogado]);
        f.OferecerOcupacoesCbo([new OcupacaoCboDto { Codigo = 214205, Titulo = "Engenheiro civil" }]);

        f.Profissao.Texto = "2142-05";
        f.Profissao.Escolher(f.Profissao.Sugestoes.Single());

        var dto = f.ParaDto();
        Assert.Null(dto.ProfissaoId);
        Assert.Equal(214205, dto.OcupacaoCboEscolhida);
        Assert.Equal("Engenheiro civil", f.Profissao.Texto);

        f.Profissao.Texto = "advogado";
        Assert.Equal(Advogado.Id, f.ParaDto().ProfissaoId);
        Assert.Null(f.ParaDto().OcupacaoCboEscolhida);
    }

    [Fact]
    public void Profissao_gravada_fora_da_lista_lida_volta_intacta()
    {
        var gravada = Guid.NewGuid();
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Ana", ProfissaoId = gravada });

        Assert.Equal(gravada, f.ParaDto().ProfissaoId);
    }

    [Fact]
    public void Profissao_criada_pelo_atalho_fica_escolhida()
    {
        var f = PessoaFormulario.NovaPessoa(profissoes: [Advogado]);
        var nova = new ProfissaoDto { Id = Guid.NewGuid(), Nome = "Engenheiro", Ativo = true };

        f.IncluirProfissao(nova);

        Assert.Equal(nova.Id, f.ParaDto().ProfissaoId);
        Assert.Equal(2, f.Profissao.Itens.Count);
    }

    [Fact]
    public void Ficha_da_profissao_envia_o_codigo_CBO_escolhido()
    {
        var cbo = new List<ItemSeletor> { ProfissaoEdicao.ItemCbo(new OcupacaoCboDto { Codigo = 241005, Titulo = "Advogado" }) };
        var ficha = ProfissaoEdicao.Criar(cbo);
        ficha.Nome = " Advogado ";

        ficha.Cbo.Texto = "241005";
        Assert.Single(ficha.Cbo.Sugestoes);
        ficha.Cbo.EscolherCommand.Execute(ficha.Cbo.Sugestoes[0]);

        var dto = ficha.ParaDto();
        Assert.Equal("Advogado", dto.Nome);
        Assert.Equal(241005, dto.OcupacaoCboId);
        Assert.Empty(ficha.ValidarLocalmente());
    }
}
