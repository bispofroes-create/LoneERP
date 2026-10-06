using Lone.Cliente.Api;
using Lone.Cliente.Mensagens;
using Lone.Cliente.Navegacao;
using Lone.Cliente.Sessao;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Metas;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Cliente.ViewModels.Seguranca;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Cliente;

public static class ConfiguracaoCliente
{
    /// <summary>
    /// Registra sessão, cliente da API, serviços e ViewModels. O aplicativo registra antes as implementações
    /// de plataforma (IArmazenamentoSeguro, IPreferencias, IDispositivo, INavegacao) e um HttpClient.
    /// </summary>
    public static IServiceCollection AddLoneCliente(this IServiceCollection services)
    {
        services.AddSingleton<SessaoCliente>();
        // Mensagens da interface (toast): um só serviço no aplicativo, o mesmo que os ViewModels usam por padrão.
        services.AddSingleton(ServicoMensagens.Padrao);
        // Motor global de navegação (histórico, Voltar, links): um só no aplicativo, o mesmo que as telas usam por padrão.
        services.AddSingleton(GerenciadorNavegacao.Padrao);
        services.AddSingleton<AberturaDePessoa>();
        services.AddSingleton<AberturaDeOperacaoTerritorial>();
        services.AddSingleton<ConfiguracaoServidor>();
        services.AddSingleton<ClienteApi>();
        services.AddSingleton<ServicoAutenticacao>();
        services.AddSingleton<PessoasApi>();
        services.AddSingleton<UsuariosApi>();
        services.AddSingleton<PerfisApi>();
        services.AddSingleton<ConsultasApi>();
        services.AddSingleton<MunicipiosApi>();
        services.AddSingleton<CamposPersonalizadosApi>();
        services.AddSingleton<EtiquetasApi>();
        services.AddSingleton<GruposEmpresariaisApi>();
        services.AddSingleton<ProfissoesApi>();
        services.AddSingleton<PapeisApi>();
        services.AddSingleton<TiposMeioContatoApi>();
        services.AddSingleton<TiposEnderecoApi>();
        services.AddSingleton<FinalidadesTratamentoApi>();
        services.AddSingleton<TiposDocumentoApi>();
        services.AddSingleton<AnexosApi>();
        services.AddSingleton<ColaboradoresApi>();
        services.AddSingleton<ComercialApi>();
        services.AddSingleton<PrazosPeriodoApi>();
        services.AddSingleton<PrazosProntos>();
        services.AddSingleton<MetasApi>();
        services.AddSingleton<TerritoriosApi>();
        services.AddSingleton<MenuUsuarioApi>();
        services.AddSingleton<FluxoDeEntrada>();

        // Um ViewModel novo a cada abertura de tela.
        services.AddTransient<LoginViewModel>();
        services.AddTransient<PrimeiroAcessoViewModel>();
        services.AddTransient<EscolherEmpresaViewModel>();
        services.AddTransient<TrocarSenhaViewModel>();
        services.AddTransient<MenuViewModel>();
        services.AddTransient<InicioViewModel>();
        services.AddTransient<ConfiguracoesViewModel>();
        services.AddTransient<UsuariosViewModel>();
        services.AddTransient<PerfisViewModel>();
        services.AddTransient<PessoasViewModel>();
        services.AddTransient<CamposPersonalizadosViewModel>();
        services.AddTransient<EtiquetasViewModel>();
        services.AddTransient<GruposEmpresariaisViewModel>();
        services.AddTransient<ProfissoesViewModel>();
        services.AddTransient<PapeisViewModel>();
        services.AddTransient<TiposMeioContatoViewModel>();
        services.AddTransient<TiposEnderecoViewModel>();
        services.AddTransient<FinalidadesTratamentoViewModel>();
        services.AddTransient<TiposDocumentoViewModel>();
        services.AddTransient<CargosViewModel>();
        services.AddTransient<DepartamentosViewModel>();
        services.AddTransient<EquipesViewModel>();
        services.AddTransient<IndicadoresViewModel>();
        services.AddTransient<MetasViewModel>();
        services.AddTransient<SetoresViewModel>();
        services.AddTransient<CentrosCustoViewModel>();
        services.AddTransient<PerfisComerciaisViewModel>();
        services.AddTransient<CondicoesPagamentoViewModel>();
        services.AddTransient<TiposCarteiraViewModel>();
        services.AddTransient<TiposAusenciaViewModel>();
        services.AddTransient<PrazosPeriodoViewModel>();
        services.AddTransient<Lone.Cliente.ViewModels.Comercial.ParametrosComerciaisViewModel>();
        services.AddTransient<Lone.Cliente.ViewModels.Comercial.CoberturasViewModel>();
        services.AddTransient<Lone.Cliente.ViewModels.Comercial.CarteiraVencendoViewModel>();
        services.AddTransient<Lone.Cliente.ViewModels.Comercial.TransferenciasViewModel>();
        services.AddTransient<Lone.Cliente.ViewModels.Comercial.CarteiraEmDataViewModel>();
        services.AddTransient<Lone.Cliente.ViewModels.Territorios.TiposTerritorioViewModel>(); // Fase 2b-1a
        services.AddTransient<Lone.Cliente.ViewModels.Territorios.MapasTerritoriaisViewModel>();
        services.AddTransient<Lone.Cliente.ViewModels.Territorios.TerritoriosViewModel>();
        services.AddTransient<Lone.Cliente.ViewModels.Territorios.OperacoesTerritoriaisViewModel>(); // Fase 2b-1b
        services.AddTransient<Lone.Cliente.ViewModels.Territorios.DivergenciasTerritoriaisViewModel>();
        services.AddTransient<Lone.Cliente.ViewModels.Territorios.ParametrosTerritoriaisViewModel>();
        services.AddTransient<Lone.Cliente.ViewModels.Pessoas.ReconferenciaCepViewModel>(); // F6 do motor de CEP

        return services;
    }
}
