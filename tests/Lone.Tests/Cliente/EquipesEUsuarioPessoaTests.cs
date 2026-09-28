using Lone.Cliente.ViewModels.Metas;
using Lone.Cliente.ViewModels.Seguranca;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Metas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>
/// Telas da Fase 2a: ligar e tirar a pessoa do usuário (escolher na lista não liga sozinho), alcance do perfil,
/// equipe acima sem ciclo e papel do membro que só muda antes da entrada.
/// </summary>
public class EquipesEUsuarioPessoaTests
{
    [Fact]
    public void Escolher_na_busca_nao_liga_ate_confirmar_e_tirar_desfaz()
    {
        var f = UsuarioFormulario.NovoUsuario();
        Assert.False(f.TemPessoa);
        Assert.False(f.LigarPessoaCommand.CanExecute(null));

        var joao = new PessoaOpcaoDto(Guid.NewGuid(), "João Silva");
        f.BuscaPessoa = "joão";
        f.DefinirResultadosPessoa([joao]);
        f.PessoaEscolhida = joao;
        Assert.False(f.TemPessoa); // só escolheu
        Assert.True(f.LigarPessoaCommand.CanExecute(null));

        f.LigarPessoaCommand.Execute(null);
        Assert.Equal(joao.Id, f.PessoaId);
        Assert.Equal("Ligado a: João Silva", f.TextoPessoa);
        Assert.Empty(f.ResultadosPessoa);
        Assert.Equal(string.Empty, f.BuscaPessoa);
        Assert.Equal(joao.Id, f.ParaRequisicao().Usuario.PessoaId);

        f.TirarPessoaCommand.Execute(null);
        Assert.Null(f.ParaRequisicao().Usuario.PessoaId);
        Assert.Contains("Nenhuma pessoa ligada", f.TextoPessoa);
    }

    [Fact]
    public void Usuario_gravado_mostra_a_pessoa_ligada()
    {
        var dto = new UsuarioDto { Id = Guid.NewGuid(), Nome = "Ana", Login = "ana", PessoaId = Guid.NewGuid(), Pessoa = "Ana Lima" };
        var f = UsuarioFormulario.De(dto, [], [OpcaoEmpresa.Todas]);
        Assert.True(f.TemPessoa);
        Assert.Equal("Ligado a: Ana Lima", f.TextoPessoa);
        Assert.Equal(dto.PessoaId, f.ParaRequisicao().Usuario.PessoaId);
    }

    [Fact]
    public void Alcance_do_perfil_vai_no_dto_e_administrador_ve_tudo()
    {
        var f = PerfilFormulario.De(new PerfilDto { Id = Guid.NewGuid(), Nome = "Vendas", AlcanceComercial = AlcanceComercial.MinhaCarteira }, []);
        Assert.Equal(AlcanceComercial.MinhaCarteira, f.Alcance.Valor);
        Assert.Equal(AlcanceComercial.MinhaCarteira, f.ParaDto().AlcanceComercial);

        f.Administrador = true;
        Assert.Equal(AlcanceComercial.Tudo, f.ParaDto().AlcanceComercial);
    }

    [Fact]
    public void Equipe_acima_tira_a_propria_e_as_de_baixo_e_mantem_a_gravada_desativada()
    {
        var regional = new EquipeDto { Id = Guid.NewGuid(), Nome = "Regional" };
        var sul = new EquipeDto { Id = Guid.NewGuid(), Nome = "Sul", EquipePaiId = regional.Id };
        var litoral = new EquipeDto { Id = Guid.NewGuid(), Nome = "Litoral", EquipePaiId = sul.Id };
        var velha = new EquipeDto { Id = Guid.NewGuid(), Nome = "Velha", Ativo = false };
        var outra = new EquipeDto { Id = Guid.NewGuid(), Nome = "Outra", Ativo = false };
        var todas = new List<EquipeDto> { regional, sul, litoral, velha, outra };

        var opcoes = EquipeEdicao.EquipesAcimaPossiveis(sul.Id, todas, velha.Id);
        Assert.Equal(new Guid?[] { null, regional.Id, velha.Id }, opcoes.Select(o => o.Valor));
        Assert.Equal("Velha (desativada)", opcoes.Single(o => o.Valor == velha.Id).Texto);
    }

    [Fact]
    public void Ficha_da_equipe_mostra_o_lider_de_hoje_e_manda_a_equipe_acima_e_o_papel()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var regional = new EquipeDto { Id = Guid.NewGuid(), Nome = "Regional" };
        var pessoa = Guid.NewGuid();
        var sul = new EquipeDto
        {
            Id = Guid.NewGuid(), Nome = "Sul", EquipePaiId = regional.Id, Lider = "Ana",
            Membros =
            [
                new MembroEquipeDto { Id = Guid.NewGuid(), PessoaId = pessoa, Pessoa = "Ana", InicioEm = hoje.AddDays(-30), Papel = PapelNaEquipe.Lider },
                new MembroEquipeDto { Id = Guid.NewGuid(), PessoaId = Guid.NewGuid(), Pessoa = "Bia", InicioEm = hoje.AddDays(5) }
            ]
        };

        var f = EquipeEdicao.De(sul, new MetaOpcoesDto(), [regional, sul]);
        Assert.Equal("Ana", f.LiderHoje);
        Assert.Equal(regional.Id, f.EquipeAcima.Valor);

        var ana = f.Membros.Single(m => m.ParaDto().PessoaId == pessoa);
        var bia = f.Membros.Single(m => m.ParaDto().PessoaId != pessoa);
        Assert.False(ana.PodeTrocarPapel); // já entrou: o histórico não é reescrito
        Assert.True(bia.PodeTrocarPapel);  // planejada: ainda pode mudar

        var dto = f.ParaDto();
        Assert.Equal(regional.Id, dto.EquipePaiId);
        Assert.Equal(PapelNaEquipe.Lider, dto.Membros.Single(m => m.PessoaId == pessoa).Papel);
        Assert.Null(dto.LiderId); // o servidor calcula
    }

    [Fact]
    public void Membro_novo_comeca_como_membro_e_pode_virar_lider()
    {
        var m = MembroEquipeFormulario.Novo([]);
        Assert.Equal(PapelNaEquipe.Membro, m.Papel.Valor);
        Assert.True(m.PodeTrocarPapel);
        m.Papel = MembroEquipeFormulario.Papeis[1];
        Assert.Equal(PapelNaEquipe.Lider, m.ParaDto().Papel);
    }
}
