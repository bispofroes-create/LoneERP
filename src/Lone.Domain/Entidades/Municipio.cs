using System.ComponentModel;
using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>
/// Município brasileiro da tabela oficial do IBGE. É uma tabela de referência: preenchida e atualizada só pela
/// sincronização com o IBGE, nunca pelo cadastro. O Id é o próprio código IBGE (7 dígitos), que é oficial,
/// imutável e o mesmo em todos os aparelhos (é também o código usado na NF-e).
/// </summary>
[DisplayName("Município"), NaoAuditar]
public class Municipio
{
    public int Id { get; set; }

    /// <summary>Nome oficial, com acentos (ex.: "São João del-Rei").</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Nome sem acentos, em maiúsculas e só com letras e números (ex.: "SAO JOAO DEL REI"), para a busca.</summary>
    public string NomeBusca { get; set; } = string.Empty;

    public string Uf { get; set; } = string.Empty;

    /// <summary>Código IBGE da UF (os 2 primeiros dígitos do código do município).</summary>
    public byte CodigoUf { get; set; }

    /// <summary>Falso se o município deixar de constar na lista do IBGE (nunca é apagado: pode haver cadastros ligados a ele).</summary>
    public bool Ativo { get; set; } = true;

    public DateTime AtualizadoEm { get; set; }

    public string NomeComUf => $"{Nome} - {Uf}";
}
