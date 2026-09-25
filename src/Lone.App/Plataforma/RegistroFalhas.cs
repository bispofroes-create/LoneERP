using System.Diagnostics;
using System.Text;

namespace Lone.App.Plataforma;

/// <summary>
/// Diagnóstico: grava em arquivo toda exceção não tratada (com a pilha completa e as internas) antes de o aplicativo
/// fechar. Não trata nem esconde nada: o erro continua derrubando o app como antes; só deixa o rastro para achar a
/// causa real. Arquivo: %LOCALAPPDATA%\Lone\falhas.log no Windows (pasta de dados do app nos outros sistemas).
/// </summary>
public static class RegistroFalhas
{
    private static readonly object Trava = new();
    private static bool _ligado;

    public static string Caminho { get; } = Path.Combine(
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lone")
            : FileSystem.AppDataDirectory,
        "falhas.log");

    /// <summary>Liga os ganchos do .NET (chamado uma vez, ao montar o app).</summary>
    public static void Ligar()
    {
        if (_ligado) return;
        _ligado = true;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Registrar("AppDomain.UnhandledException", e.ExceptionObject as Exception, $"Encerrando: {e.IsTerminating}");
        TaskScheduler.UnobservedTaskException += (_, e) => Registrar("TaskScheduler.UnobservedTaskException", e.Exception);
    }

    public static void Registrar(string origem, Exception? erro, string? detalhe = null)
    {
        var texto = new StringBuilder()
            .AppendLine(new string('=', 100))
            .AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {origem}")
            .AppendLine(detalhe ?? string.Empty)
            .AppendLine(erro?.ToString() ?? "(sem objeto de exceção)")
            .ToString();
        Debug.WriteLine(texto);
        try
        {
            lock (Trava)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Caminho)!);
                File.AppendAllText(Caminho, texto);
            }
        }
        catch (IOException) { /* sem disco: a saída de depuração acima ainda mostra o erro */ }
        catch (UnauthorizedAccessException) { }
    }
}
