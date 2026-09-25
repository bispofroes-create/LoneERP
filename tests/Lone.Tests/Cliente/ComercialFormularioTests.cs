using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comercial;
using Lone.Contracts.Pessoas;

namespace Lone.Tests.Cliente;

public class ComercialFormularioTests
{
    private static readonly TipoCarteiraDto Vendedor = new() { Id = Guid.NewGuid(), Nome = "Vendedor", Principal = true, Ordem = 1 };
    private static readonly PerfilComercialDto Atacado = new() { Id = Guid.NewGuid(), Nome = "Atacado", DescontoMaximo = 12 };
    private static readonly CondicaoPagamentoDto Trinta = new() { Id = Guid.NewGuid(), Nome = "30 dias", Parcelas = "30" };

    private static ComercialOpcoesDto Opcoes() => new()
    {
        Perfis = [Atacado],
        Condicoes = [Trinta],
        TiposCarteira = [Vendedor],
        Vendedores = [new PessoaOpcaoDto(Guid.NewGuid(), "João Vendedor")]
    };

    [Fact]
    public void Carteira_nova_ja_vem_com_o_tipo_principal_e_gravada_so_desativa()
    {
        var gravada = new CarteiraDto { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Guid.NewGuid(), Vendedor = "Ana", InicioEm = new DateOnly(2025, 1, 1) };
        var f = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Cliente", Carteira = [gravada] });
        f.DefinirOpcoesComercial(Opcoes());

        f.NovaCarteira();
        Assert.Equal(Vendedor.Id, f.Carteira[0].Tipo.Valor);

        var antiga = f.Carteira[1];
        antiga.RemoverCommand.Execute(null);
        Assert.Equal(2, f.Carteira.Count);
        Assert.False(antiga.ParaDto().Ativo);
        Assert.Contains(antiga.Vendedores, v => v.Texto.Contains("sem o papel"));
    }

    [Fact]
    public void Perfil_e_condicao_vao_no_envio_e_o_resumo_mostra_o_desconto_do_perfil()
    {
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Cliente",
            ContasCliente = [new ContaClienteDto { Id = Guid.NewGuid(), PerfilComercialId = Atacado.Id, DescontoMaximo = 5 }]
        });

        // Antes de ler as opções, o perfil gravado volta intacto.
        Assert.Equal(Atacado.Id, f.ParaDto().ContasCliente.Single(c => c.EmpresaId is null).PerfilComercialId);

        f.DefinirOpcoesComercial(Opcoes());
        f.ContaCliente.Condicao = f.ContaCliente.Condicoes.First(c => c.Valor == Trinta.Id);

        Assert.Equal(Trinta.Id, f.ParaDto().ContasCliente.Single(c => c.EmpresaId is null).CondicaoPagamentoId);
        Assert.Contains("desconto máximo 12%", f.ResumoComercial);
    }

    [Fact]
    public void Excecao_nova_sai_da_lista_e_gravada_fica()
    {
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Cliente",
            ExcecoesComerciais = [new ExcecaoComercialDto { Id = Guid.NewGuid(), InicioEm = new DateOnly(2026, 3, 1), DescontoMaximo = 15 }]
        });
        f.NovaExcecao();
        Assert.Equal(2, f.Excecoes.Count);

        f.Excecoes[0].RemoverCommand.Execute(null);
        f.Excecoes[0].RemoverCommand.Execute(null);

        Assert.Single(f.Excecoes);
        Assert.Equal(15, f.ParaDto().ExcecoesComerciais[0].DescontoMaximo);
    }
}
