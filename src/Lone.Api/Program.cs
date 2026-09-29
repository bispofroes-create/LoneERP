using Lone.Api;
using Lone.Api.Endpoints;
using Lone.Api.Seguranca;
using Lone.Application;
using Lone.Application.Infraestrutura;
using Lone.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddLoneApplication()
    .AddLoneInfrastructure(builder.Configuration)
    .AddLoneApi(builder.Configuration, builder.Environment);

var app = builder.Build();

// Em desenvolvimento o banco é criado/atualizado sozinho; em produção isso é um passo de implantação.
if (app.Configuration.GetValue<bool>("Banco:AplicarMigracoesAoIniciar"))
{
    await using var escopo = app.Services.CreateAsyncScope();
    await escopo.ServiceProvider.GetRequiredService<IBancoDeDados>().PrepararAsync();
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
