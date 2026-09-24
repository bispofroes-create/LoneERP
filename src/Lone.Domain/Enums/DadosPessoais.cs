namespace Lone.Domain.Enums;

/// <summary>Sexo do registro civil (o que consta nos documentos e é pedido pelo eSocial).</summary>
public enum SexoRegistro : byte
{
    NaoInformado = 0,
    Feminino = 1,
    Masculino = 2
}

/// <summary>Identidade de gênero, informada pela própria pessoa (opcional, separada do sexo do registro).</summary>
public enum IdentidadeGenero : byte
{
    NaoInformado = 0,
    Mulher = 1,
    Homem = 2,
    NaoBinario = 3,
    Outra = 4,
    PrefereNaoInformar = 9
}

/// <summary>Cor ou raça (categorias do IBGE). Dado sensível (LGPD): só para funcionários, por exigência do eSocial.</summary>
public enum CorRaca : byte
{
    NaoInformado = 0,
    Branca = 1,
    Preta = 2,
    Parda = 3,
    Amarela = 4,
    Indigena = 5
}

/// <summary>Estado civil (os cinco do eSocial, mais união estável).</summary>
public enum EstadoCivil : byte
{
    NaoInformado = 0,
    Solteiro = 1,
    Casado = 2,
    Divorciado = 3,
    Separado = 4,
    Viuvo = 5,
    UniaoEstavel = 6
}

/// <summary>Grau de instrução, com os códigos do eSocial (01 a 12).</summary>
public enum Escolaridade : byte
{
    NaoInformado = 0,
    Analfabeto = 1,
    FundamentalAte5AnoIncompleto = 2,
    Fundamental5AnoCompleto = 3,
    Fundamental6a9AnoIncompleto = 4,
    FundamentalCompleto = 5,
    MedioIncompleto = 6,
    MedioCompleto = 7,
    SuperiorIncompleto = 8,
    SuperiorCompleto = 9,
    PosGraduacao = 10,
    Mestrado = 11,
    Doutorado = 12
}

/// <summary>Canal de comunicação para o qual a pessoa deu (ou retirou) consentimento (LGPD).</summary>
public enum CanalComunicacao : byte
{
    Email = 1,
    WhatsApp = 2,
    Sms = 3,
    Telefone = 4,
    Correspondencia = 5
}
