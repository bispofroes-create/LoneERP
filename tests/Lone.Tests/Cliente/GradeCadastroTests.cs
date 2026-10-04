using Lone.Cliente.Grade;

namespace Lone.Tests.Cliente;

/// <summary>Lista em colunas das telas de cadastro (padrão de tela de cadastro, 03/10/2026).</summary>
public class GradeCadastroTests
{
    private sealed record Registro(Guid Id, string Nome, string Tipo, int Clientes, bool Ativo);

    private static GradeCadastro<Registro> Grade() => new(
        "Nome", r => r.Id, r => r.Nome, r => r.Tipo,
        ColunaCadastro<Registro>.Curto("clientes", "Clientes", r => r.Clientes.ToString(), 110),
        ColunaCadastro<Registro>.Selo("situacao", "Situação", r => r.Ativo ? "Ativo" : "Inativo", r => r.Ativo ? "Sucesso" : "Neutro"));

    [Fact]
    public void Monta_parte_fixa_colunas_e_marca_o_registro_aberto()
    {
        var a = new Registro(Guid.NewGuid(), "Ana", "Férias", 3, true);
        var b = new Registro(Guid.NewGuid(), "Bia", "", 0, false);
        var grade = Grade();

        var conteudo = grade.Montar([a, b], aberto: b);

        Assert.Equal(ModoLargura.Proporcional, grade.ColunaFixa.Modo);
        Assert.Equal(3, grade.ColunaFixa.Peso);
        Assert.Equal(["CLIENTES", "SITUAÇÃO"], conteudo.Colunas.Select(c => c.Titulo));
        var linhas = conteudo.Linhas.Cast<LinhaCadastro>().ToList();
        Assert.Equal(a.Id, linhas[0].Chave);
        Assert.Same(a, linhas[0].Item);
        Assert.Equal(("Ana", "Férias", true), (linhas[0].Titulo, linhas[0].Subtitulo, linhas[0].TemSubtitulo));
        Assert.False(linhas[1].TemSubtitulo);
        Assert.Equal("3", linhas[0].Celulas[0].Texto);
        Assert.Equal(("Inativo", "Neutro"), (linhas[1].Celulas[1].Texto, linhas[1].Celulas[1].Tom));
        Assert.False(linhas[0].Selecionada);
        Assert.True(linhas[1].Selecionada);
    }

    [Fact]
    public void Ponteiro_acende_e_apaga_a_linha()
    {
        var grade = Grade();
        var linha = (LinhaCadastro)grade.Montar([new Registro(Guid.NewGuid(), "Ana", "", 0, true)], null).Linhas[0];
        grade.EntrarNaLinhaCommand.Execute(linha);
        Assert.True(linha.Destacada);
        grade.SairDaLinhaCommand.Execute(linha);
        Assert.False(linha.Destacada);
    }

    [Fact]
    public void Clicar_no_titulo_ordena_crescente_decrescente_e_volta_ao_padrao()
    {
        var grade = new GradeCadastro<Registro>(
            "Nome", r => r.Id, r => r.Nome, r => r.Tipo,
            ColunaCadastro<Registro>.Curto("clientes", "Clientes", r => r.Clientes.ToString(), 110, ordem: r => r.Clientes),
            ColunaCadastro<Registro>.Texto("tipo", "Tipo", r => r.Tipo));
        Registro R(string nome, int clientes, string tipo = "") => new(Guid.NewGuid(), nome, tipo, clientes, true);
        var itens = new[] { R("TE-10", 2, "b"), R("te-9", 12), R("TE-100", 3, "A") };
        string Nomes() => string.Join(",", grade.Montar(itens, null).Linhas.Cast<LinhaCadastro>().Select(l => l.Titulo));
        var mudou = 0;
        grade.OrdemMudou += () => mudou++;

        Assert.Equal("TE-10,te-9,TE-100", Nomes()); // padrão: a ordem que veio

        grade.OrdenarColunaCommand.Execute(grade.ColunaFixa);
        Assert.Equal("te-9,TE-10,TE-100", Nomes()); // natural e sem diferenciar maiúsculas
        grade.OrdenarColunaCommand.Execute(grade.ColunaFixa);
        Assert.True(grade.OrdemDecrescente);
        Assert.Equal("TE-100,TE-10,te-9", Nomes());
        grade.OrdenarColunaCommand.Execute(grade.ColunaFixa);
        Assert.Null(grade.ColunaOrdenadaChave);
        Assert.Equal("TE-10,te-9,TE-100", Nomes());

        grade.OrdenarColunaCommand.Execute(grade.Colunas[0]); // pelo número, não pelo texto ("12" depois de "3")
        Assert.Equal("TE-10,TE-100,te-9", Nomes());

        grade.OrdenarColunaCommand.Execute(grade.Colunas[1]); // tipo: vazio fica por último nas duas direções
        Assert.Equal("TE-100,TE-10,te-9", Nomes());
        grade.OrdenarColunaCommand.Execute(grade.Colunas[1]);
        Assert.Equal("TE-10,TE-100,te-9", Nomes());
        Assert.Equal(6, mudou);
    }

    [Fact]
    public void Lista_em_arvore_nao_ordena()
    {
        var grade = new GradeCadastro<Registro>("Nome", r => r.Id, r => r.Nome, r => r.Tipo) { Recuo = _ => 0 };
        grade.OrdenarColunaCommand.Execute(grade.ColunaFixa);
        Assert.Null(grade.ColunaOrdenadaChave);
    }
}
