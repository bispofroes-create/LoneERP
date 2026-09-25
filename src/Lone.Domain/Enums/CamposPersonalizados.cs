namespace Lone.Domain.Enums;

/// <summary>Tipo de um campo personalizado. Gravado no banco: nunca renumere; tipos novos entram no fim.</summary>
public enum TipoCampoPersonalizado : byte
{
    Texto = 0,
    TextoLongo = 1,
    Inteiro = 2,
    Decimal = 3,
    SimNao = 4,
    Data = 5,
    Hora = 6,
    DataHora = 7,
    Lista = 8,
    Moeda = 9,
    Email = 10,
    Telefone = 11,
    Cpf = 12,
    Cnpj = 13
}

/// <summary>Cadastro que recebe campos personalizados (produtos, pedidos... entram aqui depois).</summary>
public enum EntidadePersonalizavel : byte
{
    Pessoa = 0,

    /// <summary>Documentos da pessoa: cada campo vale para um tipo de documento (ex.: "Categoria" da CNH).</summary>
    Documento = 1
}
