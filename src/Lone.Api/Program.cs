using Lone.Api;
using Lone.Api.Endpoints;
using Lone.Api.Seguranca;
using Lone.Application;
using Lone.Application.Infraestrutura;
using Lone.Infrastructure;
using Lone.Infrastructure.Persistencia.Servicos;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddLoneApplication()
    .AddLoneInfrastructure(builder.Configuration)
    .AddLoneApi(builder.Configuration, builder.Environment);

var app = builder.Build();

// Incidente P18: migração nunca é aplicada só porque a API iniciou. Aplicar ao iniciar exige a configuração E o opt-in
// explícito da variável LONE_PERMITIR_MIGRACAO_AUTOMATICA=true; senão, migração pendente vira só um aviso no log.
await using (var escopo = app.Services.CreateAsyncScope())
{
    await MigracaoAoIniciar.ExecutarAsync(app.Configuration, Environment.GetEnvironmentVariable(MigracaoAoIniciar.Variavel),
        escopo.ServiceProvider.GetRequiredService<IBancoDeDados>(), app.Logger);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // descrição da API em /openapi/v1.json
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<CarregarUsuarioMiddleware>();
app.UseAuthorization();

app.MapAutenticacao();
app.MapPessoas();
app.MapSeguranca();
app.MapConsultas();
app.MapCadastros();
app.MapMetas();
app.MapTerritorios();
app.MapMenu();

app.Run();

/// <summary>Visível para testes de integração (WebApplicationFactory).</summary>
public partial class Program { }
