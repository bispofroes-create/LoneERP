using Lone.Application.Seguranca;
using Lone.Contracts.Empresas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Lone.Tests.Apoio;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

public class AutenticacaoServiceTests
{
    private const string Senha = "Lone2026erp";

    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero));
    private readonly UsuariosEmMemoria _usuarios = new();
    private readonly EmpresasFixas _empresas = new();
    private readonly TokensEmMemoria _tokens;
    private readonly AutenticacaoService _servico;
    private readonly Usuario _maria;

    public AutenticacaoServiceTests()
    {
        _tokens = new TokensEmMemoria(_relogio);
        var hasher = new HasherRapido();
        _servico = new AutenticacaoService(_usuarios, _tokens, new AutenticadorLocal(hasher), hasher,
                                           new EmissorDeTeste(_relogio), _empresas, _relogio);

        var administrador = new Perfil { Id = IdSequencial.Novo(), Nome = "Administrador", Administrador = true };
        _maria = new Usuario
        {
            Id = IdSequencial.Novo(),
            Login = "maria",
            Nome = "Maria",
            SenhaHash = hasher.Gerar(Senha)
        };
        _maria.Perfis.Add(new UsuarioPerfil { Id = IdSequencial.Novo(), UsuarioId = _maria.Id, PerfilId = administrador.Id, Perfil = administrador });
        _usuarios.Usuarios.Add(_maria);
    }

    private EmpresaAtiva AdicionarEmpresa(string nome)
    {
        var empresa = new EmpresaAtiva(IdSequencial.Novo(), IdSequencial.Novo(), nome, null, true);
        _empresas.Estabelecimentos.Add(empresa);
        return empresa;
    }

    [Fact]
    public async Task Login_correto_abre_sessao_e_ativa_a_unica_empresa()
    {
        var empresa = AdicionarEmpresa("Matriz");

        var resultado = await _servico.EntrarAsync(new EntrarRequisicao("  MARIA ", Senha));

        Assert.True(resultado.Sucesso);
        Assert.Equal(empresa, resultado.Sessao!.EmpresaAtiva);
        Assert.True(resultado.Sessao.Administrador);
        Assert.Single(_tokens.Tokens);
        Assert.NotEqual(resultado.Sessao.TokenRenovacao, _tokens.Tokens[0].Hash); // só o hash é guardado
    }

    [Fact]
    public async Task Com_varias_empresas_nenhuma_fica_ativa_ate_o_usuario_escolher()
    {
        AdicionarEmpresa("Loja 1");
        var loja2 = AdicionarEmpresa("Loja 2");

        var sessao = (await _servico.EntrarAsync(new EntrarRequisicao("maria", Senha))).Sessao!;
        Assert.Null(sessao.EmpresaAtiva);
        Assert.Equal(2, sessao.EmpresasDisponiveis.Count);

        var escolhida = await _servico.SelecionarEmpresaAsync(_maria.Id,
            new SelecionarEmpresaRequisicao(loja2.EstabelecimentoId, sessao.TokenRenovacao));

        Assert.Equal(loja2, escolhida.EmpresaAtiva);
    }

    [Fact]
    public async Task Cinco_senhas_erradas_bloqueiam_o_acesso()
    {
        for (var i = 0; i < Usuario.MaximoTentativas; i++)
            await _servico.EntrarAsync(new EntrarRequisicao("maria", "errada123"));

        var comSenhaCerta = await _servico.EntrarAsync(new EntrarRequisicao("maria", Senha));
        Assert.Equal(SituacaoLogin.Bloqueado, comSenhaCerta.Situacao);

        _relogio.Advance(Usuario.TempoBloqueio + TimeSpan.FromMinutes(1));
        Assert.True((await _servico.EntrarAsync(new EntrarRequisicao("maria", Senha))).Sucesso);
    }

    [Fact]
    public async Task Renovar_troca_o_token_e_reusar_o_antigo_encerra_todas_as_sessoes()
    {
        var primeira = (await _servico.EntrarAsync(new EntrarRequisicao("maria", Senha))).Sessao!;

        var segunda = await _servico.RenovarAsync(new RenovarSessaoRequisicao(primeira.TokenRenovacao));
        Assert.NotEqual(primeira.TokenRenovacao, segunda.TokenRenovacao);

        // Alguém reapresenta o token já trocado: sinal de cópia. Tudo é revogado, inclusive a sessão nova.
        await Assert.ThrowsAsync<SessaoInvalidaException>(() =>
            _servico.RenovarAsync(new RenovarSessaoRequisicao(primeira.TokenRenovacao)));
        await Assert.ThrowsAsync<SessaoInvalidaException>(() =>
            _servico.RenovarAsync(new RenovarSessaoRequisicao(segunda.TokenRenovacao)));
    }

    [Fact]
    public async Task Token_de_renovacao_vencido_nao_renova()
    {
        var sessao = (await _servico.EntrarAsync(new EntrarRequisicao("maria", Senha))).Sessao!;

        _relogio.Advance(TimeSpan.FromDays(15));

        await Assert.ThrowsAsync<SessaoInvalidaException>(() =>
            _servico.RenovarAsync(new RenovarSessaoRequisicao(sessao.TokenRenovacao)));
    }

    [Fact]
    public async Task Usuario_inativado_nao_renova_a_sessao()
    {
        var sessao = (await _servico.EntrarAsync(new EntrarRequisicao("maria", Senha))).Sessao!;
        _maria.Ativo = false;

        await Assert.ThrowsAsync<SessaoInvalidaException>(() =>
            _servico.RenovarAsync(new RenovarSessaoRequisicao(sessao.TokenRenovacao)));
    }

    [Fact]
    public async Task Empresa_fora_das_disponiveis_e_recusada()
    {
        AdicionarEmpresa("Loja 1");
        AdicionarEmpresa("Loja 2");
        var sessao = (await _servico.EntrarAsync(new EntrarRequisicao("maria", Senha))).Sessao!;

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            _servico.SelecionarEmpresaAsync(_maria.Id, new SelecionarEmpresaRequisicao(Guid.NewGuid(), sessao.TokenRenovacao)));
    }

    [Fact]
    public async Task Sair_revoga_o_token_deste_aparelho()
    {
        var sessao = (await _servico.EntrarAsync(new EntrarRequisicao("maria", Senha))).Sessao!;

        await _servico.SairAsync(new SairRequisicao(sessao.TokenRenovacao));

        await Assert.ThrowsAsync<SessaoInvalidaException>(() =>
            _servico.RenovarAsync(new RenovarSessaoRequisicao(sessao.TokenRenovacao)));
    }
}
