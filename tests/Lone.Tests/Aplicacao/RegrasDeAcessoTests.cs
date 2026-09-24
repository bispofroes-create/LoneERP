using Lone.Application.Seguranca;
using Lone.Contracts.Empresas;
using Lone.Contracts.Seguranca;

namespace Lone.Tests.Aplicacao;

public class RegrasDeAcessoTests
{
    private static readonly Guid EmpresaA = Guid.NewGuid();
    private static readonly Guid EmpresaB = Guid.NewGuid();

    private static readonly IReadOnlyList<EmpresaAtiva> Estabelecimentos =
    [
        new(Guid.NewGuid(), EmpresaA, "Empresa A", "11222333000181", true),
        new(Guid.NewGuid(), EmpresaA, "Empresa A - Filial", "11222333000262", false),
        new(Guid.NewGuid(), EmpresaB, "Empresa B", null, true)
    ];

    private static PerfilAtribuido Perfil(Guid? empresa, params string[] permissoes) =>
        new(empresa, false, permissoes.ToHashSet());

    [Fact]
    public void Perfil_em_todas_as_empresas_libera_todos_os_estabelecimentos()
    {
        var disponiveis = RegrasDeAcesso.EmpresasDisponiveis([Perfil(null, Permissoes.Pessoas.Visualizar)], Estabelecimentos);
        Assert.Equal(3, disponiveis.Count);
    }

    [Fact]
    public void Perfil_de_uma_empresa_libera_so_os_estabelecimentos_dela()
    {
        var disponiveis = RegrasDeAcesso.EmpresasDisponiveis([Perfil(EmpresaA, Permissoes.Pessoas.Visualizar)], Estabelecimentos);

        Assert.Equal(2, disponiveis.Count);
        Assert.All(disponiveis, e => Assert.Equal(EmpresaA, e.EmpresaId));
    }

    [Fact]
    public void Permissoes_somam_perfis_gerais_e_os_da_empresa_ativa()
    {
        IReadOnlyList<PerfilAtribuido> perfis =
        [
            Perfil(null, Permissoes.Pessoas.Visualizar),
            Perfil(EmpresaA, Permissoes.Pessoas.Editar),
            Perfil(EmpresaB, Permissoes.Pessoas.Inativar)
        ];

        var naA = RegrasDeAcesso.Efetivo(perfis, EmpresaA);

        Assert.True(naA.Possui(Permissoes.Pessoas.Visualizar));
        Assert.True(naA.Possui(Permissoes.Pessoas.Editar));
        Assert.False(naA.Possui(Permissoes.Pessoas.Inativar));
    }

    [Fact]
    public void Sem_empresa_ativa_valem_so_os_perfis_gerais()
    {
        var efetivo = RegrasDeAcesso.Efetivo([Perfil(EmpresaA, Permissoes.Pessoas.Editar)], empresaId: null);
        Assert.False(efetivo.Possui(Permissoes.Pessoas.Editar));
    }

    [Fact]
    public void Administrador_tem_qualquer_permissao()
    {
        var efetivo = RegrasDeAcesso.Efetivo([new PerfilAtribuido(null, true, new HashSet<string>())], null);
        Assert.True(efetivo.Possui(Permissoes.Seguranca.GerenciarUsuarios));
    }
}
