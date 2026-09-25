using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Enderecos;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class EnderecosFormularioTests
{
    private static readonly TipoEnderecoDto Sede = new() { Id = Guid.NewGuid(), Nome = "Sede", Ordem = 1, Ativo = true };
    private static readonly TipoEnderecoDto Antigo = new() { Id = Guid.NewGuid(), Nome = "Galpão", Ordem = 2, Ativo = false };

    private static PessoaDto ComEndereco(Guid? tipo = null) => new()
    {
        Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica,
        Enderecos =
        [
            new EnderecoDto
            {
                Id = Guid.NewGuid(), Logradouro = "Rua A", Cidade = "Belo Horizonte", Uf = "MG", MunicipioId = 3106200,
                Finalidades = FinalidadeEndereco.Principal | FinalidadeEndereco.Entrega, TipoEnderecoId = tipo, Observacoes = "Portão lateral"
            }
        ]
    };

    [Fact]
    public void Remover_endereco_gravado_desativa_tira_o_principal_e_ele_volta_no_envio()
    {
        var f = PessoaFormulario.De(ComEndereco(), tiposEndereco: [Sede]);
        var endereco = Assert.Single(f.Enderecos);

        endereco.RemoverCommand.Execute(null);

        Assert.Single(f.Enderecos);
        Assert.False(endereco.Visivel);
        Assert.True(f.TemEnderecosInativos);
        var enviado = Assert.Single(f.ParaDto().Enderecos);
        Assert.False(enviado.Ativo);
        Assert.False(enviado.Finalidades.HasFlag(FinalidadeEndereco.Principal));
        Assert.True(enviado.Finalidades.HasFlag(FinalidadeEndereco.Entrega));

        f.MostrarEnderecosInativos = true;
        Assert.True(endereco.Visivel);
        endereco.ReativarCommand.Execute(null);
        Assert.True(f.ParaDto().Enderecos[0].Ativo);
    }

    [Fact]
    public void Remover_endereco_novo_tira_da_lista()
    {
        var f = PessoaFormulario.NovaPessoa();
        var endereco = f.Enderecos[0];

        endereco.RemoverCommand.Execute(null);

        Assert.Empty(f.Enderecos);
    }

    [Fact]
    public void Tipo_desativado_aparece_so_para_quem_ja_tinha()
    {
        var f = PessoaFormulario.De(ComEndereco(Antigo.Id), tiposEndereco: [Sede, Antigo]);
        var endereco = f.Enderecos[0];

        Assert.Equal("Galpão (desativado)", endereco.Tipo.Texto);
        Assert.Equal(Antigo.Id, f.ParaDto().Enderecos[0].TipoEnderecoId);
        Assert.Equal("Portão lateral", f.ParaDto().Enderecos[0].Observacoes);

        var novo = new EnderecoFormulario();
        f.AdicionarEndereco(novo);
        Assert.DoesNotContain(novo.Tipos, o => o.Valor == Antigo.Id);
        Assert.Contains(novo.Tipos, o => o.Valor == Sede.Id);
    }

    [Fact]
    public void Sem_a_lista_de_tipos_o_tipo_gravado_volta_intacto()
    {
        var f = PessoaFormulario.De(ComEndereco(Sede.Id));

        Assert.Equal(Sede.Id, f.ParaDto().Enderecos[0].TipoEnderecoId);
    }

    [Fact]
    public void Endereco_inativo_nao_e_conferido_na_tela()
    {
        var dto = ComEndereco();
        dto.Enderecos[0].MunicipioId = null;
        dto.Enderecos[0].Ativo = false;
        var f = PessoaFormulario.De(dto);

        Assert.DoesNotContain(f.ValidarLocalmente(), e => e.StartsWith("Endereço 1", StringComparison.Ordinal));
    }
}
