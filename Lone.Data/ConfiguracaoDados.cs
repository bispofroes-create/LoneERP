using Lone.Aplicacao.Auditoria;
using Lone.Aplicacao.Empresas;
using Lone.Aplicacao.Infraestrutura;
using Lone.Aplicacao.Pessoas;
using Lone.Aplicacao.Seguranca;
using Lone.Data.Consultas;
using Lone.Data.Repositorios;
using Lone.Data.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Data;

public static class ConfiguracaoDados
{
    /// <summary>Registra o banco (SQL Server), repositórios e consultas no contêiner de injeção.</summary>
    public static IServiceCollection AddLoneDados(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<LoneDbContext>(o => o.UseSqlServer(connectionString));

        services.AddSingleton<IBancoDeDados, BancoDeDados>();
        services.AddSingleton<IPessoaRepositorio, PessoaRepositorio>();
        services.AddSingleton<IAuditoriaConsultas, AuditoriaConsultas>();
        services.AddSingleton<IEmpresaConsultas, EmpresaConsultas>();
        services.AddSingleton<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddSingleton<IPerfilRepositorio, PerfilRepositorio>();

        return services;
    }
}
