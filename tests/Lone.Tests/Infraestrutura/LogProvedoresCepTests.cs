using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Bloco A, privacidade: o log padrão do cliente HTTP dos provedores de CEP (que escreve a URL de cada consulta, com UF,
/// cidade e logradouro, ou o CEP) fica em Warning na configuração da API. Só essas categorias: o resto continua em
/// Information, e as falhas (Warning/Error) dos provedores continuam no log.
/// </summary>
public class LogProvedoresCepTests
{
    private sealed class Registrador : ILoggerProvider, ILogger
    {
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private static string PastaDaApi()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            var api = Path.Combine(pasta.FullName, "src", "Lone.Api");
            if (File.Exists(Path.Combine(api, "appsettings.json"))) return api;
        }
        throw new InvalidOperationException("Pasta src/Lone.Api não encontrada a partir dos testes.");
    }

    private static ILoggerFactory Fabrica()
    {
        var api = PastaDaApi();
        var configuracao = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(api, "appsettings.json"))
            .AddJsonFile(Path.Combine(api, "appsettings.Development.json"))
            .Build();
        return LoggerFactory.Create(b => b.AddConfiguration(configuracao.GetSection("Logging")).AddProvider(new Registrador()));
    }

    [Theory]
    [InlineData("System.Net.Http.HttpClient.ViaCepProvedor.LogicalHandler")]
    [InlineData("System.Net.Http.HttpClient.ViaCepProvedor.ClientHandler")]
    [InlineData("System.Net.Http.HttpClient.BrasilApiCepProvedor.LogicalHandler")]
    [InlineData("System.Net.Http.HttpClient.BrasilApiCepProvedor.ClientHandler")]
    public void Url_das_consultas_de_cep_sai_do_log_e_as_falhas_continuam(string categoria)
    {
        using var fabrica = Fabrica();
        var log = fabrica.CreateLogger(categoria);

        Assert.False(log.IsEnabled(LogLevel.Information)); // "Start processing HTTP request GET https://viacep.com.br/ws/..."
        Assert.True(log.IsEnabled(LogLevel.Warning));
        Assert.True(log.IsEnabled(LogLevel.Error));
    }

    [Theory]
    [InlineData("Lone.Infrastructure.Integracoes.Cep.ViaCepProvedor")]       // os avisos de falha dos próprios provedores
    [InlineData("System.Net.Http.HttpClient.OutroCliente.LogicalHandler")]    // outros clientes HTTP: como antes
    [InlineData("Lone.Api")]
    public void Demais_categorias_continuam_em_information(string categoria)
    {
        using var fabrica = Fabrica();

        Assert.True(fabrica.CreateLogger(categoria).IsEnabled(LogLevel.Information));
    }
}
