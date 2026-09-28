using System.Text.RegularExpressions;
using Lone.Application.Seguranca;
using Lone.Contracts.Menu;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Menu;

public interface IPreferenciaMenuRepositorio
{
    Task<List<PreferenciaMenu>> ListarAsync(Guid usuarioId, CancellationToken ct);

    /// <summary>Acha a linha do usuário + rota (ou cria uma nova), aplica a alteração e grava.</summary>
    Task AtualizarAsync(Guid usuarioId, string rota, Action<PreferenciaMenu> alterar, CancellationToken ct);

    /// <summary>Preferência da tela do usuário (JSON); nulo = nunca gravou.</summary>
    Task<string?> ObterTelaAsync(Guid usuarioId, string tela, CancellationToken ct);

    /// <summary>Grava (ou cria) a preferência da tela do usuário.</summary>
    Task DefinirTelaAsync(Guid usuarioId, string tela, string conteudo, DateTime agoraUtc, CancellationToken ct);
}

public interface IMenuUsuarioAppService
{
    Task<PreferenciasMenuDto> ObterAsync(CancellationToken ct = default);
    Task DefinirFavoritoAsync(FavoritoMenuRequisicao requisicao, CancellationToken ct = default);
    Task RegistrarAcessoAsync(AcessoMenuRequisicao requisicao, CancellationToken ct = default);
    Task<PreferenciaTelaDto> ObterTelaAsync(string tela, CancellationToken ct = default);
    Task DefinirTelaAsync(string tela, PreferenciaTelaDto preferencia, CancellationToken ct = default);
}

/// <summary>
/// Favoritos e recentes do menu, por usuário (acompanham o usuário em qualquer aparelho). São dados do próprio usuário:
/// basta estar logado. A API não conhece as telas; o aplicativo mostra só as rotas que existem e que o perfil permite,
/// então uma rota antiga ou sem permissão fica guardada sem aparecer.
/// </summary>
public sealed partial class MenuUsuarioAppService : IMenuUsuarioAppService
{
    private readonly IPreferenciaMenuRepositorio _repositorio;
    private readonly IUsuarioAtual _usuario;
    private readonly TimeProvider _relogio;

    public MenuUsuarioAppService(IPreferenciaMenuRepositorio repositorio, IUsuarioAtual usuario, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _usuario = usuario;
        _relogio = relogio;
    }

    public async Task<PreferenciasMenuDto> ObterAsync(CancellationToken ct = default)
    {
        var linhas = await _repositorio.ListarAsync(UsuarioLogado(), ct);
        return new PreferenciasMenuDto
        {
            Favoritos = linhas.Where(l => l.Favorito)
                .OrderBy(l => l.FavoritadaEm).ThenBy(l => l.Rota, StringComparer.Ordinal)
                .Select(l => l.Rota).ToList(),
            Recentes = linhas.Where(l => l.UltimoAcessoEm is not null)
                .OrderByDescending(l => l.UltimoAcessoEm).ThenBy(l => l.Rota, StringComparer.Ordinal)
                .Take(LimitesMenu.RecentesDevolvidos)
                .Select(l => l.Rota).ToList()
        };
    }

    public async Task DefinirFavoritoAsync(FavoritoMenuRequisicao requisicao, CancellationToken ct = default)
    {
        var usuario = UsuarioLogado();
        var rota = ValidarRota(requisicao.Rota);
        if (requisicao.Favorito)
        {
            // Conferido antes de gravar; dois aparelhos ao mesmo tempo podem passar um a mais, o que não causa dano.
            var favoritos = (await _repositorio.ListarAsync(usuario, ct)).Where(l => l.Favorito).ToList();
            if (favoritos.Any(l => l.Rota == rota)) return; // já é favorita: mantém a posição
            if (favoritos.Count >= LimitesMenu.MaximoFavoritos)
                throw new ValidacaoException([$"Você já tem {LimitesMenu.MaximoFavoritos} favoritos. Remova um para marcar outro."]);
        }

        var agora = _relogio.GetUtcNow().UtcDateTime;
        await _repositorio.AtualizarAsync(usuario, rota, linha =>
        {
            if (linha.Favorito == requisicao.Favorito) return;
            linha.Favorito = requisicao.Favorito;
            linha.FavoritadaEm = requisicao.Favorito ? agora : null;
        }, ct);
    }

    public async Task RegistrarAcessoAsync(AcessoMenuRequisicao requisicao, CancellationToken ct = default)
    {
        var usuario = UsuarioLogado();
        var rota = ValidarRota(requisicao.Rota);
        var agora = _relogio.GetUtcNow().UtcDateTime;
        await _repositorio.AtualizarAsync(usuario, rota, linha => linha.UltimoAcessoEm = agora, ct);
    }

    public async Task<PreferenciaTelaDto> ObterTelaAsync(string tela, CancellationToken ct = default) =>
        new() { Conteudo = await _repositorio.ObterTelaAsync(UsuarioLogado(), ValidarRota(tela), ct) ?? string.Empty };

    /// <summary>
    /// Grava a preferência da tela (colunas da lista, ordenação...). A API só confere tamanho e que é um objeto JSON: quem
    /// entende o conteúdo é a tela, que ignora o que não conhece (ex.: coluna que perdeu a permissão).
    /// </summary>
    public async Task DefinirTelaAsync(string tela, PreferenciaTelaDto preferencia, CancellationToken ct = default)
    {
        var usuario = UsuarioLogado();
        var nome = ValidarRota(tela);
        var conteudo = (preferencia?.Conteudo ?? string.Empty).Trim();
        if (conteudo.Length == 0) conteudo = "{}"; // vazio = volta ao padrão da tela
        if (conteudo.Length > LimitesMenu.TamanhoMaximoPreferenciaTela)
            throw new ValidacaoException(["Preferência da tela grande demais."]);
        if (!ObjetoJson(conteudo))
            throw new ValidacaoException(["Preferência da tela inválida."]);
        await _repositorio.DefinirTelaAsync(usuario, nome, conteudo, _relogio.GetUtcNow().UtcDateTime, ct);
    }

    private static bool ObjetoJson(string texto)
    {
        try
        {
            using var documento = System.Text.Json.JsonDocument.Parse(texto);
            return documento.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private Guid UsuarioLogado() =>
        _usuario.Id ?? throw new ValidacaoException(["Entre com um usuário para usar favoritos e recentes."]);

    /// <summary>Rota no formato do Shell ("consulta-pessoas"): evita gravar texto livre ou lixo vindo de fora.</summary>
    public static string ValidarRota(string? rota)
    {
        var texto = (rota ?? string.Empty).Trim();
        if (texto.Length is 0 or > LimitesMenu.TamanhoMaximoRota || !FormatoRota().IsMatch(texto))
            throw new ValidacaoException(["Tela inválida para o menu."]);
        return texto;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex FormatoRota();
}
