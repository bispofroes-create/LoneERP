using Lone.Cliente.ViewModels.Seguranca;
using Lone.Contracts.Seguranca;

namespace Lone.Tests.Cliente;

public class FormulariosSegurancaTests
{
    private static readonly OpcaoPerfil Vendas = new(Guid.NewGuid(), "Vendas");
    private static readonly OpcaoEmpresa Matriz = new(Guid.NewGuid(), "Matriz");

    [Fact]
    public void Usuario_novo_exige_senha_confirmada_e_perfil()
    {
        var formulario = UsuarioFormulario.NovoUsuario();
        formulario.NovaSenha = "Senha123";
        formulario.Confirmacao = "Outra123";

        var erros = formulario.ValidarLocalmente();

        Assert.Contains("A confirmação não confere com a senha.", erros);
        Assert.Contains("Escolha ao menos um perfil.", erros);
    }

    [Fact]
    public void Usuario_existente_mostra_os_perfis_com_os_nomes_e_a_empresa()
    {
        var dto = new UsuarioDto
        {
            Id = Guid.NewGuid(),
            Nome = "Ana",
            Login = "ana",
            Perfis = [new UsuarioPerfilDto(Vendas.Id, Matriz.Id), new UsuarioPerfilDto(Vendas.Id, null)]
        };

        var formulario = UsuarioFormulario.De(dto, [Vendas], [OpcaoEmpresa.Todas, Matriz]);

        Assert.False(formulario.Novo);
        Assert.Equal(new[] { "Vendas — Matriz", "Vendas — Todas as empresas" }, formulario.Perfis.Select(p => p.Texto));
    }

    [Fact]
    public void Alteracao_sem_senha_nao_envia_senha_e_mantem_a_versao()
    {
        var dto = new UsuarioDto { Id = Guid.NewGuid(), Versao = [1, 2, 3], Nome = "Ana", Login = "ana", Email = " " };
        var formulario = UsuarioFormulario.De(dto, [Vendas], [OpcaoEmpresa.Todas]);
        formulario.Perfis.Add(new PerfilAtribuido(Vendas, OpcaoEmpresa.Todas));

        var requisicao = formulario.ParaRequisicao();

        Assert.Null(requisicao.NovaSenha);
        Assert.Null(requisicao.Usuario.Email);
        Assert.Equal(new byte[] { 1, 2, 3 }, requisicao.Usuario.Versao);
        Assert.Equal(new UsuarioPerfilDto(Vendas.Id, null), Assert.Single(requisicao.Usuario.Perfis));
    }

    [Fact]
    public void Perfil_agrupa_as_permissoes_por_modulo_e_marca_as_que_ja_tem()
    {
        var dto = new PerfilDto { Id = Guid.NewGuid(), Nome = "Vendas", Permissoes = [Permissoes.Pessoas.Visualizar] };

        var formulario = PerfilFormulario.De(dto, Permissoes.Todas);

        // Um grupo por módulo do catálogo, na ordem do catálogo (novos módulos entram sem mudar o teste).
        Assert.Equal(Permissoes.Todas.Select(p => p.Modulo).Distinct(), formulario.Grupos.Select(g => g.Modulo));
        var marcadas = formulario.Grupos.SelectMany(g => g.Permissoes).Where(p => p.Marcada).Select(p => p.Codigo);
        Assert.Equal(new[] { Permissoes.Pessoas.Visualizar }, marcadas);
    }

    [Fact]
    public void Perfil_administrador_nao_envia_lista_de_permissoes()
    {
        var formulario = PerfilFormulario.NovoPerfil(Permissoes.Todas);
        formulario.Nome = "Gerência";
        formulario.Grupos[0].Permissoes[0].Marcada = true;
        formulario.Administrador = true;

        var dto = formulario.ParaDto();

        Assert.Empty(dto.Permissoes);
        Assert.False(formulario.MostrarPermissoes);
    }
}
