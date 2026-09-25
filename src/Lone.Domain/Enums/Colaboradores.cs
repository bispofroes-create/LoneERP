namespace Lone.Domain.Enums;

/// <summary>Tipo de vínculo do colaborador com a empresa. Gravado no banco: nunca renumere; tipos novos no fim.</summary>
public enum TipoVinculo : byte
{
    Clt = 0,
    Estagio = 1,
    Aprendiz = 2,
    Temporario = 3,
    PrestadorPj = 4,
    Autonomo = 5,
    Diretor = 6,
    Outro = 9
}
