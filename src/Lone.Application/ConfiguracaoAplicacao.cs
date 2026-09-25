using Lone.Application.CamposPersonalizados;
using Lone.Application.Etiquetas;
using Lone.Application.Profissoes;
using Lone.Application.Papeis;
using Lone.Application.Contatos;
using Lone.Application.Enderecos;
using Lone.Application.Documentos;
using Lone.Application.Colaboradores;
using Lone.Application.Integracoes;
using Lone.Application.Municipios;
using Lone.Application.Pessoas;
using Lone.Application.Seguranca;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lone.Application;

public static class ConfiguracaoAplicacao
{
    /// <summary>
    /// Registra os casos de uso. Tudo "scoped": na API, um por requisição (o usuário da requisição
    /// é quem responde IUsuarioAtual, IEmpresaAtual e IAutorizacao, registrados pela própria API).
    /// Repositórios e integrações são registrados por Lone.Infrastructure.
    /// </summary>
    public static IServiceCollection AddLoneApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IHasherSenha, HasherSenhaPbkdf2>();
        services.AddSingleton<IAutenticador, AutenticadorLocal>();

        services.AddScoped<IAutenticacaoService, AutenticacaoService>();
        services.AddScoped<IAcessoService, AcessoService>();
        services.AddScoped<IUsuarioAppService, UsuarioAppService>();
        services.AddScoped<IPerfilAppService, PerfilAppService>();
        services.AddScoped<IPessoaAppService, PessoaAppService>();
        services.AddScoped<IConsultasAppService, ConsultasAppService>();
        services.AddScoped<ServicoMunicipios>();
        services.AddScoped<IMunicipioAppService, MunicipioAppService>();
        services.AddScoped<ICampoPersonalizadoAppService, CampoPersonalizadoAppService>();
        services.AddScoped<IEtiquetaAppService, EtiquetaAppService>();
        services.AddScoped<IProfissaoAppService, ProfissaoAppService>();
        services.AddScoped<IOcupacaoCboAppService, OcupacaoCboAppService>();
        services.AddScoped<IPapelAppService, PapelAppService>();
        services.AddScoped<ITipoMeioContatoAppService, TipoMeioContatoAppService>();
        services.AddScoped<ITipoEnderecoAppService, TipoEnderecoAppService>();
        services.AddScoped<ITipoDocumentoAppService, TipoDocumentoAppService>();
        services.AddScoped<IAnexoAppService, AnexoAppService>();
        services.AddScoped<ICargoAppService, CargoAppService>();
        services.AddScoped<IDepartamentoAppService, DepartamentoAppService>();
        services.AddScoped<ISetorAppService, SetorAppService>();
        services.AddScoped<ICentroCustoAppService, CentroCustoAppService>();
        services.AddScoped<IColaboradorAppService, ColaboradorAppService>();
        services.AddScoped<ReferenciasColaborador>();

        return services;
    }
}
