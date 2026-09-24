using Lone.Cliente.Plataforma;

namespace Lone.Cliente.Api;

/// <summary>Endereço da API usado por este aparelho. O usuário pode trocar na tela de login; fica guardado.</summary>
public sealed class ConfiguracaoServidor
{
    private const string Chave = "lone.servidor";

    private readonly IPreferencias _preferencias;
    private readonly IDispositivo _dispositivo;

    public ConfiguracaoServidor(IPreferencias preferencias, IDispositivo dispositivo)
    {
        _preferencias = preferencias;
        _dispositivo = dispositivo;
    }

    public string Endereco => _preferencias.Ler(Chave) is { Length: > 0 } salvo ? salvo : _dispositivo.ServidorPadrao;

    /// <summary>Valida e guarda o endereço (ex.: https://servidor.empresa.com.br). Devolve a mensagem de erro, se houver.</summary>
    public string? Definir(string? endereco)
    {
        var texto = (endereco ?? string.Empty).Trim().TrimEnd('/');
        if (texto.Length == 0)
        {
            _preferencias.Remover(Chave);
            return null;
        }

        if (!Uri.TryCreate(texto, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return "Endereço do servidor inválido. Use o formato https://servidor:porta.";

        _preferencias.Gravar(Chave, texto);
        return null;
    }

    public Uri Montar(string rota) => new(new Uri(Endereco.TrimEnd('/') + "/"), rota.TrimStart('/'));
}
