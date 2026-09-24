using Lone.Contracts.Comum;
using Lone.Contracts.Empresas;
using Lone.Contracts.Seguranca;

namespace Lone.Cliente.Api;

/// <summary>Chamadas do cadastro de usuários (exige a permissão de gerenciar usuários).</summary>
public sealed class UsuariosApi
{
    private readonly ClienteApi _api;

    public UsuariosApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<List<UsuarioResumo>> ListarAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<UsuarioResumo>>(Rotas.Usuarios.Grupo, ct);

    public Task<UsuarioDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<UsuarioDto>(Rotas.Usuarios.PorId(id), ct);

    /// <summary>Inclui ou altera (o Id novo é gerado pelo aplicativo).</summary>
    public Task<UsuarioDto> SalvarAsync(SalvarUsuarioRequisicao requisicao, CancellationToken ct = default) =>
        _api.PutAsync<UsuarioDto>(Rotas.Usuarios.PorId(requisicao.Usuario.Id), requisicao, ct);

    public Task DesbloquearAsync(Guid id, CancellationToken ct = default) =>
        _api.PostAsync(Rotas.Usuarios.Desbloquear(id), ct);

    /// <summary>Perfis que podem ser atribuídos a um usuário.</summary>
    public Task<List<PerfilResumo>> ListarPerfisAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<PerfilResumo>>(Rotas.Usuarios.PerfisDisponiveis, ct);

    /// <summary>Empresas do grupo, para limitar um perfil a uma empresa.</summary>
    public Task<List<EmpresaResumo>> ListarEmpresasAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<EmpresaResumo>>(Rotas.Empresas.DoGrupo, ct);
}
