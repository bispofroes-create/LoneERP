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

    /// <summary>
    /// Grava numa pasta própria dentro do cache do aplicativo (uma subpasta por abertura, para não misturar arquivos
    /// de mesmo nome) e entrega ao aplicativo padrão. O cache é limpo pelo sistema quando precisa de espaço.
    /// </summary>
    public async Task AbrirAsync(string nome, byte[] conteudo)
    {
        var pasta = Path.Combine(FileSystem.CacheDirectory, "anexos", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, Path.GetFileName(nome));
        await File.WriteAllBytesAsync(caminho, conteudo);
        await MainThread.InvokeOnMainThreadAsync(() => Launcher.Default.OpenAsync(new OpenFileRequest(nome, new ReadOnlyFile(caminho))));
    }
}
