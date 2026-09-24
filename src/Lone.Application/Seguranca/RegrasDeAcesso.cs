using Lone.Contracts.Empresas;
using Lone.Domain.Entidades;

namespace Lone.Application.Seguranca;

/// <summary>Um perfil do usuário, na empresa em que vale (EmpresaId nulo = todas as empresas).</summary>
public sealed record PerfilAtribuido(Guid? EmpresaId, bool Administrador, IReadOnlySet<string> Permissoes);

/// <summary>O que vale para o usuário numa empresa: soma dos perfis "em todas" e dos atribuídos a ela.</summary>
public sealed record AcessoEfetivo(bool Administrador, IReadOnlySet<string> Permissoes)
{
    public bool Possui(string permissao) => Administrador || Permissoes.Contains(permissao);
}

/// <summary>Regras de acesso por empresa, num lugar só (login, troca de empresa e cada requisição da API).</summary>
public static class RegrasDeAcesso
{
    /// <summary>Perfis ativos do usuário (o usuário precisa vir com Perfis.Perfil.Permissoes carregados).</summary>
    public static IReadOnlyList<PerfilAtribuido> PerfisAtivos(Usuario usuario) =>
        usuario.Perfis
            .Where(up => up.Perfil is { Ativo: true })
            .Select(up => new PerfilAtribuido(
                up.EmpresaId,
                up.Perfil!.Administrador,
                up.Perfil.Permissoes.Select(p => p.Codigo).ToHashSet()))
            .ToList();

    /// <summary>O usuário vê as empresas em que tem algum perfil (perfil "em todas" = todas).</summary>
    public static IReadOnlyList<EmpresaAtiva> EmpresasDisponiveis(
        IReadOnlyList<PerfilAtribuido> perfis, IReadOnlyList<EmpresaAtiva> estabelecimentosDoGrupo) =>
        perfis.Any(p => p.EmpresaId is null)
            ? estabelecimentosDoGrupo
            : estabelecimentosDoGrupo.Where(e => perfis.Any(p => p.EmpresaId == e.EmpresaId)).ToList();

    /// <summary>Permissões na empresa informada (nula = só os perfis "em todas as empresas").</summary>
    public static AcessoEfetivo Efetivo(IReadOnlyList<PerfilAtribuido> perfis, Guid? empresaId)
    {
        var validos = perfis.Where(p => p.EmpresaId is null || p.EmpresaId == empresaId).ToList();
        return new AcessoEfetivo(
            validos.Any(p => p.Administrador),
            validos.SelectMany(p => p.Permissoes).ToHashSet());
    }
}
