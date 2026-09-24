using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lone.Cliente.Sessao;
using Lone.Contracts.Comum;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Validacao;

namespace Lone.Cliente.Api;

/// <summary>
/// Única porta do aplicativo para a API. Coloca o token em cada chamada, renova a sessão sozinho quando o
/// token de acesso vence (uma renovação por vez, mesmo com várias chamadas em paralelo) e transforma as
/// respostas de erro de volta nas mesmas exceções do servidor (ValidacaoException, AcessoNegadoException...).
/// </summary>
public sealed class ClienteApi
{
    private readonly HttpClient _http;
    private readonly SessaoCliente _sessao;
    private readonly ConfiguracaoServidor _servidor;
    private readonly SemaphoreSlim _renovando = new(1, 1);

    public ClienteApi(HttpClient http, SessaoCliente sessao, ConfiguracaoServidor servidor)
    {
        _http = http;
        _sessao = sessao;
        _servidor = servidor;
    }

    public async Task<T> GetAsync<T>(string rota, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(() => new HttpRequestMessage(HttpMethod.Get, _servidor.Montar(rota)), autenticado: true, ct);
        return await ReceberAsync<T>(resposta, ct);
    }

    /// <summary>Como GetAsync, mas 404 vira nulo (ex.: cadastro que não existe mais).</summary>
    public async Task<T?> GetOuNuloAsync<T>(string rota, CancellationToken ct = default) where T : class
    {
        using var resposta = await EnviarAsync(() => new HttpRequestMessage(HttpMethod.Get, _servidor.Montar(rota)), autenticado: true, ct);
        if (resposta.StatusCode == HttpStatusCode.NotFound) return null;
        return await ReceberAsync<T>(resposta, ct);
    }

    public async Task<T> PostAsync<T>(string rota, object corpo, bool autenticado = true, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(() => ComCorpo(HttpMethod.Post, rota, corpo), autenticado, ct);
        return await ReceberAsync<T>(resposta, ct);
    }

    /// <summary>
    /// POST cujo corpo é montado a cada tentativa. Necessário quando o corpo leva o token de renovação:
    /// se a sessão for renovada no meio (401 → renovar → repetir), a repetição já vai com o token novo.
    /// </summary>
    public async Task<T> PostComCorpoAtualAsync<T>(string rota, Func<object> criarCorpo, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(() => ComCorpo(HttpMethod.Post, rota, criarCorpo()), autenticado: true, ct);
        return await ReceberAsync<T>(resposta, ct);
    }

    public async Task PostAsync(string rota, object corpo, bool autenticado = true, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(() => ComCorpo(HttpMethod.Post, rota, corpo), autenticado, ct);
        await ConfirmarAsync(resposta, ct);
    }

    public async Task<T> PutAsync<T>(string rota, object corpo, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(() => ComCorpo(HttpMethod.Put, rota, corpo), autenticado: true, ct);
        return await ReceberAsync<T>(resposta, ct);
    }

    /// <summary>PUT sem conteúdo na resposta (ex.: nova ordem dos campos).</summary>
    public async Task PutSemRespostaAsync(string rota, object corpo, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(() => ComCorpo(HttpMethod.Put, rota, corpo), autenticado: true, ct);
        await ConfirmarAsync(resposta, ct);
    }

    /// <summary>POST sem corpo (ações como "desbloquear").</summary>
    public async Task PostAsync(string rota, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(() => new HttpRequestMessage(HttpMethod.Post, _servidor.Montar(rota)), autenticado: true, ct);
        await ConfirmarAsync(resposta, ct);
    }

    /// <summary>Lê a resposta de uma chamada autenticada, avisando a sessão se a API exigir troca de senha.</summary>
    private async Task<T> ReceberAsync<T>(HttpResponseMessage resposta, CancellationToken ct)
    {
        await ConfirmarAsync(resposta, ct);
        return await LerAsync<T>(resposta, ct);
    }

    private async Task ConfirmarAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        try
        {
            await GarantirSucessoAsync(resposta, ct);
        }
        catch (TrocaDeSenhaObrigatoriaException)
        {
            _sessao.ExigirTrocaDeSenha();
            throw;
        }
    }

    /// <summary>Chamada sem token e sem renovação automática (login, primeiro acesso, renovação).</summary>
    internal async Task<HttpResponseMessage> EnviarSemSessaoAsync(HttpMethod metodo, string rota, object? corpo, CancellationToken ct)
    {
        var requisicao = corpo is null ? new HttpRequestMessage(metodo, _servidor.Montar(rota)) : ComCorpo(metodo, rota, corpo);
        return await TransmitirAsync(requisicao, ct);
    }

    internal static async Task<T> LerAsync<T>(HttpResponseMessage resposta, CancellationToken ct)
    {
        await GarantirSucessoAsync(resposta, ct);
        return await resposta.Content.ReadFromJsonAsync<T>(OpcoesJson.Padrao, ct)
               ?? throw new ErroDaApiException((int)resposta.StatusCode, "O servidor respondeu sem conteúdo.", null);
    }

    // ---------------------------------------------------------------- Envio e renovação

    private async Task<HttpResponseMessage> EnviarAsync(Func<HttpRequestMessage> criar, bool autenticado, CancellationToken ct)
    {
        var tokenUsado = autenticado ? _sessao.TokenAcesso : null;
        var resposta = await TransmitirAsync(ComToken(criar(), tokenUsado), ct);

        if (resposta.StatusCode != HttpStatusCode.Unauthorized || !autenticado)
            return resposta;

        // Token de acesso vencido: renova a sessão e repete a chamada uma vez.
        resposta.Dispose();
        await RenovarAsync(tokenUsado, ct);
        var repetida = await TransmitirAsync(ComToken(criar(), _sessao.TokenAcesso), ct);

        // Recusado mesmo com o token recém-renovado: a sessão foi revogada no servidor.
        if (repetida.StatusCode == HttpStatusCode.Unauthorized)
            _sessao.Expirar("Sua sessão foi encerrada. Entre novamente.");
        return repetida;
    }

    /// <summary>
    /// Troca o token de renovação por uma sessão nova. Se outra chamada já renovou enquanto esta esperava,
    /// não renova de novo (renovar duas vezes com o mesmo token encerraria todas as sessões por segurança).
    /// </summary>
    internal async Task RenovarAsync(string? tokenQueFalhou, CancellationToken ct)
    {
        await _renovando.WaitAsync(ct);
        try
        {
            if (_sessao.TokenAcesso is { } atual && atual != tokenQueFalhou)
                return;

            var tokenRenovacao = await _sessao.TokenRenovacaoAsync();
            if (string.IsNullOrEmpty(tokenRenovacao))
                throw new SessaoExpiradaException();

            using var resposta = await EnviarSemSessaoAsync(HttpMethod.Post, Rotas.Autenticacao.Renovar,
                new RenovarSessaoRequisicao(tokenRenovacao), ct);

            if (resposta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                var mensagem = (await LerProblemaAsync(resposta, ct))?.Mensagem ?? "Sua sessão expirou. Entre novamente.";
                _sessao.Expirar(mensagem);
                throw new SessaoExpiradaException(mensagem);
            }

            // Outras falhas (servidor instável, limite de tentativas, endereço errado) não provam que a
            // sessão acabou: o token continua guardado para a próxima tentativa.
            await GarantirSucessoAsync(resposta, ct);

            var sessao = await resposta.Content.ReadFromJsonAsync<SessaoDto>(OpcoesJson.Padrao, ct)
                         ?? throw new SessaoExpiradaException();
            await _sessao.DefinirAsync(sessao);
        }
        finally
        {
            _renovando.Release();
        }
    }

    private async Task<HttpResponseMessage> TransmitirAsync(HttpRequestMessage requisicao, CancellationToken ct)
    {
        using (requisicao)
        {
            try
            {
                return await _http.SendAsync(requisicao, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                throw new ServidorIndisponivelException(_servidor.Endereco, ex);
            }
        }
    }

    private HttpRequestMessage ComCorpo(HttpMethod metodo, string rota, object corpo) =>
        new(metodo, _servidor.Montar(rota)) { Content = JsonContent.Create(corpo, corpo.GetType(), options: OpcoesJson.Padrao) };

    private static HttpRequestMessage ComToken(HttpRequestMessage requisicao, string? token)
    {
        if (!string.IsNullOrEmpty(token))
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return requisicao;
    }

    // ---------------------------------------------------------------- Erros

    private sealed record Problema(int Status, string Mensagem, string? Codigo, IReadOnlyList<string> Erros, string? Permissao, string? SituacaoLogin);

    internal static async Task GarantirSucessoAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        if (resposta.IsSuccessStatusCode) return;

        var problema = await LerProblemaAsync(resposta, ct)
                       ?? new Problema((int)resposta.StatusCode, $"O servidor respondeu com erro (HTTP {(int)resposta.StatusCode}).",
                                       null, [], null, null);

        Exception erro = problema.Codigo switch
        {
            ErrosApi.Validacao => new ValidacaoException(problema.Erros.Count > 0 ? problema.Erros : [problema.Mensagem]),
            ErrosApi.AcessoNegado => new AcessoNegadoException(problema.Permissao ?? string.Empty, problema.Mensagem),
            ErrosApi.Conflito => new ConflitoDeEdicaoException(problema.Mensagem),
            ErrosApi.NaoAutenticado => new SessaoExpiradaException(problema.Mensagem),
            ErrosApi.TrocaDeSenhaObrigatoria => new TrocaDeSenhaObrigatoriaException(problema.Mensagem),
            ErrosApi.LoginRecusado => new LoginRecusadoException(problema.Mensagem,
                Enum.TryParse<SituacaoLogin>(problema.SituacaoLogin, ignoreCase: true, out var situacao) ? situacao : SituacaoLogin.CredenciaisInvalidas),
            _ => new ErroDaApiException(problema.Status, problema.Mensagem, problema.Codigo)
        };
        throw erro;
    }

    /// <summary>Lê o corpo ProblemDetails (RFC 9457). Nulo se a resposta não estiver nesse formato.</summary>
    private static async Task<Problema?> LerProblemaAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        try
        {
            var texto = await resposta.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(texto)) return null;

            using var json = JsonDocument.Parse(texto);
            var raiz = json.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return null;

            string? Texto(string nome) => raiz.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            var erros = raiz.TryGetProperty(ErrosApi.CampoErros, out var lista) && lista.ValueKind == JsonValueKind.Array
                ? lista.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
                : [];

            return new Problema(
                (int)resposta.StatusCode,
                Texto("detail") ?? Texto("title") ?? $"O servidor respondeu com erro (HTTP {(int)resposta.StatusCode}).",
                Texto(ErrosApi.CampoCodigo),
                erros,
                Texto(ErrosApi.CampoPermissao),
                Texto(ErrosApi.CampoSituacaoLogin));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
