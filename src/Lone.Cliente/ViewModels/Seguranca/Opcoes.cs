using Lone.Contracts.Empresas;
using Lone.Contracts.Seguranca;

namespace Lone.Cliente.ViewModels.Seguranca;

/// <summary>Perfil na lista de escolha (o texto exibido é o nome).</summary>
public sealed record OpcaoPerfil(Guid Id, string Nome)
{
    public static OpcaoPerfil De(PerfilResumo p) => new(p.Id, p.Ativo ? p.Nome : $"{p.Nome} (inativo)");
    public override string ToString() => Nome;
}

/// <summary>Empresa na lista de escolha. Id nulo = "Todas as empresas".</summary>
public sealed record OpcaoEmpresa(Guid? Id, string Nome)
{
    public static readonly OpcaoEmpresa Todas = new(null, "Todas as empresas");
    public static OpcaoEmpresa De(EmpresaResumo e) => new(e.Id, e.Ativa ? e.Nome : $"{e.Nome} (inativa)");
    public override string ToString() => Nome;
}

/// <summary>Um perfil dado ao usuário, valendo em uma empresa ou em todas.</summary>
public sealed record PerfilAtribuido(OpcaoPerfil Perfil, OpcaoEmpresa Empresa)
{
    public string Texto => $"{Perfil.Nome} — {Empresa.Nome}";
    public UsuarioPerfilDto ParaDto() => new(Perfil.Id, Empresa.Id);
}
