using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.CamposPersonalizados;

/// <summary>Coluna da tabela de valores onde um tipo guarda o dado.</summary>
public enum ColunaValor
{
    Texto,
    Numero,
    Data,
    Logico,
    Opcao
}

/// <summary>
/// Comportamento de um tipo de campo personalizado: onde guarda o valor, como normaliza e o que aceita.
/// Um tipo novo é uma classe nova registrada em TiposCampo; nada mais muda (tabela, API e tela já são genéricas).
/// </summary>
public interface ITipoCampo
{
    TipoCampoPersonalizado Tipo { get; }

    /// <summary>Nome mostrado ao administrador (ex.: "Número inteiro").</summary>
    string Nome { get; }

    ColunaValor Coluna { get; }

    /// <summary>
    /// Deixa o valor no formato gravado (ex.: e-mail em minúsculas, decimal arredondado) e devolve o problema,
    /// se houver, sem o nome do campo (ex.: "use um número inteiro"). Nulo = válido.
    /// </summary>
    string? Normalizar(CampoPersonalizado campo, PessoaValorPersonalizado valor);
}
