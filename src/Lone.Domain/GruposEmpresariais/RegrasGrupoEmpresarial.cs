using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.GruposEmpresariais;

/// <summary>Regras do grupo empresarial (cadastro e participação de uma pessoa jurídica). Não acessa banco.</summary>
public static class RegrasGrupoEmpresarial
{
    /// <summary>Espaços nas pontas e repetidos saem ("  Grupo   João " → "Grupo João").</summary>
    public static string? Texto(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? null
            : string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static void Normalizar(GrupoEmpresarial grupo)
    {
        grupo.Nome = Texto(grupo.Nome) ?? string.Empty;
        grupo.Descricao = Texto(grupo.Descricao);
    }

    public static List<string> Validar(GrupoEmpresarial grupo)
    {
        var erros = new List<string>();
        if (grupo.Nome.Length == 0)
            erros.Add("Informe o nome do grupo empresarial.");
        else if (grupo.Nome.Length > GrupoEmpresarial.TamanhoMaximoNome)
            erros.Add($"O nome do grupo empresarial pode ter no máximo {GrupoEmpresarial.TamanhoMaximoNome} caracteres.");
        if (grupo.Descricao is { Length: > GrupoEmpresarial.TamanhoMaximoDescricao })
            erros.Add($"A descrição pode ter no máximo {GrupoEmpresarial.TamanhoMaximoDescricao} caracteres.");
        return erros;
    }

    /// <summary>
    /// Só pessoa jurídica participa de um grupo empresarial. A pessoa física (e o estrangeiro) participa pelos
    /// relacionamentos com as empresas (sócio, administrador...), nunca diretamente. O banco garante o mesmo (CHECK).
    /// </summary>
    public static string? ValidarNatureza(Pessoa pessoa) =>
        pessoa.GrupoEmpresarialId is not null && pessoa.Natureza != NaturezaPessoa.Juridica
            ? "Só pessoa jurídica pode fazer parte de um grupo empresarial. A pessoa física participa pelos relacionamentos com as empresas (sócio, administrador)."
            : null;

    /// <summary>
    /// Confere o grupo escolhido contra o cadastro: precisa existir; um desativado só continua na empresa que já o tinha.
    /// </summary>
    public static string? ValidarEscolhido(Guid? escolhido, Guid? anterior, GrupoEmpresarial? cadastro)
    {
        if (escolhido is not { } id) return null;
        if (cadastro is null) return "O grupo empresarial escolhido não existe mais. Reabra o cadastro e escolha de novo.";
        if (!cadastro.Ativo && anterior != id)
            return $"O grupo empresarial \"{cadastro.Nome}\" está desativado e não pode ser escolhido para outras empresas.";
        return null;
    }

    /// <summary>Frase do histórico quando a empresa entra, sai ou troca de grupo (nulo = não mudou).</summary>
    public static string? Mudanca(Guid? anterior, Guid? atual, Func<Guid, string> nome)
    {
        if (anterior == atual) return null;
        if (anterior is null) return $"Entrou no grupo empresarial '{nome(atual!.Value)}'.";
        if (atual is null) return $"Saiu do grupo empresarial '{nome(anterior.Value)}'.";
        return $"Saiu do grupo empresarial '{nome(anterior.Value)}' e entrou no grupo '{nome(atual.Value)}'.";
    }
}
