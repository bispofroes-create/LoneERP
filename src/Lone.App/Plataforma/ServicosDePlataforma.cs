using Lone.Cliente.Plataforma;

namespace Lone.App.Plataforma;

/// <summary>Cofre do aparelho (Windows: DPAPI; Android: Keystore) para o token de renovação.</summary>
public sealed class ArmazenamentoSeguroMaui : IArmazenamentoSeguro
{
    public async Task<string?> LerAsync(string chave)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(chave);
        }
        catch (Exception)
        {
            // Cofre ilegível (ex.: app reinstalado com outra chave): trata como vazio e pede login.
            SecureStorage.Default.Remove(chave);
            return null;
        }
    }

    public Task GravarAsync(string chave, string valor) => SecureStorage.Default.SetAsync(chave, valor);

    public void Remover(string chave) => SecureStorage.Default.Remove(chave);
}

public sealed class PreferenciasMaui : IPreferencias
{
    public string? Ler(string chave) => Preferences.Default.Get<string?>(chave, null);
    public void Gravar(string chave, string valor) => Preferences.Default.Set(chave, valor);
    public void Remover(string chave) => Preferences.Default.Remove(chave);
}

public sealed class DispositivoMaui : IDispositivo
{
    public string Descricao => $"{DeviceInfo.Current.Platform} - {DeviceInfo.Current.Name}";

    /// <summary>
    /// Em desenvolvimento: o emulador Android enxerga o computador como 10.0.2.2 (e usa http, porque o
    /// certificado de desenvolvimento não é confiável no emulador). No Windows, a API local em https.
    /// </summary>
    public string ServidorPadrao => DeviceInfo.Current.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:5080"
        : "https://localhost:7080";
}
