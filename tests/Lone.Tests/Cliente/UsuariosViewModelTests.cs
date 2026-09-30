using System.Net;
using Lone.Cliente.Api;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Seguranca;
using Lone.Contracts.Comum;
using Lone.Contracts.Empresas;
using Lone.Contracts.Seguranca;
using Lone.Cliente.Mensagens;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Cliente;

public class UsuariosViewModelTests
{
    private static readonly PerfilResumo Vendas = new() { Id = Guid.NewGuid(), Nome = "Vendas", Ativo = true };

    private static readonly List<UsuarioResumo> Usuarios =
    [
        new() { Id = Guid.NewGuid(), Nome = "Bruno", Login = "bruno", Ativo = true },
        new() { Id = Guid.NewGuid(), Nome = "Ana", Login = "ana", Ativo = true }
    ];

    private static async Task<(UsuariosViewModel Tela, AmbienteCliente Ambiente)> AbrirTelaAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, new List<PerfilResumo> { Vendas })
            .Responder(HttpStatusCode.OK, new List<EmpresaResumo> { new(Guid.NewGuid(), "Matriz", true) })
            .Responder(HttpStatusCode.OK, Usuarios);

        var tela = new UsuariosViewModel(new UsuariosApi(ambiente.Api), ambiente.Dialogos);
        await tela.CarregarCommand.ExecuteAsync(null);
        return (tela, ambiente);
    }

    [Fact]
    public async Task Carrega_a_lista_em_ordem_de_nome_e_as_opcoes_de_perfil_e_empresa()
    {
        var (tela, _) = await AbrirTelaAsync();

        Assert.Equal(new[] { "Ana", "Bruno" }, tela.Itens.Select(u => u.Nome));
        Assert.Equal("Vendas", Assert.Single(tela.PerfisDisponiveis).Nome);
        Assert.Equal(new[] { "Todas as empresas", "Matriz" }, tela.EmpresasDisponiveis.Select(e => e.Nome));
        Assert.Same(OpcaoEmpresa.Todas, tela.EmpresaParaAdicionar);
    }

    [Fact]
    public async Task Busca_filtra_por_nome_ou_login()
    {
        var (tela, _) = await AbrirTelaAsync();

        tela.Busca = "BRU";

        Assert.Equal("Bruno", Assert.Single(tela.Itens).Nome);
    }

    [Fact]
    public async Task Novo_usuario_abre_a_ficha_e_no_celular_esconde_a_lista()
    {
        var (tela, _) = await AbrirTelaAsync();
        tela.ModoCompacto = true;

        await tela.NovoCommand.ExecuteAsync(null);

        Assert.True(tela.Formulario!.Novo);
        Assert.True(tela.MostrarFicha);
        Assert.False(tela.MostrarLista);

        tela.FecharFichaCommand.Execute(null);
        Assert.True(tela.MostrarLista);
    }

    [Fact]
    public async Task Mesmo_perfil_na_mesma_empresa_nao_entra_duas_vezes()
    {
        var (tela, _) = await AbrirTelaAsync();
        await tela.NovoCommand.ExecuteAsync(null);

        tela.PerfilParaAdicionar = tela.PerfisDisponiveis[0];
        tela.AdicionarPerfilCommand.Execute(null);
        tela.PerfilParaAdicionar = tela.PerfisDisponiveis[0];
        tela.AdicionarPerfilCommand.Execute(null);

        Assert.Single(tela.Formulario!.Perfis);
        Assert.Equal(TipoMensagem.Aviso, tela.TipoMensagem);
    }

    [Fact]
    public async Task Salvar_usuario_novo_envia_para_o_id_gerado_e_confirma()
    {
        var (tela, ambiente) = await AbrirTelaAsync();
        await tela.NovoCommand.ExecuteAsync(null);
        var formulario = tela.Formulario!;
        formulario.Nome = "Carla";
        formulario.Login = "carla";
        formulario.NovaSenha = formulario.Confirmacao = "Senha123";
        tela.PerfilParaAdicionar = tela.PerfisDisponiveis[0];
        tela.AdicionarPerfilCommand.Execute(null);

        var salvo = new UsuarioDto
        {
            Id = formulario.Id, Nome = "Carla", Login = "carla",
            Perfis = [new UsuarioPerfilDto(Vendas.Id, null)]
        };
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, salvo)
            .Responder(HttpStatusCode.OK, Usuarios);

        tela.Mensagens = new ServicoMensagens(new FakeTimeProvider());
        await tela.SalvarCommand.ExecuteAsync(null);

        var put = ambiente.Servidor.Recebidas[^2];
        Assert.Equal(HttpMethod.Put, put.Metodo);
        Assert.Equal("/" + Rotas.Usuarios.PorId(formulario.Id), put.Caminho);
        Assert.Contains("\"novaSenha\":\"Senha123\"", put.Corpo);
        Assert.Equal("Usuário cadastrado.", Assert.Single(tela.Mensagens.Visiveis).Texto);
        Assert.False(tela.TemMensagem);
        Assert.False(tela.Formulario!.Novo);
        Assert.Equal(string.Empty, tela.Formulario.NovaSenha);
    }

    [Fact]
    public async Task Erro_de_validacao_da_API_aparece_na_ficha_sem_fecha_la()
    {
        var (tela, ambiente) = await AbrirTelaAsync();
        await tela.NovoCommand.ExecuteAsync(null);
        var formulario = tela.Formulario!;
        formulario.NovaSenha = formulario.Confirmacao = "Senha123";
        tela.PerfilParaAdicionar = tela.PerfisDisponiveis[0];
        tela.AdicionarPerfilCommand.Execute(null);
        ambiente.Servidor.Problema(HttpStatusCode.BadRequest, ErrosApi.Validacao, "Dados inválidos.",
            (ErrosApi.CampoErros, new[] { "Informe o nome." }));

        await tela.SalvarCommand.ExecuteAsync(null);

        Assert.Equal("Informe o nome.", tela.Mensagem);
        Assert.True(tela.Editando);
        Assert.Same(formulario, tela.Formulario);
    }
}
