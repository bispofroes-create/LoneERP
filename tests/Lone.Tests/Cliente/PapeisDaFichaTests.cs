using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Papeis;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using Lone.Domain.Papeis;

namespace Lone.Tests.Cliente;

/// <summary>
/// Papéis na Identificação: a ficha mostra só o que a pessoa tem ou já teve; "Adicionar papel" oferece o resto com
/// pesquisa; o interruptor muda só a ficha (os períodos continuam sendo abertos/encerrados ao salvar, nada é apagado).
/// </summary>
public class PapeisDaFichaTests
{
    private static readonly PapelCadastroDto Parceiro = new() { Id = Guid.NewGuid(), Codigo = "PARCEIRO", Nome = "Parceiro", Descricao = "Indica clientes", Ordem = 20, Ativo = true };
    private static readonly PapelCadastroDto Antigo = new() { Id = Guid.NewGuid(), Codigo = "ANTIGO", Nome = "Antigo", Ordem = 30, Ativo = false };

    private static PapelDto Aberto(TipoPapel papel, DateOnly inicio) =>
        new() { Id = Guid.NewGuid(), PapelId = PapeisSistema.Id(papel), Papel = papel, Ativo = true, InicioEm = inicio };

    private static PapelDto Encerrado(Guid papelId, DateOnly inicio, DateOnly fim) =>
        new() { Id = Guid.NewGuid(), PapelId = papelId, Ativo = false, InicioEm = inicio, FimEm = fim };

    private static PessoaFormulario Ficha(params PapelDto[] periodos) =>
        PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Ana", Papeis = [.. periodos] }, papeis: [Parceiro, Antigo]);

    [Fact]
    public void Ficha_mostra_so_os_papeis_que_a_pessoa_tem_ou_ja_teve()
    {
        var f = Ficha(
            Aberto(TipoPapel.Cliente, new DateOnly(2026, 9, 26)),
            Encerrado(PapeisSistema.Id(TipoPapel.Vendedor), new DateOnly(2026, 5, 2), new DateOnly(2026, 6, 20)));

        var cartoes = f.PapeisFicha.NaFicha;
        Assert.Equal(new[] { "Cliente", "Vendedor" }, cartoes.Select(p => p.Nome));

        var cliente = cartoes[0];
        Assert.Equal("● Ativo", cliente.EstadoTexto);
        Assert.Equal("Desde 26/09/2026", cliente.Periodo);

        var vendedor = cartoes[1];
        Assert.Equal("○ Inativo", vendedor.EstadoTexto);
        Assert.Equal("Último período: 02/05/2026 → 20/06/2026", vendedor.Periodo);
        Assert.Equal(new[] { new PeriodoPapel("○ Encerrado", "02/05/2026 → 20/06/2026") }, vendedor.Historico);
    }

    [Fact]
    public void Sem_papeis_a_ficha_fica_vazia_e_o_painel_oferece_todos_os_ativos_do_cadastro()
    {
        var f = Ficha();

        Assert.True(f.PapeisFicha.Vazio);
        // Os oito de sistema + Parceiro; "Antigo" está desativado no cadastro e ninguém o tem: não é oferecido.
        Assert.Equal(PapeisSistema.Todos.Count + 1, f.PapeisFicha.Disponiveis.Count);
        Assert.DoesNotContain(f.PapeisFicha.Disponiveis, p => p.PapelId == Antigo.Id);
    }

    [Fact]
    public void Adicionar_pelo_painel_liga_o_papel_mostra_o_cartao_e_o_periodo_comeca_ao_salvar()
    {
        var f = Ficha();
        f.PapeisFicha.AlternarEscolhaCommand.Execute(null);
        f.PapeisFicha.Busca = "indica"; // pesquisa também na descrição, sem acento/maiúscula

        var parceiro = Assert.Single(f.PapeisFicha.Disponiveis);
        f.PapeisFicha.AdicionarCommand.Execute(parceiro);

        Assert.True(parceiro.Ativo);
        Assert.Same(parceiro, Assert.Single(f.PapeisFicha.NaFicha));
        Assert.Equal("Começa ao salvar", parceiro.Periodo);
        Assert.False(f.PapeisFicha.Escolhendo);
        Assert.DoesNotContain(parceiro, f.PapeisFicha.Disponiveis); // não duplica
        var dto = Assert.Single(f.ParaDto().Papeis);
        Assert.True(dto.Ativo);
        Assert.Equal(Guid.Empty, dto.Id); // período novo: a API começa na data da gravação
    }

    [Fact]
    public void Desligar_encerra_ao_salvar_sem_apagar_e_religar_mantem_o_mesmo_periodo()
    {
        var aberto = Aberto(TipoPapel.Cliente, new DateOnly(2025, 1, 1));
        var anterior = Encerrado(PapeisSistema.Id(TipoPapel.Cliente), new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30));
        var f = Ficha(anterior, aberto);
        var cliente = Assert.Single(f.PapeisFicha.NaFicha);

        cliente.Ativo = false; // o interruptor

        Assert.Same(cliente, Assert.Single(f.PapeisFicha.NaFicha)); // continua na ficha (tem histórico)
        Assert.Equal("Desde 01/01/2025 · encerra ao salvar", cliente.Periodo);
        var periodos = f.ParaDto().Papeis;
        Assert.Equal(2, periodos.Count); // nada é apagado
        Assert.All(periodos, p => Assert.False(p.Ativo));
        Assert.Contains(periodos, p => p.Id == aberto.Id);

        cliente.Ativo = true; // mudou de ideia antes de salvar: o mesmo período continua aberto
        Assert.Contains(f.ParaDto().Papeis, p => p.Id == aberto.Id && p.Ativo);
        Assert.Equal(new[] { "● Em vigor", "○ Encerrado" }, cliente.Historico.Select(h => h.Estado));
    }

    [Fact]
    public void Papel_novo_desligado_antes_de_salvar_some_da_ficha()
    {
        var f = Ficha();
        var parceiro = f.PapeisFicha.Disponiveis.Single(p => p.PapelId == Parceiro.Id);
        f.PapeisFicha.AdicionarCommand.Execute(parceiro);

        parceiro.Ativo = false;

        Assert.True(f.PapeisFicha.Vazio);
        Assert.Contains(parceiro, f.PapeisFicha.Disponiveis);
        Assert.Empty(f.ParaDto().Papeis);
    }

    [Fact]
    public void Permissoes_valem_tambem_na_tela()
    {
        var f = Ficha(Aberto(TipoPapel.Cliente, new DateOnly(2025, 1, 1)));

        f.PapeisFicha.DefinirPermissoes(podeEditar: true, podeEmpresasDoGrupo: false);
        Assert.DoesNotContain(f.PapeisFicha.Disponiveis, p => p.Papel == TipoPapel.EmpresaDoGrupo);
        Assert.False(f.PapelEmpresaDoGrupo.Editavel);
        Assert.True(f.PapelCliente.Editavel);

        f.PapeisFicha.DefinirPermissoes(podeEditar: false, podeEmpresasDoGrupo: true);
        f.PapeisFicha.AlternarEscolhaCommand.Execute(null);
        Assert.False(f.PapeisFicha.PodeEditar);
        Assert.False(f.PapeisFicha.Escolhendo);
        Assert.False(f.PapelCliente.Editavel);
        Assert.Empty(f.PapeisFicha.Disponiveis);
    }

    [Fact]
    public void Papel_desativado_no_cadastro_com_historico_aparece_mas_nao_religa()
    {
        var f = Ficha(Encerrado(Antigo.Id, new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31)));
        f.PapeisFicha.DefinirPermissoes(podeEditar: true, podeEmpresasDoGrupo: true);

        var antigo = Assert.Single(f.PapeisFicha.NaFicha);
        Assert.Equal("Antigo (desativado)", antigo.Texto);
        Assert.False(antigo.Editavel); // a API não deixa começar período novo de papel desativado
        Assert.Contains(f.ParaDto().Papeis, p => p.PapelId == Antigo.Id); // o período antigo volta intacto
    }
}
