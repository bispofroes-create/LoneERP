namespace Lone.Core.Enums;

/// <summary>Documentos adicionais. CPF/CNPJ e inscrições ficam na pessoa e no estabelecimento.</summary>
public enum TipoDocumento : byte
{
    Rg = 0,
    Cnh = 1,
    Passaporte = 2,
    DocumentoEstrangeiro = 3,
    Outro = 9
}
