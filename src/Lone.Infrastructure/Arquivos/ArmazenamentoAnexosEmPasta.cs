using Lone.Application.Documentos;

namespace Lone.Infrastructure.Arquivos;

/// <summary>
/// Anexos numa pasta do servidor da API (D7): {pasta}/{ano}/{mês}/{id}.bin. O caminho é montado só com o Id
/// gerado pelo servidor (nunca com o nome enviado pelo usuário) e conferido para não sair da pasta.
/// Grava num arquivo temporário e renomeia no fim: um arquivo pela metade nunca fica com o nome definitivo.
/// </summary>
public sealed class ArmazenamentoAnexosEmPasta : IArmazenamentoAnexos
{
    private readonly string _raiz;

    public ArmazenamentoAnexosEmPasta(OpcoesAnexos opcoes)
    {
        _raiz = Path.GetFullPath(string.IsNullOrWhiteSpace(opcoes.Pasta)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Lone", "Anexos")
            : opcoes.Pasta);
    }

    public async Task<string> GravarAsync(Guid id, byte[] conteudo, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var relativo = $"{agora:yyyy}/{agora:MM}/{id:N}.bin";
        var destino = Completo(relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

        var temporario = destino + ".tmp";
        await File.WriteAllBytesAsync(temporario, conteudo, ct);
        File.Move(temporario, destino, overwrite: false);
        return relativo;
    }

    public async Task<byte[]?> LerAsync(string caminho, CancellationToken ct)
    {
        var completo = Completo(caminho);
        return File.Exists(completo) ? await File.ReadAllBytesAsync(completo, ct) : null;
    }

    public Task DescartarNaoRegistradoAsync(string caminho, CancellationToken ct)
    {
        try
        {
            var completo = Completo(caminho);
            if (File.Exists(completo)) File.Delete(completo);
        }
        catch (IOException) { } // sobra um arquivo sem registro: não afeta nada
        catch (UnauthorizedAccessException) { }
        return Task.CompletedTask;
    }

    private string Completo(string relativo)
    {
        var completo = Path.GetFullPath(Path.Combine(_raiz, relativo));
        if (!completo.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho de anexo fora da pasta de anexos.");
        return completo;
    }
}
