namespace Lone.Core.Enums;

/// <summary>De onde veio uma alteração gravada na auditoria.</summary>
public enum OrigemAlteracao : byte
{
    Usuario = 0,
    ConsultaExterna = 1,
    Importacao = 2,
    Sistema = 3
}
