using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Papeis;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using Lone.Domain.Papeis;

namespace Lone.Tests.Cliente;

public class PapeisFormularioTests
{
    private static readonly PapelCadastroDto Parceiro = new() { Id = Guid.NewGuid(), Codigo = "PARCEIRO", Nome = "Parceiro", Ordem = 20, Ativo = true };
    private static readonly PapelCadastroDto Antigo = new() { Id = Guid.NewGuid(), Codigo = "ANTIGO", Nome = "Antigo", Ordem = 30, Ativo = false };

    [Fact]
    public void Ficha_mostra_os_de_sistema_os_ativos_do_cadastro_e_os_desativados_que_a_pessoa_tem()
    {
        var dto = new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Ana",
            Papeis = [new PapelDto { Id = Guid.NewGuid(), PapelId = Antigo.Id, Ativo = true, InicioEm = new DateOnly(2025, 1, 1) }]
        };

        var f = PessoaFormulario.De(dto, papeis: [Parceiro, Antigo]);

        Assert.Equal(PapeisSistema.Todos.Count + 2, f.Papeis.Count);
        Assert.Equal("Parceiro", f.Papeis[^2].Texto);
        Assert.Equal("Antigo (desativado)", f.Papeis[^1].Texto);
        Assert.Equal("desde 01/01/2025", f.Papeis[^1].Detalhe);
        Assert.True(f.PapelCliente.Papel == TipoPapel.Cliente);
    }

    [Fact]
    public void Desmarcar_encerra_e_marcar_de_novo_comeca_outro_periodo()
    {
        var encerrado = new PapelDto { Id = Guid.NewGuid(), PapelId = Parceiro.Id, Ativo = false, InicioEm = new DateOnly(2024, 1, 1), FimEm = new DateOnly(2024, 6, 30) };
        var aberto = new PapelDto { Id = Guid.NewGuid(), PapelId = Parceiro.Id, Ativo = true, InicioEm = new DateOnly(2025, 1, 1) };
        var opcao = new PapelOpcao(Parceiro.Id, null, "Parceiro", true, [encerrado, aberto]);

        opcao.Ativo = false;
        var desmarcado = opcao.ParaDtos().ToList();
        Assert.Equal(2, desmarcado.Count);
        Assert.All(desmarcado, p => Assert.False(p.Ativo));
        Assert.Equal(aberto.Id, desmarcado[1].Id); // o aberto é encerrado, não apagado

        var novo = new PapelOpcao(Parceiro.Id, null, "Parceiro", true, [encerrado]) { Ativo = true };
        var marcado = novo.ParaDtos().ToList();
        Assert.Equal(2, marcado.Count);
        Assert.Equal(Guid.Empty, marcado[1].Id); // período novo (a API começa hoje)
        Assert.Equal(encerrado.FimEm, marcado[0].FimEm);
    }

    [Fact]
    public void Periodo_de_papel_fora_do_cadastro_volta_intacto()
    {
        var desconhecido = new PapelDto { Id = Guid.NewGuid(), PapelId = Guid.NewGuid(), Ativo = false, InicioEm = new DateOnly(2020, 1, 1) };
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Ana", Papeis = [desconhecido] });

        Assert.Contains(f.ParaDto().Papeis, p => p.Id == desconhecido.Id);
    }

    [Fact]
    public void Codigo_do_papel_so_muda_na_criacao()
    {
        var existente = PapelEdicao.De(Parceiro);
        Assert.False(existente.PodeMudarCodigo);
        Assert.True(PapelEdicao.Criar().PodeMudarCodigo);

        var novo = PapelEdicao.Criar();
        novo.Nome = "Parceiro de negócios";
        novo.Ordem = "x";
        Assert.Contains("Ordem: use um número inteiro.", novo.ValidarLocalmente());
    }
}
