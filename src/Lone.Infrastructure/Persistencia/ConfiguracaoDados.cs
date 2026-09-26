using Lone.Application.Auditoria;
using Lone.Application.Empresas;
using Lone.Application.CamposPersonalizados;
using Lone.Application.Etiquetas;
using Lone.Application.GruposEmpresariais;
using Lone.Application.Relacionamentos;
using Lone.Application.Profissoes;
using Lone.Application.Papeis;
using Lone.Application.Contatos;
using Lone.Application.Enderecos;
using Lone.Application.Documentos;
using Lone.Application.Colaboradores;
using Lone.Application.Comercial;
using Lone.Application.Fiscal;
using Lone.Application.Situacoes;
using Lone.Application.Metas;
using Lone.Application.Consultas;
using Lone.Application.Infraestrutura;
using Lone.Application.Municipios;
using Lone.Application.Pessoas;
using Lone.Application.Seguranca;
using Lone.Infrastructure.Persistencia.Consultas;
using Lone.Infrastructure.Persistencia.Repositorios;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Infrastructure.Persistencia;

public static class ConfiguracaoDados
{
    /// <summary>Registra o banco (SQL Server), repositórios e consultas no contêiner de injeção.</summary>
    internal static IServiceCollection AddLoneDados(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<LoneDbContext>(o => o.UseSqlServer(connectionString));

        // Scoped: dependem do usuário da requisição (gravado na auditoria).
        services.AddSingleton<IBancoDeDados, BancoDeDados>();
        services.AddScoped<IPessoaRepositorio, PessoaRepositorio>();
        services.AddScoped<IAuditoriaConsultas, AuditoriaConsultas>();
        services.AddScoped<IEmpresaConsultas, EmpresaConsultas>();
        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<IPerfilRepositorio, PerfilRepositorio>();
        services.AddScoped<ITokenRenovacaoRepositorio, TokenRenovacaoRepositorio>();
        services.AddScoped<IMunicipioRepositorio, MunicipioRepositorio>();
        services.AddScoped<ICampoPersonalizadoRepositorio, CampoPersonalizadoRepositorio>();
        services.AddScoped<IEtiquetaRepositorio, EtiquetaRepositorio>();
        services.AddScoped<IProfissaoRepositorio, ProfissaoRepositorio>();
        services.AddScoped<IOcupacaoCboRepositorio, OcupacaoCboRepositorio>();
        services.AddScoped<IPapelRepositorio, PapelRepositorio>();
        services.AddScoped<ITipoMeioContatoRepositorio, TipoMeioContatoRepositorio>();
        services.AddScoped<ITipoEnderecoRepositorio, TipoEnderecoRepositorio>();
        services.AddScoped<ITipoDocumentoRepositorio, TipoDocumentoRepositorio>();
        services.AddScoped<IAnexoRepositorio, AnexoRepositorio>();
        services.AddScoped<ICargoRepositorio, CargoRepositorio>();
        services.AddScoped<IDepartamentoRepositorio, DepartamentoRepositorio>();
        services.AddScoped<ISetorRepositorio, SetorRepositorio>();
        services.AddScoped<ICentroCustoRepositorio, CentroCustoRepositorio>();
        services.AddScoped<IOcupacoesDoCargo, OcupacoesDoCargo>();
        services.AddScoped<IColaboradorConsultas, ColaboradorConsultas>();
        services.AddScoped<ICondicaoPagamentoRepositorio, CondicaoPagamentoRepositorio>();
        services.AddScoped<IPerfilComercialRepositorio, PerfilComercialRepositorio>();
        services.AddScoped<ITipoCarteiraRepositorio, TipoCarteiraRepositorio>();
        services.AddScoped<IComercialConsultas, ComercialConsultas>();
        services.AddScoped<ICnaeRepositorio, CnaeRepositorio>();
        services.AddScoped<ISituacaoRepositorio, SituacaoRepositorio>();
        services.AddScoped<IGrupoEmpresarialRepositorio, GrupoEmpresarialRepositorio>();
        services.AddScoped<IPessoaRelacionamentoRepositorio, PessoaRelacionamentoRepositorio>();
        services.AddScoped<IEquipeRepositorio, EquipeRepositorio>();
        services.AddScoped<IIndicadorRepositorio, IndicadorRepositorio>();
        services.AddScoped<IMetaRepositorio, MetaRepositorio>();
        services.AddScoped<IMetaConsultas, MetaConsultas>();
        services.AddScoped<IFonteIndicadores, FonteIndicadoresCadastro>();
        services.AddScoped<IConsultaPessoas, ConsultaPessoas>();
        services.AddScoped<Lone.Application.Enderecos.IFinalidadeEnderecoRepositorio, FinalidadeEnderecoRepositorio>();
        services.AddScoped<Lone.Application.Enderecos.IEnderecosDuplicadosConsulta, EnderecosDuplicadosConsulta>();
        services.AddScoped<IFiltroSalvoRepositorio, FiltroSalvoRepositorio>();

        return services;
    }
}
