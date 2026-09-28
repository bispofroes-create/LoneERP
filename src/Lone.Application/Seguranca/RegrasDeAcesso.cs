using Lone.Contracts.Empresas;
using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Application.Seguranca;

/// <summary>Um perfil do usuário, na empresa em que vale (EmpresaId nulo = todas as empresas).</summary>
public sealed record PerfilAtribuido(Guid? EmpresaId, bool Administrador, IReadOnlySet<string> Permissoes,
                                     AlcanceComercial Alcance = AlcanceComercial.Tudo);

/// <summary>O que vale para o usuário numa empresa: soma dos perfis "em todas" e dos atribuídos a ela.</summary>
public sealed record AcessoEfetivo(bool Administrador, IReadOnlySet<string> Permissoes)
{
    /// <summary>
    /// Alcance em Pessoas e no Comercial (Fase 2a-2): o maior entre os perfis que valem na empresa (decisão E1). O
    /// administrador vê tudo, qualquer que seja o valor gravado.
    /// </summary>
    public AlcanceComercial Alcance
    {
        get => Administrador ? AlcanceComercial.Tudo : _alcance;
        init => _alcance = value;
    }

    private readonly AlcanceComercial _alcance = AlcanceComercial.Tudo;

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
                up.Perfil.Permissoes.Select(p => p.Codigo).ToHashSet(),
                up.Perfil.AlcanceComercial))
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
            validos.SelectMany(p => p.Permissoes).ToHashSet())
        {
            Alcance = RegrasEscopo.Maior(validos.Select(p => p.Administrador ? AlcanceComercial.Tudo : p.Alcance))
        };
    }
}
