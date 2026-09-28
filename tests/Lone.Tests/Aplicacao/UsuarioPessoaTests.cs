using Lone.Application.Seguranca;
using Lone.Contracts.Seguranca;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Tests.Apoio;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// Usuário ligado a uma pessoa (Motor Comercial, Fase 2a, decisão F1): a pessoa precisa existir e estar ativa, fica
/// ligada a um só usuário, a ligação volta no DTO com o nome e pode ser tirada. A conferência só roda quando a pessoa muda.
/// </summary>
public class UsuarioPessoaTests
{
    private static readonly Guid PerfilVendas = Guid.NewGuid();
    private static readonly Guid Joao = Guid.NewGuid();
    private static readonly Guid Maria = Guid.NewGuid();
    private static readonly Guid Inativo = Guid.NewGuid();

    private readonly UsuariosEmMemoria _usuarios = new();
    private readonly AutorizacaoFixa _autorizacao = new();
    private readonly UsuarioAppService _servico;

    public UsuarioPessoaTests()
    {
        _usuarios.Pessoas[Joao] = ("João Silva", true);
        _usuarios.Pessoas[Maria] = ("Maria Souza", true);
        _usuarios.Pessoas[Inativo] = ("Pedro Antigo", false);
        _servico = new UsuarioAppService(_usuarios, new PerfisFixos(), new TokensEmMemoria(new FakeTimeProvider()), new HasherRapido(),
            _autorizacao, new UsuarioFixo(), new EmpresasFixas());
    }

    private Usuario Gravado(string login, Guid? pessoa = null)
    {
        var id = Guid.NewGuid();
        var u = new Usuario
        {
            Id = id, Login = login, Nome = login, SenhaHash = "teste:x", Ativo = true, PessoaId = pessoa,
            Perfis = [new UsuarioPerfil { Id = Guid.NewGuid(), UsuarioId = id, PerfilId = PerfilVendas }]
        };
        _usuarios.Usuarios.Add(u);
        return u;
    }

    private Task<UsuarioDto> Salvar(Usuario u, Guid? pessoa) => _servico.SalvarAsync(new SalvarUsuarioRequisicao(new UsuarioDto
    {
        Id = u.Id, Nome = u.Nome, Login = u.Login, Ativo = true, PessoaId = pessoa,
        Perfis = [new UsuarioPerfilDto(PerfilVendas, null)]
    }, null));

    [Fact]
    public async Task Liga_a_pessoa_e_devolve_o_nome()
    {
        var ana = Gravado("ana");
        var dto = await Salvar(ana, Joao);
        Assert.Equal(Joao, dto.PessoaId);
        Assert.Equal("João Silva", dto.Pessoa);
        Assert.Equal(Joao, _usuarios.Usuarios.Single(u => u.Id == ana.Id).PessoaId);
    }

    [Fact]
    public async Task Pessoa_ja_ligada_a_outro_usuario_nao_passa()
    {
        Gravado("bruno", Joao);
        var ana = Gravado("ana");
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => Salvar(ana, Joao));
        Assert.Contains(erro.Erros, e => e.Contains("João Silva já está ligada a outro usuário"));
    }

    [Fact]
    public async Task Pessoa_inativa_ou_inexistente_nao_passa()
    {
        var ana = Gravado("ana");
        var inativa = await Assert.ThrowsAsync<ValidacaoException>(() => Salvar(ana, Inativo));
        Assert.Contains(inativa.Erros, e => e.Contains("não está ativa"));
        var inexistente = await Assert.ThrowsAsync<ValidacaoException>(() => Salvar(ana, Guid.NewGuid()));
        Assert.Contains(inexistente.Erros, e => e.Contains("não existe mais"));
    }

    [Fact]
    public async Task Pessoa_que_ficou_inativa_depois_nao_impede_salvar_o_usuario_e_pode_ser_tirada()
    {
        var ana = Gravado("ana", Inativo);
        var mantida = await Salvar(ana, Inativo); // não mudou: não é conferida de novo
        Assert.Equal(Inativo, mantida.PessoaId);

        var tirada = await Salvar(ana, null);
        Assert.Null(tirada.PessoaId);
        Assert.Null(tirada.Pessoa);
    }

    [Fact]
    public async Task Guid_vazio_vale_como_sem_pessoa()
    {
        var dto = await Salvar(Gravado("ana"), Guid.Empty);
        Assert.Null(dto.PessoaId);
    }

    [Fact]
    public async Task Busca_exige_duas_letras_e_a_permissao_de_gerenciar_usuarios()
    {
        Assert.Empty(await _servico.BuscarPessoasAsync("j"));
        Assert.Equal(new[] { Joao }, (await _servico.BuscarPessoasAsync(" joão ")).Select(p => p.Id));
        Assert.Empty(await _servico.BuscarPessoasAsync("pedro")); // inativa não aparece

        _autorizacao.Negadas.Add(Permissoes.Seguranca.GerenciarUsuarios);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => _servico.BuscarPessoasAsync("maria"));
    }

    private sealed class PerfisFixos : IPerfilRepositorio
    {
        public Task<List<PerfilResumo>> ListarAsync(CancellationToken ct) =>
            Task.FromResult(new List<PerfilResumo> { new() { Id = PerfilVendas, Nome = "Vendas", Ativo = true } });
        public Task<Perfil?> ObterAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlySet<Guid>> IdsAdministradoresAtivosAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
        public Task SalvarAsync(Perfil perfil, bool novo, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class AutorizacaoFixa : IAutorizacao
    {
        public HashSet<string> Negadas { get; } = new();
        public bool Possui(string permissao) => !Negadas.Contains(permissao);
        public void Exigir(string permissao)
        {
            if (!Possui(permissao)) throw new AcessoNegadoException(permissao);
        }
    }

    private sealed class UsuarioFixo : IUsuarioAtual
    {
        public Guid? Id => Guid.Empty;
        public string Nome => "Admin";
    }
}
