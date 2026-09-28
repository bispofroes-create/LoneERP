using Lone.Application.Menu;
using Lone.Application.Seguranca;
using Lone.Contracts.Menu;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>Favoritos e recentes do menu: por usuário, nunca apagados, com limite de favoritos e rota conferida.</summary>
public class MenuUsuarioAppServiceTests
{
    private readonly PreferenciasEmMemoria _repositorio = new();
    private readonly UsuarioFixo _usuario = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));
    private readonly MenuUsuarioAppService _servico;

    public MenuUsuarioAppServiceTests() => _servico = new MenuUsuarioAppService(_repositorio, _usuario, _relogio);

    private Task Favoritar(string rota, bool favorito = true) =>
        _servico.DefinirFavoritoAsync(new FavoritoMenuRequisicao { Rota = rota, Favorito = favorito });

    private Task Abrir(string rota) => _servico.RegistrarAcessoAsync(new AcessoMenuRequisicao { Rota = rota });

    [Fact]
    public async Task Favoritos_ficam_na_ordem_em_que_foram_marcados_e_desmarcar_so_desmarca()
    {
        await Favoritar("metas");
        _relogio.Advance(TimeSpan.FromMinutes(1));
        await Favoritar("pessoas");
        _relogio.Advance(TimeSpan.FromMinutes(1));
        await Favoritar("metas"); // de novo: continua na posição em que estava

        Assert.Equal(new[] { "metas", "pessoas" }, (await _servico.ObterAsync()).Favoritos);

        await Favoritar("metas", favorito: false);

        Assert.Equal(new[] { "pessoas" }, (await _servico.ObterAsync()).Favoritos);
        var desmarcada = Assert.Single(_repositorio.Linhas, l => l.Rota == "metas"); // a linha continua (nunca apaga)
        Assert.False(desmarcada.Favorito);
        Assert.Null(desmarcada.FavoritadaEm);
    }

    [Fact]
    public async Task Recentes_vem_do_mais_novo_para_o_mais_antigo_e_reabrir_sobe_para_o_topo()
    {
        await Abrir("pessoas");
        _relogio.Advance(TimeSpan.FromMinutes(1));
        await Abrir("papeis");
        _relogio.Advance(TimeSpan.FromMinutes(1));
        await Abrir("pessoas");

        Assert.Equal(new[] { "pessoas", "papeis" }, (await _servico.ObterAsync()).Recentes);
        Assert.Equal(2, _repositorio.Linhas.Count); // uma linha por usuário + rota
    }

    [Fact]
    public async Task Recentes_devolvidos_tem_limite_e_favorito_sem_acesso_nao_vira_recente()
    {
        for (var i = 0; i < LimitesMenu.RecentesDevolvidos + 3; i++)
        {
            await Abrir($"tela-{i}");
            _relogio.Advance(TimeSpan.FromSeconds(1));
        }
        await Favoritar("so-favorita");

        var preferencias = await _servico.ObterAsync();

        Assert.Equal(LimitesMenu.RecentesDevolvidos, preferencias.Recentes.Count);
        Assert.Equal($"tela-{LimitesMenu.RecentesDevolvidos + 2}", preferencias.Recentes[0]);
        Assert.DoesNotContain("so-favorita", preferencias.Recentes);
    }

    [Fact]
    public async Task Nao_passa_do_limite_de_favoritos()
    {
        for (var i = 0; i < LimitesMenu.MaximoFavoritos; i++) await Favoritar($"tela-{i}");

        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => Favoritar("mais-uma"));

        Assert.Contains($"{LimitesMenu.MaximoFavoritos} favoritos", Assert.Single(erro.Erros));
        await Favoritar("tela-0"); // marcar de novo uma que já é favorita não conta como nova
        await Favoritar("tela-0", favorito: false);
        await Favoritar("mais-uma"); // abriu uma vaga
    }

    [Fact]
    public async Task Cada_usuario_ve_so_os_seus()
    {
        await Favoritar("metas");
        await Abrir("metas");
        _usuario.Id = Guid.NewGuid();

        var outro = await _servico.ObterAsync();

        Assert.Empty(outro.Favoritos);
        Assert.Empty(outro.Recentes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Pessoas")]
    [InlineData("pessoas/../usuarios")]
    [InlineData("-pessoas")]
    [InlineData("pessoas--x")]
    [InlineData("tela com espaço")]
    public async Task Rota_fora_do_formato_do_shell_e_recusada(string rota)
    {
        await Assert.ThrowsAsync<ValidacaoException>(() => Favoritar(rota));
        await Assert.ThrowsAsync<ValidacaoException>(() => Abrir(rota));
        Assert.Empty(_repositorio.Linhas);
    }

    [Fact]
    public async Task Sem_usuario_logado_nada_e_gravado()
    {
        _usuario.Id = null;

        await Assert.ThrowsAsync<ValidacaoException>(() => Abrir("pessoas"));
        await Assert.ThrowsAsync<ValidacaoException>(() => _servico.ObterAsync());
        Assert.Empty(_repositorio.Linhas);
    }

    [Fact]
    public async Task Preferencia_da_tela_e_do_usuario_e_so_aceita_objeto_json_de_tamanho_limitado()
    {
        Assert.Equal(string.Empty, (await _servico.ObterTelaAsync("pessoas-lista")).Conteudo);

        await _servico.DefinirTelaAsync("pessoas-lista", new PreferenciaTelaDto { Conteudo = "{\"Colunas\":[\"enderecos.bairro\"]}" });
        Assert.Contains("enderecos.bairro", (await _servico.ObterTelaAsync("pessoas-lista")).Conteudo);

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            _servico.DefinirTelaAsync("pessoas-lista", new PreferenciaTelaDto { Conteudo = "[1,2]" }));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            _servico.DefinirTelaAsync("pessoas-lista", new PreferenciaTelaDto { Conteudo = "não é json" }));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            _servico.DefinirTelaAsync("pessoas-lista", new PreferenciaTelaDto { Conteudo = "{\"x\":\"" + new string('a', LimitesMenu.TamanhoMaximoPreferenciaTela) + "\"}" }));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            _servico.DefinirTelaAsync("Pessoas Lista", new PreferenciaTelaDto { Conteudo = "{}" }));

        // Vazio = volta ao padrão da tela (grava um objeto vazio).
        await _servico.DefinirTelaAsync("pessoas-lista", new PreferenciaTelaDto { Conteudo = "  " });
        Assert.Equal("{}", (await _servico.ObterTelaAsync("pessoas-lista")).Conteudo);

        var outro = Guid.NewGuid();
        var meu = _usuario.Id;
        _usuario.Id = outro;
        Assert.Equal(string.Empty, (await _servico.ObterTelaAsync("pessoas-lista")).Conteudo); // cada um vê só a sua
        _usuario.Id = meu;
    }

    private sealed class UsuarioFixo : IUsuarioAtual
    {
        public Guid? Id { get; set; } = Guid.NewGuid();
        public string Nome => "Maria";
    }

    private sealed class PreferenciasEmMemoria : IPreferenciaMenuRepositorio
    {
        public List<PreferenciaMenu> Linhas { get; } = new();

        public Task<List<PreferenciaMenu>> ListarAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(Linhas.Where(l => l.UsuarioId == usuarioId).ToList());

        public Task AtualizarAsync(Guid usuarioId, string rota, Action<PreferenciaMenu> alterar, CancellationToken ct)
        {
            var linha = Linhas.FirstOrDefault(l => l.UsuarioId == usuarioId && l.Rota == rota);
            if (linha is null)
            {
                linha = new PreferenciaMenu { Id = Guid.NewGuid(), UsuarioId = usuarioId, Rota = rota };
                Linhas.Add(linha);
            }
            alterar(linha);
            return Task.CompletedTask;
        }

        public Dictionary<(Guid, string), string> Telas { get; } = new();

        public Task<string?> ObterTelaAsync(Guid usuarioId, string tela, CancellationToken ct) =>
            Task.FromResult(Telas.TryGetValue((usuarioId, tela), out var c) ? c : null);

        public Task DefinirTelaAsync(Guid usuarioId, string tela, string conteudo, DateTime agoraUtc, CancellationToken ct)
        {
            Telas[(usuarioId, tela)] = conteudo;
            return Task.CompletedTask;
        }
    }
}
