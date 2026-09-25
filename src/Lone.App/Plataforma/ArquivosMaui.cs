using Lone.Cliente.Plataforma;

namespace Lone.App.Plataforma;

/// <summary>Escolha de arquivo pelo seletor do sistema (Windows: janela Abrir; Android: seletor de documentos).</summary>
public sealed class ArquivosMaui : IArquivos
{
    public async Task<ArquivoEscolhido?> EscolherAsync(string titulo)
    {
        var escolhido = await MainThread.InvokeOnMainThreadAsync(() => FilePicker.Default.PickAsync(new PickOptions { PickerTitle = titulo }));
        if (escolhido is null) return null;

        await using var origem = await escolhido.OpenReadAsync();
        using var memoria = new MemoryStream();
        await origem.CopyToAsync(memoria);
        return new ArquivoEscolhido(escolhido.FileName, memoria.ToArray());
    }
}
