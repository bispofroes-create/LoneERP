namespace Lone.Domain.Enums;

/// <summary>
/// Por que a migração do cadastro antigo deixou este endereço para revisão manual (a marca geral fica em
/// Pessoa.RevisarFinalidadesEndereco). Gravado só pela migração; a API só apaga (resolvido ou conferido pelo usuário),
/// nunca liga. Ambiguidade de principal (2+ endereços com a mesma finalidade e nenhum principal) não fica aqui: é
/// calculada a cada momento a partir das relações.
/// </summary>
[Flags]
public enum MotivoRevisaoEndereco : short
{
    Nenhum = 0,

    /// <summary>Era o "principal" antigo, mas não tinha nenhuma finalidade: nenhuma foi inventada.</summary>
    AntigoPrincipalSemFinalidade = 1,

    /// <summary>Mais de um endereço ativo da pessoa estava marcado como "principal" antigo (inconsistência).</summary>
    AntigoPrincipalRepetido = 2
}
